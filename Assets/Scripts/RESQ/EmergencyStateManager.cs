using UnityEngine;
using UnityEngine.Events;

namespace RESQ
{
    public enum EmergencyState
    {
        Inactive,
        AlarmActive,
        EvacuationActive,
        Completed,
        Failed
    }

    public sealed class EmergencyStateManager : MonoBehaviour
    {
        [SerializeField] private FireAlarmController alarmController;
        [SerializeField] private FireController fireController;
        [SerializeField, Min(0f)] private float emergencyTimeoutSeconds;

        [SerializeField] private UnityEvent<EmergencyState> stateChanged;
        [SerializeField] private UnityEvent emergencyStarted;
        [SerializeField] private UnityEvent evacuationStarted;
        [SerializeField] private UnityEvent evacuationCompleted;
        [SerializeField] private UnityEvent evacuationFailed;

        private EmergencyState currentState = EmergencyState.Inactive;
        private float elapsedSeconds;

        public EmergencyState CurrentState => currentState;
        public float ElapsedSeconds => elapsedSeconds;
        public float EmergencyTimeoutSeconds => emergencyTimeoutSeconds;

        public UnityEvent<EmergencyState> StateChanged => stateChanged;
        public UnityEvent EmergencyStarted => emergencyStarted;
        public UnityEvent EvacuationStarted => evacuationStarted;
        public UnityEvent EvacuationCompleted => evacuationCompleted;
        public UnityEvent EvacuationFailed => evacuationFailed;

        private void Update()
        {
            if (currentState is not (EmergencyState.AlarmActive or EmergencyState.EvacuationActive))
            {
                return;
            }

            elapsedSeconds += Time.deltaTime;

            if (emergencyTimeoutSeconds > 0f && elapsedSeconds >= emergencyTimeoutSeconds)
            {
                FailEvacuation();
            }
        }

        [ContextMenu("Start Emergency")]
        public void StartEmergency()
        {
            if (currentState != EmergencyState.Inactive)
            {
                return;
            }

            elapsedSeconds = 0f;

            if (alarmController != null)
            {
                alarmController.ActivateAlarm();
            }

            if (fireController != null)
            {
                fireController.ActivateFire();
            }

            SetState(EmergencyState.AlarmActive);
            emergencyStarted?.Invoke();
        }

        [ContextMenu("Begin Evacuation")]
        public void BeginEvacuation()
        {
            if (currentState != EmergencyState.AlarmActive)
            {
                return;
            }

            SetState(EmergencyState.EvacuationActive);
            evacuationStarted?.Invoke();
        }

        [ContextMenu("Complete Evacuation")]
        public void CompleteEvacuation()
        {
            if (currentState != EmergencyState.EvacuationActive)
            {
                return;
            }

            SetState(EmergencyState.Completed);
            evacuationCompleted?.Invoke();
        }

        [ContextMenu("Fail Evacuation")]
        public void FailEvacuation()
        {
            if (currentState is not (EmergencyState.AlarmActive or EmergencyState.EvacuationActive))
            {
                return;
            }

            SetState(EmergencyState.Failed);
            evacuationFailed?.Invoke();
        }

        [ContextMenu("Reset Emergency")]
        public void ResetEmergency()
        {
            elapsedSeconds = 0f;

            if (alarmController != null)
            {
                alarmController.ResetAlarm();
            }

            if (fireController != null)
            {
                fireController.ResetFire();
            }

            SetState(EmergencyState.Inactive);
        }

        private void SetState(EmergencyState newState)
        {
            if (currentState == newState)
            {
                return;
            }

            currentState = newState;
            stateChanged?.Invoke(currentState);
        }
    }
}
