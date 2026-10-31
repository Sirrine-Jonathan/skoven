using UnityEngine;
using Flowstate.Network;
using Flowstate.Settings;

namespace Flowstate.UI
{
    /// <summary>
    /// Routes workspace and session commands received from the companion app
    /// to the appropriate local systems (PanelManager, WorkspaceSettings, etc.).
    ///
    /// These are commands the companion app reflects back after parsing a voice
    /// command of type "workspace" or "session".
    /// </summary>
    public class WorkspaceCommandHandler : MonoBehaviour
    {
        [Header("Dependencies")]
        public PanelManager panelManager;
        public WebSocketClient wsClient;

        private void Awake()
        {
            if (panelManager == null)
                panelManager = FindFirstObjectByType<PanelManager>();
        }

        // ── Called by MessageRouter events ────────────────────────────────

        /// Handle a session_state message (e.g., on save or reconnect)
        public void OnSessionState(SessionStateMessage msg)
        {
            Debug.Log($"[Flowstate] Session state: {msg.projectName} / {msg.branch}");
        }

        // ── Called directly from hand menu buttons ────────────────────────

        public void ShowPanel(string name) => panelManager?.ShowPanel(name);
        public void HidePanel(string name) => panelManager?.HidePanel(name);
        public void TogglePanel(string name) => panelManager?.TogglePanel(name);
        public void ResetLayout() => panelManager?.ResetAllLayouts();
        public void SaveLayout() => panelManager?.SaveAllLayouts();

        public void SetEnvironmentVoid()   => SetEnvironment(WorkspaceEnvironment.Void);
        public void SetEnvironmentStudio() => SetEnvironment(WorkspaceEnvironment.Studio);
        public void SetEnvironmentExterior() => SetEnvironment(WorkspaceEnvironment.Exterior);

        private void SetEnvironment(WorkspaceEnvironment env)
        {
            if (WorkspaceSettings.Instance != null)
                WorkspaceSettings.Instance.Environment = env;
        }

        public void SaveSession()
        {
            wsClient?.Send("{\"type\":\"command\",\"action\":\"save_session\",\"payload\":{}}");
        }

        public void EndSession()
        {
            SaveSession();
            panelManager?.SaveAllLayouts();
            Debug.Log("[Flowstate] Session ended.");
            // In production: return to the home environment or quit
        }
    }
}
