using System.Collections.Generic;
using UnityEngine;

namespace ShenhuSpm
{
    public sealed class QuickTestRunner : MonoBehaviour
    {
        [SerializeField] private TextAsset sh1Csv;
        [SerializeField] private TextAsset sh2Csv;
        [SerializeField] private MappingParameters mappingParameters = new MappingParameters();

        [ContextMenu("Run Quick Test")]
        public void RunQuickTest()
        {
            RunStationTest(sh1Csv, SpmStation.SH1);
            RunStationTest(sh2Csv, SpmStation.SH2);

            MappingResult reference = VirtualParticleMapper.Map(1.0, mappingParameters);
            bool mappingPass = reference.TheoreticalVirtualFlocCount >= 39.400 &&
                               reference.TheoreticalVirtualFlocCount <= 39.403;
            Debug.Log(string.Format(
                "Mapping reference: 1 mg/L -> {0:F3} virtual floc-scale particles. {1}",
                reference.TheoreticalVirtualFlocCount,
                mappingPass ? "PASS" : "FAIL"), this);
        }

        private void RunStationTest(TextAsset asset, SpmStation station)
        {
            if (asset == null)
            {
                Debug.LogWarning("No quick-test CSV assigned for " + station + ".", this);
                return;
            }

            List<SpmRecord> records = CsvSpmDataSource.Parse(asset.text, station);
            SpmRecord middle = TimeSeriesInterpolator.SampleAt(
                records,
                (records[0].ElapsedHours + records[records.Count - 1].ElapsedHours) / 2.0);
            double concentration = middle.HasSpm
                ? middle.SpmMgPerL
                : TurbidityCalibration.ConvertToSpm(station, middle.TurbidityNtu);
            MappingResult result = VirtualParticleMapper.Map(concentration, mappingParameters);

            Debug.Log(string.Format(
                "{0} quick test: {1} rows, middle SPM={2:F6} mg/L, theoretical={3:F0}, rendered target={4}.",
                station,
                records.Count,
                concentration,
                result.TheoreticalVirtualFlocCount,
                result.RenderTargetCount), this);
        }
    }
}
