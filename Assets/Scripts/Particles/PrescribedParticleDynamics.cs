using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShenhuSpm
{
    // Gravity, horizontal random walks and particle-contact merging.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class PrescribedParticleDynamics : MonoBehaviour
    {
        [Header("Motion and display settings")]
        [SerializeField] private bool configureModulesOnAwake = true;
        [SerializeField] private float gravityMultiplier = 0.001f;
        [SerializeField] private Vector3 emissionBoxSize = new Vector3(8f, 8f, 8f);
        [SerializeField] private Vector3 emissionBoxCenter = new Vector3(0f, 4f, 0f);
        [SerializeField] private float displayDiameterMeters = 0.0256f;
        [Tooltip("Prescribed horizontal random-walk diffusivity in scene units squared / observational second.")]
        [SerializeField] private float horizontalRandomWalk = 0.0001f;
        [SerializeField] private bool enableMerging = true;
        [Range(0f, 1f)] [SerializeField] private float mergeProbabilityPerContactStep = 1f;
        [SerializeField] private int randomSeed = 20261023;

        private System.Random random;
        private ParticleSystem.Particle[] buffer;
        private readonly Dictionary<Vector3Int, List<int>> grid = new Dictionary<Vector3Int, List<int>>();
        private readonly List<List<int>> bucketPool = new List<List<int>>();
        private int bucketCount;
        private bool[] merged;
        public long TotalMergeEvents { get; private set; }

        private void Awake()
        {
            ResetState();
            if (configureModulesOnAwake) ConfigureModules();
        }

        public void ResetState()
        {
            random = new System.Random(randomSeed);
            TotalMergeEvents = 0;
            grid.Clear();
        }

        [ContextMenu("Configure Prescribed Motion")]
        public void ConfigureModules()
        {
            ParticleSystem system = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = gravityMultiplier;
            main.startSpeed = 0f;
            main.startSize = displayDiameterMeters;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = emissionBoxSize;
            shape.position = emissionBoxCenter;
        }

        public void Step(ParticleSystem system, float dataSeconds)
        {
            if (random == null) ResetState();
            if (dataSeconds <= 0f) return;
            int capacity = Math.Max(1, system.particleCount);
            if (buffer == null || buffer.Length < capacity)
            {
                buffer = new ParticleSystem.Particle[capacity];
                merged = new bool[capacity];
            }
            int count = system.GetParticles(buffer);
            Array.Clear(merged, 0, count);
            double sigma = Math.Sqrt(2.0 * Math.Max(0f, horizontalRandomWalk) * dataSeconds);
            float largestDiameter = 0.000001f;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = buffer[i].position;
                position.x += (float)(sigma * Gaussian());
                position.z += (float)(sigma * Gaussian());
                buffer[i].position = position;
                largestDiameter = Math.Max(largestDiameter, buffer[i].GetCurrentSize(system));
            }
            // One merge per particle per data step; a spatial hash avoids all-pairs scans.
            if (enableMerging && mergeProbabilityPerContactStep > 0f)
            {
                grid.Clear();
                bucketCount = 0;
                float cellSize = largestDiameter;
                for (int i = 0; i < count; i++)
                {
                    Vector3Int key = Cell(buffer[i].position, cellSize);
                    List<int> bucket;
                    if (!grid.TryGetValue(key, out bucket))
                    {
                        if (bucketCount == bucketPool.Count) bucketPool.Add(new List<int>());
                        bucket = bucketPool[bucketCount++];
                        bucket.Clear();
                        grid.Add(key, bucket);
                    }
                    bucket.Add(i);
                }
                for (int i = 0; i < count; i++)
                {
                    if (merged[i]) continue;
                    Vector3Int key = Cell(buffer[i].position, cellSize);
                    bool found = false;
                    for (int x = -1; x <= 1 && !found; x++)
                    for (int y = -1; y <= 1 && !found; y++)
                    for (int z = -1; z <= 1 && !found; z++)
                    {
                        List<int> bucket;
                        if (!grid.TryGetValue(key + new Vector3Int(x, y, z), out bucket)) continue;
                        foreach (int j in bucket)
                        {
                            if (j <= i || merged[j]) continue;
                            float di = buffer[i].GetCurrentSize(system);
                            float dj = buffer[j].GetCurrentSize(system);
                            float radiusSum = (di + dj) * 0.5f;
                            if ((buffer[i].position - buffer[j].position).sqrMagnitude >= radiusSum * radiusSum)
                                continue;
                            if (random.NextDouble() >= mergeProbabilityPerContactStep) continue;
                            double vi = di * (double)di * di;
                            double vj = dj * (double)dj * dj;
                            float ratio = (float)(vj / (vi + vj));
                            buffer[i].position = Vector3.Lerp(buffer[i].position, buffer[j].position, ratio);
                            buffer[i].velocity = Vector3.Lerp(buffer[i].velocity, buffer[j].velocity, ratio);
                            buffer[i].startSize = (float)Math.Pow(vi + vj, 1.0 / 3.0);
                            // Preserve the sum of displayed spherical volumes during merging.
                            merged[i] = merged[j] = true;
                            buffer[j].remainingLifetime = -1f;
                            TotalMergeEvents++;
                            found = true;
                            break;
                        }
                    }
                }
            }
            int kept = 0;
            for (int i = 0; i < count; i++)
                if (buffer[i].remainingLifetime > 0f) buffer[kept++] = buffer[i];
            system.SetParticles(buffer, kept);
        }

        private static Vector3Int Cell(Vector3 position, float size)
        {
            return new Vector3Int(Mathf.FloorToInt(position.x / size),
                Mathf.FloorToInt(position.y / size), Mathf.FloorToInt(position.z / size));
        }

        private double Gaussian()
        {
            return Math.Sqrt(-2.0 * Math.Log(1.0 - random.NextDouble())) *
                Math.Cos(2.0 * Math.PI * random.NextDouble());
        }
    }
}
