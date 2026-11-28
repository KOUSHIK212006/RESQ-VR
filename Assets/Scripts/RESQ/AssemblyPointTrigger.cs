using UnityEngine;

namespace RESQ
{
    [RequireComponent(typeof(Collider))]
    public sealed class AssemblyPointTrigger : MonoBehaviour
    {
        [SerializeField] private EmergencyStateManager emergencyStateManager;
        [SerializeField] private EvacuationManager evacuationManager;
        [SerializeField] private Collider expectedPlayerCollider;

        private bool completed;

        private void OnEnable()
        {
            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.AddListener(OnEmergencyStateChanged);
            }
        }

        private void OnDisable()
        {
            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.RemoveListener(OnEmergencyStateChanged);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (completed ||
                other == null ||
                other != expectedPlayerCollider ||
                evacuationManager == null)
            {
                return;
            }

            completed = evacuationManager.TryCompleteAtAssembly(other);
        }

        private void OnEmergencyStateChanged(EmergencyState state)
        {
            if (state == EmergencyState.Inactive)
            {
                completed = false;
            }
        }
    }
}
