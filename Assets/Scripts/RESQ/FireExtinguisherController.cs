using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RESQ
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class FireExtinguisherController : MonoBehaviour
    {
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private Transform nozzle;
        [SerializeField] private ParticleSystem suppressionEffect;
        [SerializeField] private EmergencyStateManager emergencyStateManager;
        [SerializeField] private FireController fireController;
        [SerializeField] private FireSuppressionTarget[] targets;
        [SerializeField, Min(0f)] private float suppressionPerSecond = 25f;
        [SerializeField, Min(0.1f)] private float suppressionRange = 5f;

        private bool isSpraying;

        public bool IsSpraying => isSpraying;

        private void Awake()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (suppressionEffect != null)
            {
                ParticleSystem.MainModule main = suppressionEffect.main;
                main.playOnAwake = false;
                suppressionEffect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnEnable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.activated.AddListener(OnActivated);
                grabInteractable.deactivated.AddListener(OnDeactivated);
            }

            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.AddListener(OnEmergencyStateChanged);
            }
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.activated.RemoveListener(OnActivated);
                grabInteractable.deactivated.RemoveListener(OnDeactivated);
            }

            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.RemoveListener(OnEmergencyStateChanged);
            }

            StopSpraying();
        }

        private void Update()
        {
            if (!isSpraying)
            {
                return;
            }

            if (!CanOperate())
            {
                StopSpraying();
                return;
            }

            if (nozzle == null || targets == null || targets.Length == 0)
            {
                return;
            }

            if (!Physics.Raycast(
                    nozzle.position,
                    nozzle.forward,
                    out RaycastHit hit,
                    suppressionRange,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide))
            {
                return;
            }

            FireSuppressionTarget target = hit.collider.GetComponentInParent<FireSuppressionTarget>();
            if (IsConfiguredTarget(target) && !target.IsSuppressed)
            {
                target.ApplySuppression(suppressionPerSecond * Time.deltaTime);
            }
        }

        private void OnActivated(ActivateEventArgs args)
        {
            if (!CanOperate())
            {
                return;
            }

            isSpraying = true;

            if (suppressionEffect != null && !suppressionEffect.isPlaying)
            {
                suppressionEffect.Play(false);
            }
        }

        private void OnDeactivated(DeactivateEventArgs args)
        {
            StopSpraying();
        }

        private void OnEmergencyStateChanged(EmergencyState state)
        {
            if (state is not (EmergencyState.AlarmActive or EmergencyState.EvacuationActive))
            {
                StopSpraying();
            }
        }

        private bool CanOperate()
        {
            return emergencyStateManager != null &&
                   fireController != null &&
                   fireController.FireIsActive &&
                   emergencyStateManager.CurrentState is
                       EmergencyState.AlarmActive or EmergencyState.EvacuationActive;
        }

        private bool IsConfiguredTarget(FireSuppressionTarget target)
        {
            if (target == null || targets == null)
            {
                return false;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void StopSpraying()
        {
            isSpraying = false;

            if (suppressionEffect != null)
            {
                suppressionEffect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
