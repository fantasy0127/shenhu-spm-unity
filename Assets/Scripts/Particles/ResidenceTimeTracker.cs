using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShenhuSpm
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ResidenceTimeTracker : MonoBehaviour
    {
        [SerializeField] private ParticleSystem targetParticleSystem;
        [SerializeField] private ParticleFieldController fieldController;

        private readonly Dictionary<uint, double> entryTimes = new Dictionary<uint, double>();
        private readonly HashSet<uint> currentSeeds = new HashSet<uint>();
        private readonly List<uint> departedSeeds = new List<uint>();
        private ParticleSystem.Particle[] particles;
        private double lastElapsedHours = double.NaN;
        private double summedResidenceHours;

        public long CompletedParticleCount { get; private set; }

        public double MeanResidenceTimeHours
        {
            get
            {
                return CompletedParticleCount == 0
                    ? double.NaN
                    : summedResidenceHours / CompletedParticleCount;
            }
        }

        private void Awake()
        {
            if (targetParticleSystem == null)
            {
                targetParticleSystem = GetComponent<ParticleSystem>();
            }

            if (fieldController == null)
            {
                fieldController = GetComponent<ParticleFieldController>();
            }
        }

        private void LateUpdate()
        {
            if (fieldController == null)
            {
                return;
            }

            double elapsedHours = fieldController.CurrentElapsedHours;
            if (!double.IsNaN(lastElapsedHours) && elapsedHours < lastElapsedHours)
            {
                ResetTracking();
            }

            lastElapsedHours = elapsedHours;
            ScanParticles(elapsedHours);
        }

        [ContextMenu("Reset Residence-Time Statistics")]
        public void ResetTracking()
        {
            entryTimes.Clear();
            currentSeeds.Clear();
            departedSeeds.Clear();
            CompletedParticleCount = 0;
            summedResidenceHours = 0.0;
            lastElapsedHours = double.NaN;
        }

        private void ScanParticles(double elapsedHours)
        {
            int count = targetParticleSystem.particleCount;
            if (particles == null || particles.Length < count)
            {
                particles = new ParticleSystem.Particle[count];
            }

            count = targetParticleSystem.GetParticles(particles);
            currentSeeds.Clear();

            for (int i = 0; i < count; i++)
            {
                uint seed = particles[i].randomSeed;
                currentSeeds.Add(seed);
                if (!entryTimes.ContainsKey(seed))
                {
                    entryTimes.Add(seed, elapsedHours);
                }
            }

            departedSeeds.Clear();
            foreach (KeyValuePair<uint, double> entry in entryTimes)
            {
                if (!currentSeeds.Contains(entry.Key))
                {
                    double residence = Math.Max(0.0, elapsedHours - entry.Value);
                    summedResidenceHours += residence;
                    CompletedParticleCount++;
                    departedSeeds.Add(entry.Key);
                }
            }

            foreach (uint seed in departedSeeds)
            {
                entryTimes.Remove(seed);
            }
        }
    }
}
