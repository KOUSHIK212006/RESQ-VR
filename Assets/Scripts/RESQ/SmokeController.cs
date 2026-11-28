using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RESQ
{
    public sealed class SmokeController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] smokeParticleSystems;
        [SerializeField] private EmergencyStateManager emergencyStateManager;
        [SerializeField] private Collider exposureTrigger;
        [SerializeField] private Transform exposureTargetRoot;

        [SerializeField] private UnityEvent smokeActivated;
        [SerializeField] private UnityEvent smokeDeactivated;
        [SerializeField] private UnityEvent exposureStarted;
        [SerializeField] private UnityEvent exposureEnded;

        private readonly HashSet<Collider> exposureColliders = new HashSet<Collider>();

        private bool smokeIsActive;
        private bool playerIsExposed;
        private bool exposureDetectionEnabled;

        public bool SmokeIsActive => smokeIsActive;
        public bool PlayerIsExposed => playerIsExposed;
        public UnityEvent SmokeActivated => smokeActivated;
        public UnityEvent SmokeDeactivated => smokeDeactivated;
        public UnityEvent ExposureStarted => exposureStarted;
        public UnityEvent ExposureEnded => exposureEnded;

        private void Awake()
        {
            PrepareSmokeParticleSystems();
            ValidateExposureConfiguration();
        }

        private void OnEnable()
        {
            if (emergencyStateManager == null)
            {
                return;
            }

            emergencyStateManager.EmergencyStarted?.AddListener(ActivateSmoke);
            emergencyStateManager.StateChanged?.AddListener(OnEmergencyStateChanged);

            if (emergencyStateManager.CurrentState is
                EmergencyState.AlarmActive or EmergencyState.EvacuationActive)
            {
                ActivateSmoke();
            }
        }

        private void OnDisable()
        {
            if (emergencyStateManager != null)
            {
                emergencyStateManager.EmergencyStarted?.RemoveListener(ActivateSmoke);
                emergencyStateManager.StateChanged?.RemoveListener(OnEmergencyStateChanged);
            }

            ResetSmoke();

            if (!gameObject.activeInHierarchy)
            {
                exposureColliders.Clear();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!exposureDetectionEnabled || other == null || !IsExposureTarget(other.transform))
            {
                return;
            }

            if (exposureColliders.Add(other))
            {
                UpdateExposureState();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!exposureDetectionEnabled || other == null)
            {
                return;
            }

            if (exposureColliders.Remove(other))
            {
                UpdateExposureState();
            }
        }

        [ContextMenu("Activate Smoke")]
        public void ActivateSmoke()
        {
            if (smokeIsActive)
            {
                return;
            }

            smokeIsActive = true;

            if (smokeParticleSystems != null)
            {
                foreach (ParticleSystem smokeParticleSystem in smokeParticleSystems)
                {
                    if (smokeParticleSystem != null)
                    {
                        smokeParticleSystem.Play(false);
                    }
                }
            }

            smokeActivated?.Invoke();
            UpdateExposureState();
        }

        [ContextMenu("Deactivate Smoke")]
        public void DeactivateSmoke()
        {
            if (!smokeIsActive)
            {
                return;
            }

            smokeIsActive = false;
            StopSmokeParticleSystems(ParticleSystemStopBehavior.StopEmittingAndClear);
            smokeDeactivated?.Invoke();
            UpdateExposureState();
        }

        [ContextMenu("Reset Smoke")]
        public void ResetSmoke()
        {
            bool wasActive = smokeIsActive;
            smokeIsActive = false;
            StopSmokeParticleSystems(ParticleSystemStopBehavior.StopEmittingAndClear);

            if (wasActive)
            {
                smokeDeactivated?.Invoke();
            }

            UpdateExposureState();
        }

        private void PrepareSmokeParticleSystems()
        {
            if (smokeParticleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem smokeParticleSystem in smokeParticleSystems)
            {
                if (smokeParticleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule mainModule = smokeParticleSystem.main;
                mainModule.playOnAwake = false;
                smokeParticleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void StopSmokeParticleSystems(ParticleSystemStopBehavior stopBehavior)
        {
            if (smokeParticleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem smokeParticleSystem in smokeParticleSystems)
            {
                if (smokeParticleSystem != null)
                {
                    smokeParticleSystem.Stop(false, stopBehavior);
                }
            }
        }

        private void ValidateExposureConfiguration()
        {
            bool exposureWasConfigured = exposureTrigger != null || exposureTargetRoot != null;
            exposureDetectionEnabled =
                exposureTrigger != null &&
                exposureTargetRoot != null &&
                exposureTrigger.isTrigger &&
                exposureTrigger.gameObject == gameObject;

            if (exposureWasConfigured && !exposureDetectionEnabled)
            {
                Debug.LogError(
                    $"{nameof(SmokeController)} exposure detection requires a target root and a trigger Collider on the same GameObject.",
                    this);
            }
        }

        private bool IsExposureTarget(Transform otherTransform)
        {
            return exposureTargetRoot != null &&
                   (otherTransform == exposureTargetRoot ||
                    otherTransform.IsChildOf(exposureTargetRoot));
        }

        private void UpdateExposureState()
        {
            bool isExposedNow =
                smokeIsActive &&
                exposureDetectionEnabled &&
                exposureColliders.Count > 0;

            if (playerIsExposed == isExposedNow)
            {
                return;
            }

            playerIsExposed = isExposedNow;

            if (playerIsExposed)
            {
                exposureStarted?.Invoke();
            }
            else
            {
                exposureEnded?.Invoke();
            }
        }

        private void OnEmergencyStateChanged(EmergencyState state)
        {
            if (state == EmergencyState.Inactive)
            {
                ResetSmoke();
            }
        }
    }
}
