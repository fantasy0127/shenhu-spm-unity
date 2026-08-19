using System;

namespace ShenhuSpm
{
    public enum CalibrationOutOfRangePolicy
    {
        Throw,
        ReturnNaN,
        ClampToCalibrationRange
    }

    public sealed class CalibrationSegment
    {
        public readonly double MinimumExclusive;
        public readonly double MaximumInclusive;
        public readonly double Slope;
        public readonly double Intercept;

        public CalibrationSegment(double minimumExclusive, double maximumInclusive, double slope, double intercept)
        {
            MinimumExclusive = minimumExclusive;
            MaximumInclusive = maximumInclusive;
            Slope = slope;
            Intercept = intercept;
        }

        public bool Contains(double turbidityNtu)
        {
            return turbidityNtu > MinimumExclusive && turbidityNtu <= MaximumInclusive;
        }

        public double Evaluate(double turbidityNtu)
        {
            return Slope * turbidityNtu + Intercept;
        }
    }

    public static class TurbidityCalibration
    {
        // Calibration intervals are open on the left and closed on the right.
        private static readonly CalibrationSegment[] Sh1Segments =
        {
            new CalibrationSegment(1.0, 5.0, 38.405, -32.570),
            new CalibrationSegment(5.0, 25.0, 50.423, -28.456),
            new CalibrationSegment(25.0, 50.0, 41.209, -25.839)
        };

        private static readonly CalibrationSegment[] Sh2Segments =
        {
            new CalibrationSegment(1.0, 2.0, 22.136, -19.665),
            new CalibrationSegment(2.0, 5.0, 31.631, -20.934),
            new CalibrationSegment(5.0, 10.0, 32.347, -23.067)
        };

        public static double ConvertToSpm(
            SpmStation station,
            double turbidityNtu,
            CalibrationOutOfRangePolicy outOfRangePolicy = CalibrationOutOfRangePolicy.Throw)
        {
            CalibrationSegment[] segments = station == SpmStation.SH1 ? Sh1Segments : Sh2Segments;
            foreach (CalibrationSegment segment in segments)
            {
                if (segment.Contains(turbidityNtu))
                {
                    return segment.Evaluate(turbidityNtu);
                }
            }

            if (outOfRangePolicy == CalibrationOutOfRangePolicy.ReturnNaN)
            {
                return double.NaN;
            }

            if (outOfRangePolicy == CalibrationOutOfRangePolicy.ClampToCalibrationRange)
            {
                double minimum = segments[0].MinimumExclusive + 1e-9;
                double maximum = segments[segments.Length - 1].MaximumInclusive;
                double clamped = Math.Max(minimum, Math.Min(maximum, turbidityNtu));
                foreach (CalibrationSegment segment in segments)
                {
                    if (segment.Contains(clamped))
                    {
                        return segment.Evaluate(clamped);
                    }
                }
            }

            throw new ArgumentOutOfRangeException(
                "turbidityNtu",
                turbidityNtu,
                "Turbidity is outside the submitted calibration intervals for " + station + ".");
        }
    }
}
