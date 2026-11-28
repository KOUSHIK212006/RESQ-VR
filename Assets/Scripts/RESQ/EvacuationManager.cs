using UnityEngine;
using UnityEngine.Events;

namespace RESQ
{
    public sealed class EvacuationManager : MonoBehaviour
    {
        [SerializeField] private EmergencyStateManager emergencyStateManager;
        [SerializeField] private Collider expectedPlayerCollider;
        [SerializeField] private Transform[] orderedWaypoints;
        [SerializeField] private Transform assemblyPoint;

        [SerializeField] private UnityEvent evacuationStarted = new UnityEvent();
        [SerializeField] private UnityEvent<int> waypointReached = new UnityEvent<int>();
        [SerializeField] private UnityEvent<float> progressChanged = new UnityEvent<float>();
        [SerializeField] private UnityEvent assemblyPointReached = new UnityEvent();

        private int reachedWaypointCount;
        private bool isEvacuationActive;
        private bool isComplete;

        public int ReachedWaypointCount => reachedWaypointCount;
        public int WaypointCount => orderedWaypoints != null ? orderedWaypoints.Length : 0;
        public bool IsComplete => isComplete;
        public float Progress
        {
            get
            {
                int totalSteps = WaypointCount + 1;
                return totalSteps == 0
                    ? 0f
                    : Mathf.Clamp01((reachedWaypointCount + (isComplete ? 1f : 0f)) / totalSteps);
            }
        }

        public string CurrentDestination
        {
            get
            {
                if (isComplete)
                {
                    return "Assembly point reached";
                }

                if (orderedWaypoints != null && reachedWaypointCount < orderedWaypoints.Length)
                {
                    Transform waypoint = orderedWaypoints[reachedWaypointCount];
                    return waypoint != null ? waypoint.name : "Route waypoint unavailable";
                }

                return assemblyPoint != null ? assemblyPoint.name : "Assembly point unavailable";
            }
        }

        public UnityEvent EvacuationStarted => evacuationStarted;
        public UnityEvent<int> WaypointReached => waypointReached;
        public UnityEvent<float> ProgressChanged => progressChanged;
        public UnityEvent AssemblyPointReached => assemblyPointReached;

        private void OnEnable()
        {
            if (emergencyStateManager == null)
            {
                return;
            }

            emergencyStateManager.StateChanged.AddListener(OnEmergencyStateChanged);

            if (emergencyStateManager.CurrentState == EmergencyState.EvacuationActive)
            {
                BeginEvacuation();
            }
        }

        private void OnDisable()
        {
            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.RemoveListener(OnEmergencyStateChanged);
            }
        }

        public bool TryReachWaypoint(int waypointIndex, Collider playerCollider)
        {
            if (!isEvacuationActive ||
                isComplete ||
                emergencyStateManager == null ||
                emergencyStateManager.CurrentState != EmergencyState.EvacuationActive ||
                playerCollider == null ||
                playerCollider != expectedPlayerCollider ||
                waypointIndex != reachedWaypointCount ||
                orderedWaypoints == null ||
                waypointIndex < 0 ||
                waypointIndex >= orderedWaypoints.Length)
            {
                return false;
            }

            reachedWaypointCount++;
            waypointReached?.Invoke(waypointIndex);
            progressChanged?.Invoke(Progress);
            return true;
        }

        public bool TryCompleteAtAssembly(Collider playerCollider)
        {
            if (!isEvacuationActive ||
                isComplete ||
                emergencyStateManager == null ||
                emergencyStateManager.CurrentState != EmergencyState.EvacuationActive ||
                playerCollider == null ||
                playerCollider != expectedPlayerCollider ||
                reachedWaypointCount != WaypointCount)
            {
                return false;
            }

            isComplete = true;
            isEvacuationActive = false;
            assemblyPointReached?.Invoke();
            progressChanged?.Invoke(Progress);
            emergencyStateManager.CompleteEvacuation();
            return true;
        }

        private void OnEmergencyStateChanged(EmergencyState state)
        {
            if (state == EmergencyState.EvacuationActive)
            {
                BeginEvacuation();
            }
            else if (state == EmergencyState.Inactive)
            {
                ResetProgress();
            }
            else
            {
                isEvacuationActive = false;
            }
        }

        private void BeginEvacuation()
        {
            reachedWaypointCount = 0;
            isComplete = false;
            isEvacuationActive = true;
            evacuationStarted?.Invoke();
            progressChanged?.Invoke(Progress);
        }

        private void ResetProgress()
        {
            reachedWaypointCount = 0;
            isComplete = false;
            isEvacuationActive = false;
            progressChanged?.Invoke(Progress);
        }
    }
}
