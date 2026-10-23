using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ShenhuSpm
{
    [DisallowMultipleComponent]
    public sealed class MetricExporter : MonoBehaviour
    {
        [SerializeField] private ParticleFieldController fieldController;
        [SerializeField] private CollisionStatistics collisionStatistics;
        [SerializeField] private ResidenceTimeTracker residenceTimeTracker;
        [SerializeField] private string outputFileName = "shenhu_spm_metrics.csv";
        [SerializeField] private bool flushEveryFrame = true;
        [Tooltip("Export the optional virtual particle display-duration diagnostic.")]
        [SerializeField] private bool includeVirtualDisplayDuration;
        private double previousTarget = double.NaN;
        private int previousRendered;
        private bool havePreviousFrame;

        private StreamWriter writer;

        public string OutputPath { get; private set; }

        private void Awake()
        {
            if (fieldController == null)
            {
                fieldController = GetComponent<ParticleFieldController>();
            }

            if (collisionStatistics == null)
            {
                collisionStatistics = GetComponent<CollisionStatistics>();
            }

            if (residenceTimeTracker == null)
            {
                residenceTimeTracker = GetComponent<ResidenceTimeTracker>();
            }
        }

        private void OnEnable()
        {
            if (fieldController == null)
            {
                Debug.LogWarning("MetricExporter requires a ParticleFieldController reference.", this);
                return;
            }

            OpenWriter();
            fieldController.FrameUpdated += WriteFrame;
            fieldController.PlaybackReset += ResetDifferences;
        }

        private void OnDisable()
        {
            if (fieldController != null)
            {
                fieldController.FrameUpdated -= WriteFrame;
                fieldController.PlaybackReset -= ResetDifferences;
            }

            CloseWriter();
        }

        private void ResetDifferences()
        {
            previousTarget = double.NaN;
            havePreviousFrame = false;
        }

        private void OpenWriter()
        {
            OutputPath = Path.Combine(Application.persistentDataPath, SanitizeFileName(outputFileName));
            writer = new StreamWriter(OutputPath, false);
            writer.WriteLine(
                "station,timestamp,elapsed_hours,turbidity_ntu,spm_mg_l," +
                "theoretical_virtual_count,render_target_count,rendered_count,renderer_limit_applied," +
                "raw_collision_events,weighted_collision_equivalent," +
                "raw_collision_frequency_per_m2_per_hour,weighted_collision_frequency_per_m2_per_hour," +
                "completed_virtual_display_durations,mean_virtual_display_duration_hours," +
                "target_count_change,rendered_count_change,data_step_seconds,input_is_preprocessed," +
                "primary_particle_volume_mm3,geometric_volume_ratio");
            writer.Flush();
        }

        private void WriteFrame(ParticleFrame frame)
        {
            if (writer == null || frame == null || frame.Mapping == null)
            {
                return;
            }

            long totalCollisions = collisionStatistics == null ? 0 : collisionStatistics.TotalCollisions;
            double frequency = collisionStatistics == null
                ? 0.0
                : collisionStatistics.CollisionFrequencyPerM2PerHour;
            double rawFrequency = collisionStatistics == null
                ? 0.0
                : collisionStatistics.RawCollisionFrequencyPerM2PerHour;
            double weightedCollisions = collisionStatistics == null
                ? 0.0
                : collisionStatistics.TotalWeightedCollisions;
            long completedResidences = !includeVirtualDisplayDuration || residenceTimeTracker == null
                ? 0
                : residenceTimeTracker.CompletedParticleCount;
            double meanResidenceTime = !includeVirtualDisplayDuration || residenceTimeTracker == null
                ? double.NaN
                : residenceTimeTracker.MeanResidenceTimeHours;

            writer.WriteLine(string.Join(",", new[]
            {
                frame.Station.ToString(),
                frame.Timestamp == DateTime.MinValue ? string.Empty : frame.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                frame.ElapsedHours.ToString("G17", CultureInfo.InvariantCulture),
                ToCsvNumber(frame.TurbidityNtu),
                frame.SpmMgPerL.ToString("G17", CultureInfo.InvariantCulture),
                frame.Mapping.TheoreticalVirtualFlocCount.ToString("G17", CultureInfo.InvariantCulture),
                frame.Mapping.RenderTargetCount.ToString(CultureInfo.InvariantCulture),
                frame.CurrentRenderedCount.ToString(CultureInfo.InvariantCulture),
                frame.Mapping.RendererLimitApplied ? "true" : "false",
                totalCollisions.ToString(CultureInfo.InvariantCulture),
                weightedCollisions.ToString("G17", CultureInfo.InvariantCulture),
                rawFrequency.ToString("G17", CultureInfo.InvariantCulture),
                frequency.ToString("G17", CultureInfo.InvariantCulture),
                completedResidences.ToString(CultureInfo.InvariantCulture),
                ToCsvNumber(meanResidenceTime),
                ToCsvNumber(havePreviousFrame ? frame.Mapping.RoundedTheoreticalVirtualFlocCount - previousTarget : double.NaN),
                havePreviousFrame ? (frame.CurrentRenderedCount - previousRendered).ToString(CultureInfo.InvariantCulture) : string.Empty,
                fieldController.DataStepSeconds.ToString("G17", CultureInfo.InvariantCulture),
                fieldController.InputIsPreprocessed ? "true" : "false",
                frame.Mapping.PrimaryParticleVolumeMm3.ToString("G17", CultureInfo.InvariantCulture),
                frame.Mapping.GeometricVolumeRatio.ToString("G17", CultureInfo.InvariantCulture)
            }));

            previousTarget = frame.Mapping.RoundedTheoreticalVirtualFlocCount;
            previousRendered = frame.CurrentRenderedCount;
            havePreviousFrame = true;
            if (flushEveryFrame)
            {
                writer.Flush();
            }
        }

        private void CloseWriter()
        {
            if (writer == null)
            {
                return;
            }

            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        private static string ToCsvNumber(double value)
        {
            return double.IsNaN(value) ? string.Empty : value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "shenhu_spm_metrics.csv" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return result;
        }
    }
}
