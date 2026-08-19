using System;

namespace ShenhuSpm
{
    public enum SpmStation
    {
        SH1,
        SH2
    }

    [Serializable]
    public sealed class SpmRecord
    {
        public SpmStation Station;
        public DateTime Timestamp;
        public double ElapsedHours;
        public double TurbidityNtu = double.NaN;
        public double SpmMgPerL = double.NaN;

        public bool HasTurbidity
        {
            get { return !double.IsNaN(TurbidityNtu); }
        }

        public bool HasSpm
        {
            get { return !double.IsNaN(SpmMgPerL); }
        }

        public SpmRecord Clone()
        {
            return new SpmRecord
            {
                Station = Station,
                Timestamp = Timestamp,
                ElapsedHours = ElapsedHours,
                TurbidityNtu = TurbidityNtu,
                SpmMgPerL = SpmMgPerL
            };
        }
    }
}
