using UnityEngine;
using UnityEngine.Events;

namespace RESQ
{
    public sealed class FireSuppressionTarget : MonoBehaviour
    {
        [SerializeField] private GameObject[] fireVisualRoots;
        [SerializeField] private ParticleSystem[] fireParticleSystems;
        [SerializeField, Min(0.01f)] private float maxSuppression = 100f;
        [SerializeField] private bool activateVisualsOnEnable = false;
        [SerializeField] private FireController configuredOwner;

        [SerializeField] private UnityEvent suppressionStarted;
        [SerializeField] private UnityEvent<float> suppressionProgressChanged;
        [SerializeField] private UnityEvent fireSuppressed;

        private FireController owner;
        private float currentSuppression;
        private bool isSuppressed;

        public bool IsSuppressed => isSuppressed;
        public float CurrentSuppression => currentSuppression;
        public float MaxSuppression => maxSuppression;
        public float NormalizedSuppression =>
            maxSuppression <= 0f
                ? 0f
                : Mathf.Clamp01(currentSuppression / maxSuppression);

        public UnityEvent SuppressionStarted => suppressionStarted;
        public UnityEvent<float> SuppressionProgressChanged => suppressionProgressChanged;
        public UnityEvent FireSuppressed => fireSuppressed;

        private void OnEnable()
        {
            if (owner == null)
            {
                FindOwnerInParent();
            }

            if (owner != null)
            {
                owner.RegisterTarget(this);
            }
        }

        private void OnDisable()
        {
            if (owner != null)
            {
                owner.UnregisterTarget(this);
            }
        }

        [ContextMenu("Apply 10 Suppression")]
        private void ApplyTestSuppression()
        {
            ApplySuppression(10f);
        }

        public void SetOwner(FireController targetOwner)
        {
            owner = targetOwner;

            if (targetOwner != null)
            {
                configuredOwner = targetOwner;
            }
        }

        public void ClearOwner(bool clearConfiguredOwner = false)
        {
            owner = null;

            if (clearConfiguredOwner)
            {
                configuredOwner = null;
            }
        }

        public void ApplySuppression(float amount)
        {
            if (isSuppressed || amount <= 0f)
            {
                return;
            }

            if (currentSuppression <= 0f)
            {
                suppressionStarted?.Invoke();
            }

            currentSuppression = Mathf.Min(maxSuppression, currentSuppression + amount);
            suppressionProgressChanged?.Invoke(NormalizedSuppression);

            if (currentSuppression >= maxSuppression)
            {
                isSuppressed = true;
                DeactivateTarget();
                fireSuppressed?.Invoke();
            }
        }

        public void ActivateTarget()
        {
            if (isSuppressed)
            {
                return;
            }

            SetVisualRootsActive(true);

            if (fireParticleSystems != null)
            {
                foreach (ParticleSystem particleSystem in fireParticleSystems)
                {
                    if (particleSystem != null)
                    {
                        particleSystem.Play(true);
                    }
                }
            }
        }

        public void DeactivateTarget()
        {
            if (fireParticleSystems != null)
            {
                foreach (ParticleSystem particleSystem in fireParticleSystems)
                {
                    if (particleSystem != null)
                    {
                        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            SetVisualRootsActive(false);
        }

        public void ResetTarget()
        {
            currentSuppression = 0f;
            isSuppressed = false;
            suppressionProgressChanged?.Invoke(0f);
            SetVisualRootsActive(false);

            if (fireParticleSystems != null)
            {
                foreach (ParticleSystem particleSystem in fireParticleSystems)
                {
                    if (particleSystem != null)
                    {
                        particleSystem.Stop(
                            true,
                            ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
        }

        private void FindOwnerInParent()
        {
            owner = configuredOwner != null
                ? configuredOwner
                : GetComponentInParent<FireController>();
        }

        private void SetVisualRootsActive(bool active)
        {
            if (fireVisualRoots == null)
            {
                return;
            }

            foreach (GameObject visualRoot in fireVisualRoots)
            {
                if (visualRoot != null)
                {
                    visualRoot.SetActive(active);
                }
            }
        }
    }
}
