using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShenhuSpm
{
    [DisallowMultipleComponent]
    public sealed class CollisionStatistics : MonoBehaviour
    {
        private struct CollisionSample
        {
            public double ElapsedHours;
            public int Count;
            public double WeightedCount;
        }

        [Tooltip("Projected seabed area represented by the particle domain (m2).")]
        [Min(0.0001f)]
        [SerializeField] private float referenceSeabedAreaM2 = 1f;
        [Min(0.0001f)]
        [SerializeField] private float reportingWindowHours = 1f;

        private readonly Queue<CollisionSample> samples = new Queue<CollisionSample>();
        private int collisionsInWindow;
        private double weightedCollisionsInWindow;

        public long TotalCollisions { get; private set; }
        public double TotalWeightedCollisions { get; private set; }

        public double RawCollisionFrequencyPerM2PerHour
        {
            get
            {
                return collisionsInWindow / (referenceSeabedAreaM2 * reportingWindowHours);
            }
        }

        public double CollisionFrequencyPerM2PerHour
        {
            get
            {
                return weightedCollisionsInWindow / (referenceSeabedAreaM2 * reportingWindowHours);
            }
        }

        public void RecordCollisions(int count, double elapsedHours, double representativeWeight)
        {
            if (count <= 0)
            {
                return;
            }

            representativeWeight = Math.Max(1.0, representativeWeight);
            double weightedCount = count * representativeWeight;

            TotalCollisions += count;
            TotalWeightedCollisions += weightedCount;
            collisionsInWindow += count;
            weightedCollisionsInWindow += weightedCount;
            samples.Enqueue(new CollisionSample
            {
                ElapsedHours = elapsedHours,
                Count = count,
                WeightedCount = weightedCount
            });
            RemoveExpiredSamples(elapsedHours);
        }

        public void AdvanceTime(double elapsedHours)
        {
            RemoveExpiredSamples(elapsedHours);
        }

        public void ResetStatistics()
        {
            TotalCollisions = 0;
            TotalWeightedCollisions = 0.0;
            collisionsInWindow = 0;
            weightedCollisionsInWindow = 0.0;
            samples.Clear();
        }

        private void RemoveExpiredSamples(double currentElapsedHours)
        {
            double minimumTime = currentElapsedHours - reportingWindowHours;
            while (samples.Count > 0 && samples.Peek().ElapsedHours < minimumTime)
            {
                CollisionSample expired = samples.Dequeue();
                collisionsInWindow -= expired.Count;
                weightedCollisionsInWindow -= expired.WeightedCount;
            }
        }

        private void OnValidate()
        {
            referenceSeabedAreaM2 = Mathf.Max(0.0001f, referenceSeabedAreaM2);
            reportingWindowHours = Mathf.Max(0.0001f, reportingWindowHours);
        }
    }
}
