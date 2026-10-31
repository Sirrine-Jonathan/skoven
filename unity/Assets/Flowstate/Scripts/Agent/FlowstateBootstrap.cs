using UnityEngine;
using Flowstate.Network;
using Flowstate.Settings;

namespace Flowstate.Agent
{
    /// <summary>
    /// Main bootstrap — sends the handshake after the WebSocket connects
    /// and wires up the voice capture indicator to the chat panel.
    ///
    /// Attach to a root GameObject in the main scene alongside
    /// WebSocketClient, MessageRouter, VoiceCapture, and VoiceInputController.
    /// </summary>
    public class FlowstateBootstrap : MonoBehaviour
    {
        [Header("Dependencies")]
        public WebSocketClient wsClient;
        public Voice.VoiceCapture voiceCapture;
        public UI.Panels.ChatPanel chatPanel;

        private void Awake()
        {
            if (wsClient == null)
                wsClient = FindFirstObjectByType<WebSocketClient>();
            if (voiceCapture == null)
                voiceCapture = FindFirstObjectByType<Voice.VoiceCapture>();
            if (chatPanel == null)
                chatPanel = FindFirstObjectByType<UI.Panels.ChatPanel>();
        }

        private void Start()
        {
            wsClient.OnConnected += OnConnected;

            // Wire voice indicator to chat panel
            if (voiceCapture != null && chatPanel != null)
            {
                // Poll voice state each frame (simple; replace with event if preferred)
                InvokeRepeating(nameof(UpdateVoiceIndicator), 0f, 0.1f);
            }
        }

        private void OnConnected()
        {
            var settings = WorkspaceSettings.Instance;

            // Send handshake immediately on connection
            string projectPath = "";
#if UNITY_EDITOR
            // In editor, use a test project path if set in PlayerPrefs
            projectPath = PlayerPrefs.GetString("fs.test.projectPath", "");
#endif

            string handshake = projectPath.Length > 0
                ? $"{{\"type\":\"handshake\",\"deviceId\":\"{settings.DeviceId}\",\"projectPath\":\"{EscapeJson(projectPath)}\"}}"
                : $"{{\"type\":\"handshake\",\"deviceId\":\"{settings.DeviceId}\"}}";

            wsClient.Send(handshake);
            Debug.Log("[Flowstate] Handshake sent.");
        }

        private void UpdateVoiceIndicator()
        {
            chatPanel?.SetVoiceActive(voiceCapture.IsRecording);
        }

        private void OnDestroy()
        {
            wsClient.OnConnected -= OnConnected;
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
