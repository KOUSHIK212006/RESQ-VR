using System.Text;
using TMPro;
using UnityEngine;

namespace RESQ
{
    public sealed class EvacuationStatusUI : MonoBehaviour
    {
        [SerializeField] private EmergencyStateManager emergencyStateManager;
        [SerializeField] private EvacuationManager evacuationManager;
        [SerializeField] private TMP_Text statusText;

        private readonly StringBuilder statusBuilder = new StringBuilder(256);

        private void OnEnable()
        {
            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.AddListener(OnStateChanged);
            }

            if (evacuationManager != null)
            {
                evacuationManager.ProgressChanged.AddListener(OnProgressChanged);
                evacuationManager.WaypointReached.AddListener(OnWaypointReached);
                evacuationManager.AssemblyPointReached.AddListener(OnAssemblyPointReached);
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (emergencyStateManager != null)
            {
                emergencyStateManager.StateChanged.RemoveListener(OnStateChanged);
            }

            if (evacuationManager != null)
            {
                evacuationManager.ProgressChanged.RemoveListener(OnProgressChanged);
                evacuationManager.WaypointReached.RemoveListener(OnWaypointReached);
                evacuationManager.AssemblyPointReached.RemoveListener(OnAssemblyPointReached);
            }
        }

        private void OnStateChanged(EmergencyState _)
        {
            Refresh();
        }

        private void OnProgressChanged(float _)
        {
            Refresh();
        }

        private void OnWaypointReached(int _)
        {
            Refresh();
        }

        private void OnAssemblyPointReached()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (statusText == null)
            {
                return;
            }

            EmergencyState state = emergencyStateManager != null
                ? emergencyStateManager.CurrentState
                : EmergencyState.Inactive;
            float progress = evacuationManager != null ? evacuationManager.Progress : 0f;
            string destination = evacuationManager != null
                ? evacuationManager.CurrentDestination
                : "Route unavailable";

            statusBuilder.Clear();
            statusBuilder.Append("<b>RESQ EMERGENCY</b>\n");
            statusBuilder.Append("Status: ").Append(FormatState(state)).Append('\n');

            if (state == EmergencyState.EvacuationActive)
            {
                statusBuilder.Append("Instruction: Open the marked door and follow the route.\n");
            }
            else if (state == EmergencyState.Completed)
            {
                statusBuilder.Append("<color=#8CFF9B>Assembly point reached — evacuation complete.</color>\n");
            }
            else if (state == EmergencyState.Failed)
            {
                statusBuilder.Append("Evacuation failed. Follow instructor guidance.\n");
            }
            else if (state == EmergencyState.AlarmActive)
            {
                statusBuilder.Append("Alarm active. Prepare to evacuate.\n");
            }
            else
            {
                statusBuilder.Append("Awaiting emergency activation.\n");
            }

            statusBuilder.Append("Destination: ").Append(destination).Append('\n');
            statusBuilder.Append("Evacuation progress: ")
                .Append(Mathf.RoundToInt(progress * 100f))
                .Append("%\n");
            statusBuilder.Append("<size=70%>Temporary training markers only; this environment has no physical stairs or basement route.</size>");
            statusText.text = statusBuilder.ToString();
        }

        private static string FormatState(EmergencyState state)
        {
            switch (state)
            {
                case EmergencyState.AlarmActive:
                    return "Alarm Active";
                case EmergencyState.EvacuationActive:
                    return "Evacuation Active";
                case EmergencyState.Completed:
                    return "Completed";
                case EmergencyState.Failed:
                    return "Failed";
                default:
                    return "Inactive";
            }
        }
    }
}
