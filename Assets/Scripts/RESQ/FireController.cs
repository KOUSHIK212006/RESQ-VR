using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RESQ
{
    public sealed class FireController : MonoBehaviour
    {
        [SerializeField] private FireSuppressionTarget[] targets;

        [SerializeField] private UnityEvent fireActivated;
        [SerializeField] private UnityEvent fireReset;
        [SerializeField] private UnityEvent<float> suppressionProgressChanged;
        [SerializeField] private UnityEvent allFiresSuppressed;

        private readonly List<FireSuppressionTarget> registeredTargets =
            new List<FireSuppressionTarget>();

        private bool fireIsActive;
        private bool allFiresSuppressedRaised;

        public bool FireIsActive => fireIsActive;
        public IReadOnlyList<FireSuppressionTarget> Targets => registeredTargets;
        public int ActiveTargetCount
        {
            get
            {
                int activeCount = 0;

                for (int index = 0; index < registeredTargets.Count; index++)
                {
                    if (registeredTargets[index] != null &&
                        !registeredTargets[index].IsSuppressed)
                    {
                        activeCount++;
                    }
                }

                return activeCount;
            }
        }

        public UnityEvent FireActivated => fireActivated;
        public UnityEvent FireReset => fireReset;
        public UnityEvent<float> SuppressionProgressChanged => suppressionProgressChanged;
        public UnityEvent AllFiresSuppressed => allFiresSuppressed;

        private void Awake()
        {
            if (targets != null)
            {
                for (int index = 0; index < targets.Length; index++)
                {
                    RegisterTarget(targets[index]);
                }
            }

            ResetFire();
        }

        [ContextMenu("Activate Fire")]
        public void ActivateFire()
        {
            if (fireIsActive)
            {
                return;
            }

            fireIsActive = true;
            allFiresSuppressedRaised = false;

            for (int index = 0; index < registeredTargets.Count; index++)
            {
                FireSuppressionTarget target = registeredTargets[index];
                if (target != null)
                {
                    target.ActivateTarget();
                }
            }

            fireActivated?.Invoke();
        }

        [ContextMenu("Reset Fire")]
        public void ResetFire()
        {
            fireIsActive = false;
            allFiresSuppressedRaised = false;

            for (int index = 0; index < registeredTargets.Count; index++)
            {
                FireSuppressionTarget target = registeredTargets[index];
                if (target != null)
                {
                    target.ResetTarget();
                }
            }

            suppressionProgressChanged?.Invoke(0f);
            fireReset?.Invoke();
        }

        [ContextMenu("Deactivate Fire")]
        public void DeactivateFire()
        {
            fireIsActive = false;

            for (int index = 0; index < registeredTargets.Count; index++)
            {
                FireSuppressionTarget target = registeredTargets[index];
                if (target != null)
                {
                    target.DeactivateTarget();
                }
            }
        }

        public void RegisterTarget(FireSuppressionTarget target)
        {
            if (target == null || registeredTargets.Contains(target))
            {
                return;
            }

            registeredTargets.Add(target);
            target.SetOwner(this);
            target.SuppressionProgressChanged.AddListener(OnTargetProgressChanged);
            target.FireSuppressed.AddListener(OnTargetSuppressed);

            if (fireIsActive)
            {
                target.ActivateTarget();
            }
        }

        public void UnregisterTarget(FireSuppressionTarget target)
        {
            if (target == null || !registeredTargets.Contains(target))
            {
                return;
            }

            target.SuppressionProgressChanged.RemoveListener(OnTargetProgressChanged);
            target.FireSuppressed.RemoveListener(OnTargetSuppressed);
            target.ClearOwner(true);
            registeredTargets.Remove(target);
        }

        public float GetOverallSuppressionProgress()
        {
            if (registeredTargets.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            int validTargets = 0;

            for (int index = 0; index < registeredTargets.Count; index++)
            {
                FireSuppressionTarget target = registeredTargets[index];
                if (target != null)
                {
                    total += target.NormalizedSuppression;
                    validTargets++;
                }
            }

            return validTargets == 0 ? 0f : total / validTargets;
        }

        private void OnTargetProgressChanged(float _)
        {
            suppressionProgressChanged?.Invoke(GetOverallSuppressionProgress());
        }

        private void OnTargetSuppressed()
        {
            suppressionProgressChanged?.Invoke(GetOverallSuppressionProgress());

            if (allFiresSuppressedRaised || registeredTargets.Count == 0)
            {
                return;
            }

            for (int index = 0; index < registeredTargets.Count; index++)
            {
                FireSuppressionTarget target = registeredTargets[index];
                if (target != null && !target.IsSuppressed)
                {
                    return;
                }
            }

            allFiresSuppressedRaised = true;
            allFiresSuppressed?.Invoke();
        }

        private void OnDestroy()
        {
            for (int index = registeredTargets.Count - 1; index >= 0; index--)
            {
                FireSuppressionTarget target = registeredTargets[index];
                if (target != null)
                {
                    target.SuppressionProgressChanged.RemoveListener(OnTargetProgressChanged);
                    target.FireSuppressed.RemoveListener(OnTargetSuppressed);
                    target.ClearOwner();
                }
            }

            registeredTargets.Clear();
        }
    }
}
