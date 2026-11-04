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

        private float _lastBindCheck;

        private void Start()
        {
            TryBindRightController();
            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;
        }

        private void OnDestroy()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
            InputDevices.deviceDisconnected -= OnDeviceDisconnected;
        }

        private void OnDeviceConnected(InputDevice device)
        {
            TryBindRightController();
        }

        private void OnDeviceDisconnected(InputDevice device)
        {
            if (device == _rightController)
            {
                _rightController = default;
            }
        }

        private void TryBindRightController()
        {
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                devices
            );
            if (devices.Count > 0 && devices[0].isValid)
            {
                _rightController = devices[0];
            }
            else
            {
                _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            }
        }

        private void Update()
        {
            if (!_rightController.isValid && Time.unscaledTime - _lastBindCheck > 0.5f)
            {
                _lastBindCheck = Time.unscaledTime;
                TryBindRightController();
            }

            if (voiceMode != VoiceMode.PushToTalk) return;

            bool gripPressed = ReadRightGrip();

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

        private bool ReadRightGrip()
        {
            if (!_rightController.isValid) return false;
            if (_rightController.TryGetFeatureValue(CommonUsages.gripButton, out bool btn) && btn) return true;
            if (_rightController.TryGetFeatureValue(CommonUsages.grip, out float val) && val > 0.45f) return true;
            return false;
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
