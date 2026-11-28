using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RESQ
{
    public sealed class EmergencyDoorController : MonoBehaviour
    {
        [SerializeField] private EmergencyStateManager emergencyStateManager;
        [SerializeField] private XRBaseInteractable doorInteractable;
        [SerializeField] private Transform doorLeafPivot;
        [SerializeField] private Renderer accessIndicator;
        [SerializeField, Min(1f)] private float openAngle = 95f;
        [SerializeField, Min(0.1f)] private float rotationSpeed = 4f;
        [SerializeField] private Color lockedIndicatorColor = new Color(1f, 0.45f, 0.05f);
        [SerializeField] private Color readyIndicatorColor = new Color(0.1f, 0.9f, 0.25f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock indicatorProperties;
        private Quaternion closedRotation;
        private Quaternion targetRotation;
        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (doorLeafPivot != null)
            {
                closedRotation = doorLeafPivot.localRotation;
                targetRotation = closedRotation;
            }

            indicatorProperties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.AddListener(OnEmergencyStateChanged);
            }

            if (doorInteractable != null)
            {
                doorInteractable.selectEntered.AddListener(OnDoorSelected);
            }

            OnEmergencyStateChanged(
                emergencyStateManager != null
                    ? emergencyStateManager.CurrentState
                    : EmergencyState.Inactive);
        }

        private void OnDisable()
        {
            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.RemoveListener(OnEmergencyStateChanged);
            }

            if (doorInteractable != null)
            {
                doorInteractable.selectEntered.RemoveListener(OnDoorSelected);
                doorInteractable.enabled = false;
            }
        }

        private void Update()
        {
            if (doorLeafPivot == null)
            {
                return;
            }

            float blend = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);
            doorLeafPivot.localRotation =
                Quaternion.Slerp(doorLeafPivot.localRotation, targetRotation, blend);
        }

        private void OnEmergencyStateChanged(EmergencyState state)
        {
            bool canOpen = state == EmergencyState.EvacuationActive;

            if (doorInteractable != null)
            {
                doorInteractable.enabled = canOpen;
            }

            SetIndicatorColor(canOpen ? readyIndicatorColor : lockedIndicatorColor);

            if (state == EmergencyState.Inactive)
            {
                ResetDoor();
            }
        }

        private void OnDoorSelected(SelectEnterEventArgs args)
        {
            if (emergencyStateManager == null ||
                emergencyStateManager.CurrentState != EmergencyState.EvacuationActive ||
                doorLeafPivot == null ||
                isOpen)
            {
                return;
            }

            targetRotation = closedRotation * Quaternion.Euler(0f, -openAngle, 0f);
            isOpen = true;
        }

        private void ResetDoor()
        {
            isOpen = false;
            targetRotation = closedRotation;

            if (doorLeafPivot != null)
            {
                doorLeafPivot.localRotation = closedRotation;
            }
        }

        private void SetIndicatorColor(Color color)
        {
            if (accessIndicator == null)
            {
                return;
            }

            accessIndicator.GetPropertyBlock(indicatorProperties);
            indicatorProperties.SetColor(BaseColorId, color);
            indicatorProperties.SetColor(ColorId, color);
            accessIndicator.SetPropertyBlock(indicatorProperties);
        }
    }

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
