using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Skoven.Network;
using Skoven.Settings;

namespace Skoven.Settings
{
    /// <summary>
    /// One-time setup wizard shown on first launch.
    ///
    /// Steps:
    ///   1. Enter companion app host IP address
    ///   2. Confirm connection (handshake result)
    ///   3. Select voice mode (push-to-talk recommended)
    ///
    /// Shown again if the connection fails on startup or user resets from settings.
    /// After completion, WorkspaceSettings.IsSetupComplete is true and the wizard
    /// is not shown again.
    /// </summary>
    public class SetupWizard : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject wizardRoot;
        public TMP_InputField ipAddressInput;
        public TMP_InputField portInput;
        public Button connectButton;
        public TextMeshProUGUI statusText;
        public GameObject voiceModeStep;
        public Toggle pushToTalkToggle;
        public Toggle alwaysListeningToggle;
        public Button finishButton;

        [Header("Dependencies")]
        public WebSocketClient wsClient;

        private int _step;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Start()
        {
            bool setupComplete = PlayerPrefs.GetInt("fs.setup.complete", 0) == 1;

            if (setupComplete)
            {
                wizardRoot.SetActive(false);
                return;
            }

            ShowWizard();
        }

        // ── Wizard flow ────────────────────────────────────────────────────

        public void ShowWizard()
        {
            wizardRoot.SetActive(true);
            _step = 1;

            if (ipAddressInput != null)
                ipAddressInput.text = WorkspaceSettings.Instance?.HostAddress ?? "192.168.1.100";
            if (portInput != null)
                portInput.text = (WorkspaceSettings.Instance?.HostPort ?? 8765).ToString();

            if (voiceModeStep != null)
                voiceModeStep.SetActive(false);

            SetStatus("Enter the IP address of your development machine.");
        }

        /// Called by the Connect button
        public void OnConnectPressed()
        {
            if (wsClient == null || WorkspaceSettings.Instance == null) return;

            var ip = ipAddressInput != null ? ipAddressInput.text.Trim() : "";
            if (string.IsNullOrEmpty(ip)) { SetStatus("Please enter an IP address."); return; }

            int port = 8765;
            if (portInput != null) int.TryParse(portInput.text, out port);

            WorkspaceSettings.Instance.HostAddress = ip;
            WorkspaceSettings.Instance.HostPort = port;

            wsClient.hostAddress = ip;
            wsClient.port = port;

            SetStatus("Connecting...");
            connectButton.interactable = false;

            wsClient.OnConnected += OnConnected;
            wsClient.OnDisconnected += OnDisconnected;

            _ = wsClient.ConnectAsync();
        }

        private void OnConnected()
        {
            wsClient.OnConnected -= OnConnected;
            wsClient.OnDisconnected -= OnDisconnected;

            SetStatus("Connected! Choose your voice mode.");
            if (voiceModeStep != null) voiceModeStep.SetActive(true);
            connectButton.interactable = true;
            _step = 2;
        }

        private void OnDisconnected()
        {
            if (_step == 1)
            {
                wsClient.OnConnected -= OnConnected;
                wsClient.OnDisconnected -= OnDisconnected;
                SetStatus("Connection failed. Check the IP address and that the companion app is running.");
                connectButton.interactable = true;
            }
        }

        /// Called by the Finish button
        public void OnFinishPressed()
        {
            if (WorkspaceSettings.Instance != null)
            {
                WorkspaceSettings.Instance.VoiceMode =
                    pushToTalkToggle != null && pushToTalkToggle.isOn
                        ? VoiceMode.PushToTalk
                        : VoiceMode.AlwaysListening;
            }

            PlayerPrefs.SetInt("fs.setup.complete", 1);
            PlayerPrefs.Save();

            wizardRoot.SetActive(false);
        }

        /// Called from the Settings panel to re-run setup
        public void Reset()
        {
            PlayerPrefs.SetInt("fs.setup.complete", 0);
            PlayerPrefs.Save();
            ShowWizard();
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }
    }
}
