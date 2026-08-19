# Shenhu SPM Unity code

This repository contains a C# implementation of the workflow for converting SH1 and SH2 suspended particulate matter (SPM) observations into Unity particle fields. It includes CSV and optional MySQL input, station calibration, time interpolation, particle-number mapping, seabed collision statistics, residence-time tracking, and metric export.

## Requirements

- Tuanjie Editor 1.10.1 or a compatible Unity editor
- Built-in Unity Particle System
- Optional MySQL input: `MySql.Data`

The CSV example and formula self-test do not require MySQL.

## Quick test

1. Copy the `Assets` folder into a Unity/Tuanjie project.
2. Wait for script compilation to finish.
3. Select `Shenhu SPM > Run Mapping Self Test` from the editor menu.
4. Check that the Console ends with `Shenhu SPM mapping self-test PASS.`

The test checks the SH1/SH2 calibration coefficients, linear interpolation, CSV parsing, the spatial scale factor, and the reference result of approximately 883 virtual particles at 1 mg/L.

Two short input examples are provided in:

```text
Assets/StreamingAssets/ExampleData/sh1_spm_sample.csv
Assets/StreamingAssets/ExampleData/sh2_spm_sample.csv
```

To test the CSV workflow in Play mode, add `QuickTestRunner` to an empty GameObject, assign both CSV files, and select `Run Quick Test` from the component menu.

## Unity setup

Use one Particle System for SH1 and one for SH2. Add these components to each particle object:

```text
ParticleFieldController
CollisionStatistics
SeabedCollisionHandler
ResidenceTimeTracker
MetricExporter
```

In `ParticleFieldController`, select the station and assign the corresponding CSV file. The component reads the time series and updates the target particle count. `Renderer Particle Limit` is a display limit; the uncapped theoretical count is retained in the exported data.

For seabed collisions, create a Unity layer named `Seabed`, assign it to the terrain objects that contain colliders, and select the layer in `SeabedCollisionHandler`. Set `Reference Seabed Area M2` in `CollisionStatistics` to the projected area represented by the particle domain.

`MetricExporter` writes `shenhu_spm_metrics.csv` to `Application.persistentDataPath`.

## CSV format

```text
station,timestamp,elapsed_hours,turbidity_ntu,spm_mg_l
```

`spm_mg_l` is used when present. Otherwise, SPM is calculated from `turbidity_ntu`. If both time columns are present, `elapsed_hours` is used for playback and interpolation.

## Default mapping parameters

| Parameter | Value |
|---|---:|
| Primary particle diameter | 30 µm |
| Particle density | 1.48 g/cm3 |
| Sensor volume | 125 cm3 |
| Simulation volume | 512 m3 |
| Floc diameter | 25.6 mm |
| Fractal dimension | 2.55 |
| Correction coefficient | 0.93 |

The calibration coefficients and mapping parameters are defined in `TurbidityCalibration.cs` and `VirtualParticleMapper.cs`.

## Optional MySQL input

1. Install `MySql.Data` in the project.
2. Add `SHENHU_MYSQL` to `Player Settings > Scripting Define Symbols`.
3. Create the table using `Config/schema.sql`.
4. Configure `MySqlConnectionSettings` and call `MySqlSpmDataSource.Load()`.

Do not commit database passwords. `Config/database.example.json` contains placeholders only.

## License

MIT License. See `LICENSE`.
