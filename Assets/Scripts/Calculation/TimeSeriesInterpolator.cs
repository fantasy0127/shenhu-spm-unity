using System;
using System.Collections.Generic;

namespace ShenhuSpm
{
    public static class TimeSeriesInterpolator
    {
        public static SpmRecord SampleAt(IList<SpmRecord> records, double elapsedHours)
        {
            if (records == null || records.Count == 0)
            {
                throw new ArgumentException("At least one SPM record is required.", "records");
            }

            if (elapsedHours <= records[0].ElapsedHours)
            {
                return records[0].Clone();
            }

            if (elapsedHours >= records[records.Count - 1].ElapsedHours)
            {
                return records[records.Count - 1].Clone();
            }

            int low = 0;
            int high = records.Count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (records[middle].ElapsedHours <= elapsedHours)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            SpmRecord left = records[low];
            SpmRecord right = records[high];
            double width = right.ElapsedHours - left.ElapsedHours;
            double fraction = width <= 0.0 ? 0.0 : (elapsedHours - left.ElapsedHours) / width;

            return new SpmRecord
            {
                Station = left.Station,
                Timestamp = InterpolateTimestamp(left.Timestamp, right.Timestamp, fraction),
                ElapsedHours = elapsedHours,
                TurbidityNtu = InterpolateFinite(left.TurbidityNtu, right.TurbidityNtu, fraction),
                SpmMgPerL = InterpolateFinite(left.SpmMgPerL, right.SpmMgPerL, fraction)
            };
        }

        public static List<SpmRecord> InterpolateUniform(IList<SpmRecord> records, double stepHours)
        {
            if (stepHours <= 0.0)
            {
                throw new ArgumentOutOfRangeException("stepHours", "Interpolation step must be positive.");
            }

            if (records == null || records.Count == 0)
            {
                throw new ArgumentException("At least one SPM record is required.", "records");
            }

            List<SpmRecord> output = new List<SpmRecord>();
            double start = records[0].ElapsedHours;
            double end = records[records.Count - 1].ElapsedHours;
            for (double time = start; time < end; time += stepHours)
            {
                output.Add(SampleAt(records, time));
            }

            output.Add(SampleAt(records, end));
            return output;
        }

        private static double InterpolateFinite(double left, double right, double fraction)
        {
            bool leftFinite = !double.IsNaN(left);
            bool rightFinite = !double.IsNaN(right);
            if (leftFinite && rightFinite)
            {
                return left + (right - left) * fraction;
            }

            if (leftFinite)
            {
                return left;
            }

            return rightFinite ? right : double.NaN;
        }

        private static DateTime InterpolateTimestamp(DateTime left, DateTime right, double fraction)
        {
            if (left == DateTime.MinValue)
            {
                return right;
            }

            if (right == DateTime.MinValue)
            {
                return left;
            }

            long ticks = left.Ticks + (long)((right.Ticks - left.Ticks) * fraction);
            return new DateTime(ticks, left.Kind);
        }
    }
}
