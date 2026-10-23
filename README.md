# Shenhu SPM Unity core code

C# scripts for converting SH1 and SH2 suspended particulate matter (SPM) time series into Unity particle fields. The package includes CSV and optional MySQL input, station-specific calibration, time interpolation, particle-count mapping, prescribed particle motion, merging, seabed collision handling and metric export.

## Requirements

- Tuanjie Editor 1.10.1 or a compatible Unity editor
- Built-in Unity Particle System
- Optional MySQL input: `MySql.Data` and the `SHENHU_MYSQL` scripting symbol

## Setup

Copy `Assets` into the Unity project. Create a particle object for each site and add `ParticleFieldController`, which also requires `PrescribedParticleDynamics`. Assign the station and CSV TextAsset.

For seabed collisions, add `SeabedCollisionHandler` and `CollisionStatistics`. Create a `Seabed` layer, assign it to terrain objects with colliders and select it in the handler. Configure the projected bed area and reporting window for collision diagnostics. The area defaults to 1 m² and should match the scene geometry when area-normalized diagnostics are used.

Add `MetricExporter` to write `shenhu_spm_metrics.csv` to `Application.persistentDataPath`. `ResidenceTimeTracker` is optional and tracks virtual-particle display duration.

The default emission box is 8 × 8 × 8 scene units, centred 4 units above the particle object's origin. With 1 scene unit = 1 m, its volume is 512 m³. Position the object above the seabed and ensure the terrain collider covers the emission region. Terrain, camera and lighting are configured in the Unity scene.

## Concentration-to-particle mapping

| Parameter | Value |
|---|---:|
| Primary-particle diameter | 30 µm |
| Computational primary-particle volume | 1.413 × 10⁻⁵ mm³ |
| Representative material density | 1.48 g/cm³ |
| Reference water volume | 1 m³ |
| Sensor volume | 125 cm³ |
| Visualization domain volume | 512 m³ |
| Display floc diameter | 25.6 mm |
| Geometric volume ratio | 6.2137837 × 10⁸ |
| Fractal dimension | 2.55 |
| Correction coefficient | 0.93 |
| Renderer particle limit | 50,000 |

The mapped count is calculated as:

```text
primaryCountPerM3 × simulationVolumeM3 / geometricVolumeRatio
```

These computational inputs give approximately 39.401245666 virtual particles per mg/L before rounding. Targets are rounded to the nearest integer, with midpoint ties away from zero. The fractal factor is used separately to calculate equivalent solid volume per floc.

`PrimaryParticleVolumeMm3Override` and `GeometricVolumeRatioOverride` store the computational values used in Table R1. Setting either override to zero enables calculation of that quantity from the corresponding diameters. When changing particle parameters, update the associated override or select the diameter-derived calculation.

Example targets:

| SPM concentration (mg/L) | Target count |
|---:|---:|
| 1268.65 | 49,986 |
| 182.05 | 7,173 |
| 130.96 | 5,160 |
| 6.74 | 266 |
| 8.68 | 342 |

The SH1 snapshot checks use the available concentration precision: 481.699, 1065.512, 1223.524, 1268.650 and 459.807 mg/L. Retain this precision until the final count-rounding step.

The renderer limit caps the display target at 50,000. Exported data distinguish the uncapped mapped count, capped display target and current rendered count.

## Input and calibration

CSV columns:

```text
station,timestamp,elapsed_hours,turbidity_ntu,spm_mg_l
```

`spm_mg_l` is used when present. Otherwise, turbidity is converted using the station-specific coefficients in `TurbidityCalibration.cs`. Calibration intervals are open on the left and closed on the right. The default out-of-range policy raises an error; optional policies return NaN or clamp the input to the calibration range.

For event playback, smoothing is completed before import. The controller linearly interpolates the supplied concentration values. `inputIsPreprocessed` records the input status in the export. If the input contains only turbidity, concentration is calculated at each observation node before interpolation.

The example files demonstrate the input structure with 120 s spacing for SH1 and 5 s spacing for SH2. SH2 example timestamps use a demonstration time grid; concentration values are retained from the supplied sample records. The files provide limited sample inputs for testing the workflow.

## Time interpolation and playback

`dataStepSeconds = 0.1` specifies the interpolation interval in observational seconds. A 120 s SH1 interval contains 1,200 interpolation steps; a 5 s SH2 interval contains 50. Concentration is evaluated at each step without constructing a dense copy of the complete time series.

`dataHoursPerRealSecond` controls playback speed. The default 1/3600 advances one observational second per wall-clock second. `maxDataStepsPerFrame` limits work in each rendered frame; queued steps are processed in subsequent frames.

The controller advances the Particle System with `Simulate()` at each data-time step. Automatic playback remains paused. Use the controller as the sole playback driver for this Particle System. Original observation intervals remain 120 s at SH1 and 5 s at SH2.

## Particle motion and merging

`PrescribedParticleDynamics` provides gravity-driven vertical motion and Gaussian horizontal random-walk displacements. Configurable defaults are:

| Setting | Default |
|---|---:|
| Gravity multiplier | 0.001 |
| Horizontal random-walk coefficient | 0.0001 scene-units²/s |
| Display diameter | 0.0256 m |
| Merging probability per contact step | 1 |
| Random seed | 20261023 |

These settings prescribe visualization behavior. Unity's global gravity also affects vertical motion. Parameters can be adjusted in the Inspector.

Particle contact is detected when centre separation is less than the sum of displayed radii. Eligible pairs merge into one larger displayed particle. The sum of their displayed spherical volumes is preserved for the merge; position and velocity are volume-weighted. A spatial hash is used for neighbour searches, and each particle can merge once per data step.

Seabed contact removes a particle immediately through the collision module with lifetime loss set to 1. Separately, count matching gradually adds or removes particles to approach the concentration-derived target. `countAdjustmentRate` sets the response rate. Stochastic rounding determines the adjustment count, and removals select particles uniformly without replacement. Initial loading and explicit seeking set the target immediately.

The current displayed count can differ from the mapped target because of gradual adjustment, merging and seabed contact. The numerical lifetime defaults to 10⁹ s. The combined rules express concentration-driven visual changes; they do not solve a suspended-mass conservation equation.

## Exported metrics

`MetricExporter` records concentration, mapped count, capped target, rendered count, signed count changes, interpolation interval, preprocessing status and mapping inputs. Negative count changes indicate decreases. Endpoint differences can be calculated from selected records.

Collision counts and area-normalized frequencies remain available as simulated-contact diagnostics. Their interpretation depends on the prescribed motion, collision rules, reference area and reporting window.

Virtual display-duration export is disabled by default. To enable it, attach `ResidenceTimeTracker` and select `includeVirtualDisplayDuration`. Its CSV fields are `completed_virtual_display_durations` and `mean_virtual_display_duration_hours`. The tracker samples particle seeds at rendered frames, so accelerated multi-step playback can miss departures within a frame. The approximately 1.5 h concentration-decline intervals are calculated from concentration timestamps separately.

Resetting or seeking clears diagnostic baselines. `StepToHours` initializes the display at the requested concentration. Collision and display-duration histories begin from that initialized state.

## Checks

- Editor: **Shenhu SPM > Run Mapping Self Test** checks station conversion values, the reference mapping, ten snapshot targets, interpolation and CSV parsing.
- Play mode: assign both sample files to `QuickTestRunner` and select **Run Quick Test**.
- Numerical/source checks: run `python3 Verification/verify_revision.py`. Optional C# syntax parsing uses the `tree-sitter` and `tree-sitter-c-sharp` Python packages.

Check engine compilation, seabed collisions, rendering and playback performance in the target Unity/Tuanjie project. Numerical and syntax check results are recorded in `Verification/verification_results.txt`.

## Data availability and license

Complete observational datasets are subject to university and project data-sharing restrictions. Source code, documentation, quick-test scripts and limited sample input records are provided for inspection and workflow testing. The sample records demonstrate the input format and workflow. The MIT License applies to the source code; see `LICENSE`.
