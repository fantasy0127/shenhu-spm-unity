using System;

namespace ShenhuSpm
{
    [Serializable]
    public sealed class MappingParameters
    {
        public double PrimaryParticleDiameterUm = 30.0;
        // Table R1 computational inputs. Set overrides to 0 for diameter-derived values.
        public double PrimaryParticleVolumeMm3Override = 1.413e-5;
        public double GeometricVolumeRatioOverride = 6.2137837e8;
        public double ParticleDensityGPerCm3 = 1.48;
        public double ReferenceWaterVolumeM3 = 1.0;
        public double SensorVolumeCm3 = 125.0;
        public double SimulationVolumeM3 = 512.0;
        public double FlocDiameterMm = 25.6;
        public double FlocFractalDimension = 2.55;
        public double FlocCorrectionCoefficient = 0.93;
        public int RendererParticleLimit = 50000;

        public void Validate()
        {
            if (PrimaryParticleDiameterUm <= 0.0 || ParticleDensityGPerCm3 <= 0.0 ||
                ReferenceWaterVolumeM3 <= 0.0 || SensorVolumeCm3 <= 0.0 ||
                SimulationVolumeM3 <= 0.0 || FlocDiameterMm <= 0.0 ||
                FlocFractalDimension <= 0.0 || FlocCorrectionCoefficient <= 0.0 ||
                RendererParticleLimit <= 0 || PrimaryParticleVolumeMm3Override < 0.0 ||
                GeometricVolumeRatioOverride < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    "parameters",
                    "All mapping parameters must be positive.");
            }
        }
    }

    public sealed class MappingResult
    {
        public double SpmMgPerL;
        public double PrimaryParticleVolumeUm3;
        public double SolidVolumeMm3PerReferenceVolume;
        public double PrimaryParticleCountPerM3;
        public double SpatialScaleFactor;
        public double FlocScaleFactor;
        public double GeometricVolumeRatio;
        public double PrimaryParticleVolumeMm3;
        public double EquivalentSolidVolumePerFlocMm3;
        public double TheoreticalVirtualFlocCount;
        public long RoundedTheoreticalVirtualFlocCount;
        public int RenderTargetCount;
        public bool RendererLimitApplied;
    }

    public static class VirtualParticleMapper
    {
        public static MappingResult Map(double spmMgPerL, MappingParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            parameters.Validate();
            if (double.IsNaN(spmMgPerL) || double.IsInfinity(spmMgPerL) || spmMgPerL < 0.0)
            {
                throw new ArgumentOutOfRangeException("spmMgPerL", "SPM concentration must be finite and non-negative.");
            }

            double primaryDiameterMm = parameters.PrimaryParticleDiameterUm / 1000.0;
            double primaryVolumeMm3 = parameters.PrimaryParticleVolumeMm3Override > 0.0
                ? parameters.PrimaryParticleVolumeMm3Override
                : Math.PI / 6.0 * Math.Pow(primaryDiameterMm, 3.0);
            double primaryVolumeUm3 = primaryVolumeMm3 * 1e9;

            // 1 mg/L = 1 g/m3.
            double massG = spmMgPerL * parameters.ReferenceWaterVolumeM3;
            double solidVolumeCm3 = massG / parameters.ParticleDensityGPerCm3;
            double solidVolumeMm3 = solidVolumeCm3 * 1000.0;
            double primaryCountInReferenceVolume = solidVolumeMm3 / primaryVolumeMm3;
            double primaryCountPerM3 = primaryCountInReferenceVolume / parameters.ReferenceWaterVolumeM3;

            double sensorVolumeM3 = parameters.SensorVolumeCm3 * 1e-6;
            double spatialScale = parameters.SimulationVolumeM3 / sensorVolumeM3;
            double flocScale = parameters.FlocCorrectionCoefficient *
                               Math.Pow(parameters.FlocDiameterMm / primaryDiameterMm,
                                   parameters.FlocFractalDimension);
            double equivalentSolidVolumePerFlocMm3 = flocScale * primaryVolumeMm3;

            double geometricRatio = parameters.GeometricVolumeRatioOverride > 0.0
                ? parameters.GeometricVolumeRatioOverride
                : Math.Pow(parameters.FlocDiameterMm / primaryDiameterMm, 3.0);
            double theoreticalVirtualCount = primaryCountPerM3 * sensorVolumeM3 * spatialScale / geometricRatio;
            long roundedCount = SafeRoundToLong(theoreticalVirtualCount);
            int renderTarget = (int)Math.Min(roundedCount, parameters.RendererParticleLimit);

            return new MappingResult
            {
                SpmMgPerL = spmMgPerL,
                PrimaryParticleVolumeUm3 = primaryVolumeUm3,
                SolidVolumeMm3PerReferenceVolume = solidVolumeMm3,
                PrimaryParticleCountPerM3 = primaryCountPerM3,
                SpatialScaleFactor = spatialScale,
                FlocScaleFactor = flocScale,
                GeometricVolumeRatio = geometricRatio,
                PrimaryParticleVolumeMm3 = primaryVolumeMm3,
                EquivalentSolidVolumePerFlocMm3 = equivalentSolidVolumePerFlocMm3,
                TheoreticalVirtualFlocCount = theoreticalVirtualCount,
                RoundedTheoreticalVirtualFlocCount = roundedCount,
                RenderTargetCount = renderTarget,
                RendererLimitApplied = roundedCount > parameters.RendererParticleLimit
            };
        }

        private static long SafeRoundToLong(double value)
        {
            if (value >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)Math.Round(Math.Max(0.0, value), MidpointRounding.AwayFromZero);
        }
    }
}
