using UnityEngine;
using UnityEngine.XR;

namespace Flowstate.UI
{
    /// <summary>
    /// Hand menu — a radial panel attached to the non-dominant (left) hand.
    /// Activates when the developer turns their palm upward (wrist rotation check).
    ///
    /// The menu itself is built in the scene as a Canvas child of this GameObject.
    /// Sections (exposed as child GameObjects):
    ///   - PanelsSection    — show/hide individual panels
    ///   - WorkspaceSection — environment, layout reset
    ///   - SessionSection   — save, end session
    ///   - AgentSection     — stop, pause, resume
    ///
    /// Button presses are wired via the Inspector to WorkspaceCommandHandler
    /// and VoiceInputController.
    /// </summary>
    public class HandMenu : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Dot product threshold for palm-up detection (1 = straight up).")]
        [Range(0f, 1f)]
        public float palmUpThreshold = 0.7f;

        [Tooltip("How long (seconds) the palm must be up before menu opens.")]
        public float activationDelay = 0.3f;

        [Header("Menu Sections")]
        public GameObject panelsSection;
        public GameObject workspaceSection;
        public GameObject sessionSection;
        public GameObject agentSection;

        private bool _menuVisible;
        private float _palmUpTime;
        private InputDevice _leftController;
        private Transform _cameraTransform;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Start()
        {
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
                devices
            );
            if (devices.Count > 0) _leftController = devices[0];

            InputDevices.deviceConnected += OnDeviceConnected;

            var cam = Camera.main;
            _cameraTransform = cam != null ? cam.transform : transform;

            SetMenuVisible(false);
        }

        private void OnDestroy()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
        }

        private void OnDeviceConnected(InputDevice device)
        {
            if ((device.characteristics & InputDeviceCharacteristics.Left) != 0 &&
                (device.characteristics & InputDeviceCharacteristics.Controller) != 0)
                _leftController = device;
        }

        private void Update()
        {
            UpdatePalmDetection();
            if (_menuVisible) FaceCamera();
        }

        // ── Palm-up detection ──────────────────────────────────────────────

        private void UpdatePalmDetection()
        {
            bool palmUp = IsPalmUp();

            if (palmUp)
            {
                _palmUpTime += Time.deltaTime;
                if (_palmUpTime >= activationDelay && !_menuVisible)
                    SetMenuVisible(true);
            }
            else
            {
                _palmUpTime = 0f;
                if (_menuVisible)
                    SetMenuVisible(false);
            }
        }

        private bool IsPalmUp()
        {
            if (!_leftController.isValid) return false;

            if (_leftController.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
            {
                // Palm normal points "up" when the wrist is rotated so the palm faces the sky
                Vector3 palmNormal = rotation * Vector3.up;
                float dot = Vector3.Dot(palmNormal, Vector3.up);
                return dot > palmUpThreshold;
            }
            return false;
        }

        // ── Visibility ─────────────────────────────────────────────────────

        private void SetMenuVisible(bool visible)
        {
            _menuVisible = visible;
            if (panelsSection)    panelsSection.SetActive(visible);
            if (workspaceSection) workspaceSection.SetActive(visible);
            if (sessionSection)   sessionSection.SetActive(visible);
            if (agentSection)     agentSection.SetActive(visible);
        }

        public void ShowSection(string section)
        {
            // Deactivate all, then activate the named section
            SetMenuVisible(false);
            var target = section.ToLowerInvariant() switch
            {
                "panels"    => panelsSection,
                "workspace" => workspaceSection,
                "session"   => sessionSection,
                "agent"     => agentSection,
                _           => null
            };
            if (target != null) target.SetActive(true);
            _menuVisible = true;
        }

        // ── Billboard (face camera) ────────────────────────────────────────

        private void FaceCamera()
        {
            if (_cameraTransform == null) return;
            var dir = _cameraTransform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-dir);
        }
    }
}
