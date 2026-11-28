using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RESQ
{
    public sealed class EmergencyStateControl : MonoBehaviour
    {
        public enum Action
        {
            StartEmergency,
            BeginEvacuation,
            ResetEmergency
        }

        [SerializeField] private EmergencyStateManager emergencyStateManager;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private Action action;

        private void OnEnable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(OnSelected);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(OnSelected);
            }
        }

        private void OnSelected(SelectEnterEventArgs _)
        {
            if (emergencyStateManager == null)
            {
                return;
            }

            switch (action)
            {
                case Action.StartEmergency:
                    emergencyStateManager.StartEmergency();
                    break;
                case Action.BeginEvacuation:
                    emergencyStateManager.BeginEvacuation();
                    break;
                case Action.ResetEmergency:
                    emergencyStateManager.ResetEmergency();
                    break;
            }
        }
    }
}
