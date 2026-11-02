using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Skoven.UI.Panels;

namespace Skoven.XR
{
    /// <summary>
    /// 6DOF Controller Tracking, Laser Pointer, and Spatial UI/Panel Interactor for Meta Quest.
    /// Supports:
    /// - 6DOF controller pose tracking via InputDevices (Right & Left)
    /// - Pointer ray with visual line renderer and surface reticle
    /// - Trigger click on UI Buttons (e.g. Talk button, approval buttons)
    /// - Grip-to-move for floating panels (hold grip while pointing at a panel to reposition in AR)
    /// - Haptic pulses on button hover/click and panel grab
    /// </summary>
    public class SkovenPointer : MonoBehaviour
    {
        public enum HandType { Left, Right }

        [Header("Hand Setup")]
        public HandType hand = HandType.Right;
        public float pointerPitchOffset = -18f; // calibrated angle for natural pointer ray
        public float maxRayDistance = 6f;

        [Header("Visuals")]
        public LineRenderer pointerLine;
        public Transform reticle;

        private InputDevice _device;
        private float _lastBindTime;
        private bool _triggerWasPressed;
        private bool _gripWasPressed;
        private Button _hoveredButton;

        // Panel grabbing
        private FloatingPanel _grabbedPanel;
        private float _grabDistance;
        private Quaternion _grabControllerRot;
        private Quaternion _grabPanelRot;

        private void Awake()
        {
            if (pointerLine == null)
                pointerLine = GetComponent<LineRenderer>();
        }

        private void Start()
        {
            BindDevice();
        }

        private void Update()
        {
            if (!_device.isValid)
            {
                if (Time.time - _lastBindTime > 1.0f)
                {
                    _lastBindTime = Time.time;
                    BindDevice();
                }
                return;
            }

            UpdateTrackingPose();
            ProcessRayAndInteraction();
        }

        private void BindDevice()
        {
            var characteristics = InputDeviceCharacteristics.Controller |
                (hand == HandType.Left ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);

            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(characteristics, devices);
            if (devices.Count > 0)
            {
                _device = devices[0];
                Debug.Log($"[Skoven.XR] Bound {hand} controller: {_device.name}");
            }
        }

        private void UpdateTrackingPose()
        {
            if (_device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
                transform.localPosition = position;

            if (_device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                transform.localRotation = rotation;
        }

        private void ProcessRayAndInteraction()
        {
            Vector3 rayOrigin = transform.position;
            Vector3 rayDir = transform.rotation * Quaternion.Euler(pointerPitchOffset, 0f, 0f) * Vector3.forward;

            // Handle panel dragging if currently grabbing
            bool gripPressed = IsGripPressed();
            if (_grabbedPanel != null)
            {
                if (gripPressed)
                {
                    Vector3 targetPos = rayOrigin + rayDir * _grabDistance;
                    _grabbedPanel.transform.position = targetPos;

                    Quaternion rotDelta = transform.rotation * Quaternion.Inverse(_grabControllerRot);
                    _grabbedPanel.transform.rotation = rotDelta * _grabPanelRot;

                    if (pointerLine != null)
                    {
                        pointerLine.enabled = true;
                        pointerLine.SetPosition(0, rayOrigin);
                        pointerLine.SetPosition(1, targetPos);
                    }
                    if (reticle != null)
                    {
                        reticle.gameObject.SetActive(true);
                        reticle.position = targetPos;
                    }
                    return;
                }
                else
                {
                    // Released panel
                    _grabbedPanel.SaveLayout();
                    _grabbedPanel = null;
                    SendHaptic(0.3f, 0.05f);
                }
            }

            // Raycast against all scene colliders
            int layerMask = ~0;
            bool hitSomething = Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, maxRayDistance, layerMask);

            Vector3 endPoint = hitSomething ? hit.point : rayOrigin + rayDir * 2.5f;

            if (pointerLine != null)
            {
                pointerLine.enabled = true;
                pointerLine.SetPosition(0, rayOrigin);
                pointerLine.SetPosition(1, endPoint);
            }

            if (reticle != null)
            {
                reticle.gameObject.SetActive(hitSomething);
                if (hitSomething)
                {
                    reticle.position = hit.point;
                    reticle.rotation = Quaternion.LookRotation(hit.normal);
                }
            }

            if (!hitSomething)
            {
                _hoveredButton = null;
                _gripWasPressed = gripPressed;
                _triggerWasPressed = IsTriggerPressed();
                return;
            }

            // Check button hover
            var btn = hit.collider.GetComponentInParent<Button>();
            if (btn != null && btn != _hoveredButton)
            {
                _hoveredButton = btn;
                SendHaptic(0.15f, 0.02f);
            }
            else if (btn == null)
            {
                _hoveredButton = null;
            }

            // Check Trigger Click
            bool triggerPressed = IsTriggerPressed();
            if (triggerPressed && !_triggerWasPressed)
            {
                if (_hoveredButton != null && _hoveredButton.interactable)
                {
                    _hoveredButton.onClick.Invoke();
                    SendHaptic(0.5f, 0.06f);
                }
            }
            _triggerWasPressed = triggerPressed;

            // Check Grip to Grab Panel (Left hand grab, or either hand when not on button)
            bool canGrab = (hand == HandType.Left) || (hand == HandType.Right && _hoveredButton == null);
            if (canGrab && gripPressed && !_gripWasPressed)
            {
                var panel = hit.collider.GetComponentInParent<FloatingPanel>();
                if (panel != null)
                {
                    _grabbedPanel = panel;
                    _grabDistance = Vector3.Distance(rayOrigin, hit.point);
                    _grabControllerRot = transform.rotation;
                    _grabPanelRot = panel.transform.rotation;
                    SendHaptic(0.4f, 0.06f);
                }
            }
            _gripWasPressed = gripPressed;
        }

        private bool IsTriggerPressed()
        {
            if (_device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
                return true;
            if (_device.TryGetFeatureValue(CommonUsages.trigger, out float val) && val > 0.5f)
                return true;
            return false;
        }

        private bool IsGripPressed()
        {
            if (_device.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed) && pressed)
                return true;
            if (_device.TryGetFeatureValue(CommonUsages.grip, out float val) && val > 0.5f)
                return true;
            return false;
        }

        public void SendHaptic(float amplitude, float duration)
        {
            if (_device.isValid)
            {
                _device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
            }
        }
    }
}
