using UnityEngine;
using UnityEngine.XR;
using Skoven.Network;

namespace Skoven.Voice
{
    /// <summary>
    /// Maps XR controller input to voice capture.
    ///
    /// Push-to-talk: right grip button held → capture.
    /// Always-listening: VAD-driven (controlled by companion app / future update).
    ///
    /// Sends a typed QuestCommand message to the companion app for workspace
    /// actions (stop, save, etc.) that don't go through the voice pipeline.
    /// </summary>
    public class VoiceInputController : MonoBehaviour
    {
        [Header("Dependencies")]
        public VoiceCapture voiceCapture;
        public WebSocketClient wsClient;

        [Header("Settings")]
        public VoiceMode voiceMode = VoiceMode.PushToTalk;

        public enum VoiceMode { PushToTalk, AlwaysListening }

        private bool _gripWasPressed;
        private InputDevice _rightController;

        private void Start()
        {
            // Try to find the right controller device
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                devices
            );
            if (devices.Count > 0)
                _rightController = devices[0];

            InputDevices.deviceConnected += OnDeviceConnected;
        }

        private void OnDestroy()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
        }

        private void OnDeviceConnected(InputDevice device)
        {
            if ((device.characteristics & InputDeviceCharacteristics.Right) != 0 &&
                (device.characteristics & InputDeviceCharacteristics.Controller) != 0)
            {
                _rightController = device;
            }
        }

        private void Update()
        {
            if (voiceMode != VoiceMode.PushToTalk) return;

            bool gripPressed = false;
            if (_rightController.isValid)
                _rightController.TryGetFeatureValue(CommonUsages.gripButton, out gripPressed);

            if (gripPressed && !_gripWasPressed)
            {
                voiceCapture.StartCapture();
            }
            else if (!gripPressed && _gripWasPressed)
            {
                voiceCapture.StopCapture();
            }

            _gripWasPressed = gripPressed;
        }

        // ── Hard stop (called from hand menu "Stop" button) ────────────────
        public void SendStop()
        {
            wsClient.Send("{\"type\":\"stop\"}");
        }

        /// <summary>
        /// Send an approval decision. Called from UI approval buttons.
        /// </summary>
        public void SendApproval(bool approved)
        {
            string json = approved
                ? "{\"type\":\"approval\",\"decision\":\"approve\"}"
                : "{\"type\":\"approval\",\"decision\":\"reject\"}";
            wsClient.Send(json);
        }
    }
}
