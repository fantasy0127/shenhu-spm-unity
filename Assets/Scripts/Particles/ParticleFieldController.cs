using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShenhuSpm
{
    [Serializable]
    public sealed class ParticleFrame
    {
        public SpmStation Station;
        public DateTime Timestamp;
        public double ElapsedHours;
        public double TurbidityNtu;
        public double SpmMgPerL;
        public MappingResult Mapping;
        public int CurrentRenderedCount;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticleFieldController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SpmStation station = SpmStation.SH1;
        [SerializeField] private TextAsset csvData;
        [SerializeField] private CalibrationOutOfRangePolicy calibrationOutOfRangePolicy =
            CalibrationOutOfRangePolicy.Throw;

        [Header("Playback")]
        [Tooltip("Number of observational hours advanced per real-time second.")]
        [Min(0.0001f)]
        [SerializeField] private float dataHoursPerRealSecond = 1.0f;
        [SerializeField] private bool loopPlayback;
        [SerializeField] private bool playOnStart;

        [Header("Particle system")]
        [SerializeField] private ParticleSystem targetParticleSystem;
        [Min(1f)]
        [SerializeField] private float particleLifetimeSeconds = 3600f;
        [SerializeField] private MappingParameters mappingParameters = new MappingParameters();

        private List<SpmRecord> records;
        private ParticleSystem.Particle[] particleBuffer;
        private bool isPlaying;
        private double playbackElapsedHours;

        public event Action<ParticleFrame> FrameUpdated;

        public SpmStation Station
        {
            get { return station; }
        }

        public double CurrentElapsedHours
        {
            get { return playbackElapsedHours; }
        }

        public ParticleFrame CurrentFrame { get; private set; }

        private void Awake()
        {
            if (targetParticleSystem == null)
            {
                targetParticleSystem = GetComponent<ParticleSystem>();
            }

            ConfigureParticleSystem();
            if (csvData != null)
            {
                LoadData();
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!isPlaying || records == null || records.Count == 0)
            {
                return;
            }

            playbackElapsedHours += Time.deltaTime * dataHoursPerRealSecond;
            double finalTime = records[records.Count - 1].ElapsedHours;
            if (playbackElapsedHours > finalTime)
            {
                if (loopPlayback)
                {
                    playbackElapsedHours = records[0].ElapsedHours;
                }
                else
                {
                    playbackElapsedHours = finalTime;
                    isPlaying = false;
                }
            }

            ApplyAt(playbackElapsedHours);
        }

        [ContextMenu("Load CSV")]
        public void LoadData()
        {
            if (csvData == null)
            {
                throw new InvalidOperationException("Assign a normalized station CSV TextAsset before loading data.");
            }

            records = CsvSpmDataSource.Parse(csvData.text, station);
            if (records.Count == 0)
            {
                throw new InvalidOperationException("The assigned CSV contains no records.");
            }

            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Station != station)
                {
                    throw new InvalidOperationException(
                        "CSV station " + records[i].Station +
                        " does not match ParticleFieldController station " + station + ".");
                }
            }

            playbackElapsedHours = records[0].ElapsedHours;
            ApplyAt(playbackElapsedHours);
        }

        public void Play()
        {
            if (records == null || records.Count == 0)
            {
                LoadData();
            }

            isPlaying = true;
            if (!targetParticleSystem.isPlaying)
            {
                targetParticleSystem.Play();
            }
        }

        public void Pause()
        {
            isPlaying = false;
            targetParticleSystem.Pause();
        }

        public void ResetPlayback()
        {
            isPlaying = false;
            playbackElapsedHours = records != null && records.Count > 0 ? records[0].ElapsedHours : 0.0;
            targetParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (records != null && records.Count > 0)
            {
                ApplyAt(playbackElapsedHours);
            }
        }

        public void StepToHours(double elapsedHours)
        {
            if (records == null || records.Count == 0)
            {
                LoadData();
            }

            playbackElapsedHours = elapsedHours;
            ApplyAt(playbackElapsedHours);
        }

        private void ConfigureParticleSystem()
        {
            mappingParameters.Validate();

            ParticleSystem.MainModule main = targetParticleSystem.main;
            main.maxParticles = mappingParameters.RendererParticleLimit;
            main.startLifetime = particleLifetimeSeconds;
            main.loop = false;
            main.playOnAwake = false;

            // Particle count is set directly by ResizeParticleSystem().
            ParticleSystem.EmissionModule emission = targetParticleSystem.emission;
            emission.enabled = false;
        }

        private void ApplyAt(double elapsedHours)
        {
            SpmRecord sample = TimeSeriesInterpolator.SampleAt(records, elapsedHours);
            if (!sample.HasSpm && !sample.HasTurbidity)
            {
                throw new InvalidOperationException(
                    "The sampled row has neither spm_mg_l nor turbidity_ntu data.");
            }

            double concentration = sample.HasSpm
                ? sample.SpmMgPerL
                : TurbidityCalibration.ConvertToSpm(station, sample.TurbidityNtu, calibrationOutOfRangePolicy);

            MappingResult mapping = VirtualParticleMapper.Map(concentration, mappingParameters);
            ResizeParticleSystem(mapping.RenderTargetCount);

            CurrentFrame = new ParticleFrame
            {
                Station = station,
                Timestamp = sample.Timestamp,
                ElapsedHours = sample.ElapsedHours,
                TurbidityNtu = sample.TurbidityNtu,
                SpmMgPerL = concentration,
                Mapping = mapping,
                CurrentRenderedCount = targetParticleSystem.particleCount
            };

            Action<ParticleFrame> handler = FrameUpdated;
            if (handler != null)
            {
                handler(CurrentFrame);
            }

            if (!isPlaying && targetParticleSystem.isPlaying)
            {
                targetParticleSystem.Pause();
            }
        }

        private void ResizeParticleSystem(int targetCount)
        {
            targetCount = Mathf.Clamp(targetCount, 0, mappingParameters.RendererParticleLimit);
            int currentCount = targetParticleSystem.particleCount;

            if (currentCount < targetCount)
            {
                if (!targetParticleSystem.isPlaying)
                {
                    targetParticleSystem.Play();
                }

                targetParticleSystem.Emit(targetCount - currentCount);
                return;
            }

            if (currentCount <= targetCount)
            {
                return;
            }

            if (particleBuffer == null || particleBuffer.Length < currentCount)
            {
                particleBuffer = new ParticleSystem.Particle[currentCount];
            }

            int received = targetParticleSystem.GetParticles(particleBuffer);
            targetParticleSystem.SetParticles(particleBuffer, Mathf.Min(targetCount, received));
        }
    }
}
