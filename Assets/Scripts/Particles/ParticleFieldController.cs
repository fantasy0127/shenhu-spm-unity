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
    [RequireComponent(typeof(PrescribedParticleDynamics))]
    public sealed class ParticleFieldController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SpmStation station = SpmStation.SH1;
        [SerializeField] private TextAsset csvData;
        [SerializeField] private CalibrationOutOfRangePolicy calibrationOutOfRangePolicy =
            CalibrationOutOfRangePolicy.Throw;

        [Header("Playback")]
        [Tooltip("Number of observational hours advanced per real-time second.")]
        [Min(0.0000001f)]
        [SerializeField] private float dataHoursPerRealSecond = 1.0f / 3600f;
        [Tooltip("Interpolation interval in observational seconds.")]
        [SerializeField] private double dataStepSeconds = 0.1;
        [SerializeField] private int maxDataStepsPerFrame = 32;
        [Tooltip("Fraction of the count deficit adjusted per observational second.")]
        [SerializeField] private float countAdjustmentRate = 2f;
        [SerializeField] private int countAdjustmentSeed = 20261023;
        [Tooltip("Input SPM values are already smoothed before import; no smoothing is applied here.")]
        [SerializeField] private bool inputIsPreprocessed = true;
        [SerializeField] private bool loopPlayback;
        [SerializeField] private bool playOnStart;

        [Header("Particle system")]
        [SerializeField] private ParticleSystem targetParticleSystem;
        [Min(1f)]
        [SerializeField] private float particleLifetimeSeconds = 1000000000f;
        [SerializeField] private MappingParameters mappingParameters = new MappingParameters();

        private List<SpmRecord> records;
        private ParticleSystem.Particle[] particleBuffer;
        private bool isPlaying;
        private double playbackElapsedHours;
        private double pendingDataSeconds;
        private System.Random countRandom;
        private PrescribedParticleDynamics dynamics;
        public event Action PlaybackReset;
        public double DataStepSeconds { get { return dataStepSeconds; } }
        public bool InputIsPreprocessed { get { return inputIsPreprocessed; } }

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

            countRandom = new System.Random(countAdjustmentSeed);
            dynamics = GetComponent<PrescribedParticleDynamics>();
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

            pendingDataSeconds += Time.deltaTime * dataHoursPerRealSecond * 3600.0;
            int budget = Math.Max(1, maxDataStepsPerFrame);
            double finalTime = records[records.Count - 1].ElapsedHours;
            while (isPlaying && budget-- > 0)
            {
                double remaining = (finalTime - playbackElapsedHours) * 3600.0;
                double step = Math.Min(dataStepSeconds, remaining);
                if (step <= 1e-8)
                {
                    if (loopPlayback) { ResetPlayback(); isPlaying = true; }
                    else { isPlaying = false; }
                    break;
                }
                if (pendingDataSeconds + 1e-9 < step) break;
                pendingDataSeconds -= step;
                playbackElapsedHours += step / 3600.0;
                ApplyAt(playbackElapsedHours, false, (float)step);
            }
            // Carry queued data-time steps into the next rendered frame.
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

            // Convert observed nodes first; interpolation is then in concentration space.
            foreach (SpmRecord record in records)
            {
                if (!record.HasSpm && record.HasTurbidity)
                    record.SpmMgPerL = TurbidityCalibration.ConvertToSpm(
                        station, record.TurbidityNtu, calibrationOutOfRangePolicy);
            }
            targetParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            pendingDataSeconds = 0.0;
            playbackElapsedHours = records[0].ElapsedHours;
            ApplyAt(playbackElapsedHours, true, 0f);
        }

        public void Play()
        {
            if (records == null || records.Count == 0)
            {
                LoadData();
            }

            isPlaying = true;
            // Manual stepping keeps particle simulation on the observational clock.
            targetParticleSystem.Pause();
        }

        public void Pause()
        {
            isPlaying = false;
            targetParticleSystem.Pause();
        }

        public void ResetPlayback()
        {
            isPlaying = false;
            pendingDataSeconds = 0.0;
            countRandom = new System.Random(countAdjustmentSeed);
            if (dynamics != null) dynamics.ResetState();
            Action reset = PlaybackReset;
            if (reset != null) reset();
            playbackElapsedHours = records != null && records.Count > 0 ? records[0].ElapsedHours : 0.0;
            targetParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (records != null && records.Count > 0)
            {
                ApplyAt(playbackElapsedHours, true, 0f);
            }
        }

        public void StepToHours(double elapsedHours)
        {
            if (records == null || records.Count == 0)
            {
                LoadData();
            }

            if (elapsedHours < records[0].ElapsedHours || elapsedHours > records[records.Count - 1].ElapsedHours)
                throw new ArgumentOutOfRangeException("elapsedHours");
            ResetPlayback();
            playbackElapsedHours = elapsedHours;
            ApplyAt(playbackElapsedHours, true, 0f);
        }

        private void ConfigureParticleSystem()
        {
            mappingParameters.Validate();
            if (dataStepSeconds <= 0f || countAdjustmentRate <= 0f || dataHoursPerRealSecond <= 0f)
                throw new InvalidOperationException("Playback intervals and adjustment rate must be positive.");

            ParticleSystem.MainModule main = targetParticleSystem.main;
            main.maxParticles = mappingParameters.RendererParticleLimit;
            main.startLifetime = particleLifetimeSeconds;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = 1f;
            targetParticleSystem.useAutoRandomSeed = false;
            targetParticleSystem.randomSeed = (uint)Math.Max(1, countAdjustmentSeed);
            main.loop = true;
            main.playOnAwake = false;

            // Particle count is set directly by ResizeParticleSystem().
            ParticleSystem.EmissionModule emission = targetParticleSystem.emission;
            emission.enabled = false;
        }

        private void ApplyAt(double elapsedHours, bool initialize, float stepSeconds)
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
            ResizeParticleSystem(mapping.RenderTargetCount, initialize, stepSeconds);
            if (stepSeconds > 0f)
            {
                if (dynamics != null) dynamics.Step(targetParticleSystem, stepSeconds);
                targetParticleSystem.Simulate(stepSeconds, false, false, false);
                targetParticleSystem.Pause();
            }

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

        private void ResizeParticleSystem(int targetCount, bool initialize, float stepSeconds)
        {
            targetCount = Mathf.Clamp(targetCount, 0, mappingParameters.RendererParticleLimit);
            int currentCount = targetParticleSystem.particleCount;

            int difference = targetCount - currentCount;
            int change = Math.Abs(difference);
            if (!initialize && change > 0)
            {
                double expected = change * (1.0 - Math.Exp(-countAdjustmentRate * stepSeconds));
                change = (int)Math.Floor(expected);
                if (countRandom.NextDouble() < expected - change) change++;
            }
            if (difference > 0)
            {
                targetParticleSystem.Emit(change);
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
            // Select removals uniformly without replacement.
            int kept = received;
            int remove = Math.Min(change, received);
            for (int i = 0; i < remove; i++)
            {
                int index = countRandom.Next(kept);
                particleBuffer[index] = particleBuffer[--kept];
            }
            targetParticleSystem.SetParticles(particleBuffer, kept);
        }
    }
}
