using UnityEngine;

namespace RESQ
{
    [RequireComponent(typeof(Collider))]
    public sealed class EvacuationWaypoint : MonoBehaviour
    {
        [SerializeField, Min(0)] private int waypointIndex;
        [SerializeField] private EvacuationManager evacuationManager;
        [SerializeField] private Collider expectedPlayerCollider;

        public int WaypointIndex => waypointIndex;

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && other == expectedPlayerCollider && evacuationManager != null)
            {
                evacuationManager.TryReachWaypoint(waypointIndex, other);
            }
        }
    }
}
