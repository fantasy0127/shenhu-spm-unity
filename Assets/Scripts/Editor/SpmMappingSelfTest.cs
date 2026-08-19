#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShenhuSpm.Editor
{
    public static class SpmMappingSelfTest
    {
        [MenuItem("Shenhu SPM/Run Mapping Self Test")]
        public static void Run()
        {
            int failures = 0;
            failures += AssertNear("SH1 at 5 NTU", TurbidityCalibration.ConvertToSpm(SpmStation.SH1, 5.0), 159.455, 1e-9);
            failures += AssertNear("SH1 at 10 NTU", TurbidityCalibration.ConvertToSpm(SpmStation.SH1, 10.0), 475.774, 1e-9);
            failures += AssertNear("SH2 at 2 NTU", TurbidityCalibration.ConvertToSpm(SpmStation.SH2, 2.0), 24.607, 1e-9);

            MappingParameters parameters = new MappingParameters();
            MappingResult mapping = VirtualParticleMapper.Map(1.0, parameters);
            failures += AssertNear("Eq. 11 reference count", mapping.TheoreticalVirtualFlocCount, 883.0, 3.0);
            failures += AssertNear("Eq. 8 spatial scale", mapping.SpatialScaleFactor, 4.096e6, 1e-6);

            List<SpmRecord> interpolationInput = new List<SpmRecord>
            {
                new SpmRecord { Station = SpmStation.SH1, ElapsedHours = 0.0, SpmMgPerL = 10.0 },
                new SpmRecord { Station = SpmStation.SH1, ElapsedHours = 1.0, SpmMgPerL = 20.0 }
            };
            failures += AssertNear(
                "Linear interpolation",
                TimeSeriesInterpolator.SampleAt(interpolationInput, 0.5).SpmMgPerL,
                15.0,
                1e-12);

            string chineseCsv =
                "站点,时间,小时,悬浮物浓度（SH1）\n" +
                "SH1,2021-03-15 12:00:00.000,0.0,527.6871327\n" +
                "SH1,2021-03-15 12:02:00.000,0.033333,782.0653385";
            List<SpmRecord> parsed = CsvSpmDataSource.Parse(chineseCsv, SpmStation.SH1);
            failures += AssertNear("Chinese CSV header parsing", parsed[0].SpmMgPerL, 527.6871327, 1e-9);

            if (failures == 0)
            {
                Debug.Log("Shenhu SPM mapping self-test PASS.");
            }
            else
            {
                Debug.LogError("Shenhu SPM mapping self-test FAIL: " + failures + " failed checks.");
            }
        }

        private static int AssertNear(string name, double actual, double expected, double tolerance)
        {
            if (Math.Abs(actual - expected) <= tolerance)
            {
                Debug.Log(string.Format("PASS {0}: {1:G17}", name, actual));
                return 0;
            }

            Debug.LogError(string.Format("FAIL {0}: actual={1:G17}, expected={2:G17}", name, actual, expected));
            return 1;
        }
    }
}
#endif
