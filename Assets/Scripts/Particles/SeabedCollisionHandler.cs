using System.Collections.Generic;
using UnityEngine;

namespace ShenhuSpm
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SeabedCollisionHandler : MonoBehaviour
    {
        [SerializeField] private ParticleSystem targetParticleSystem;
        [SerializeField] private ParticleFieldController fieldController;
        [SerializeField] private CollisionStatistics collisionStatistics;
        [Tooltip("Assign the Seabed layer. Terrain child objects must use the same layer.")]
        [SerializeField] private LayerMask seabedLayers;
        [SerializeField] private bool configureCollisionModuleOnAwake = true;

        private readonly List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();

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

            if (collisionStatistics == null)
            {
                collisionStatistics = GetComponent<CollisionStatistics>();
            }

            if (configureCollisionModuleOnAwake)
            {
                ConfigureCollisionModule();
            }
        }

        [ContextMenu("Configure Collision Module")]
        public void ConfigureCollisionModule()
        {
            if (seabedLayers.value == 0)
            {
                Debug.LogWarning(
                    "Seabed collision mask is empty. Create a Seabed layer and assign it to the terrain collider objects.",
                    this);
            }

            ParticleSystem.CollisionModule collision = targetParticleSystem.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = seabedLayers;
            collision.dampen = 0f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
            collision.sendCollisionMessages = true;
        }

        private void OnEnable()
        {
            if (fieldController != null) fieldController.PlaybackReset += ResetDiagnostics;
        }

        private void OnDisable()
        {
            if (fieldController != null) fieldController.PlaybackReset -= ResetDiagnostics;
        }

        private void ResetDiagnostics()
        {
            if (collisionStatistics != null) collisionStatistics.ResetStatistics();
        }

        private void Update()
        {
            if (collisionStatistics != null && fieldController != null)
            {
                collisionStatistics.AdvanceTime(fieldController.CurrentElapsedHours);
            }
        }

        private void OnParticleCollision(GameObject other)
        {
            if ((seabedLayers.value & (1 << other.layer)) == 0)
            {
                return;
            }

            int count = ParticlePhysicsExtensions.GetCollisionEvents(targetParticleSystem, other, collisionEvents);
            if (count > 0 && collisionStatistics != null)
            {
                double elapsedHours = fieldController == null ? 0.0 : fieldController.CurrentElapsedHours;
                double representativeWeight = 1.0;
                if (fieldController != null &&
                    fieldController.CurrentFrame != null &&
                    fieldController.CurrentFrame.Mapping != null &&
                    fieldController.CurrentFrame.Mapping.RenderTargetCount > 0)
                {
                    representativeWeight =
                        fieldController.CurrentFrame.Mapping.TheoreticalVirtualFlocCount /
                        fieldController.CurrentFrame.Mapping.RenderTargetCount;
                }

                collisionStatistics.RecordCollisions(count, elapsedHours, representativeWeight);
            }
        }
    }
}
