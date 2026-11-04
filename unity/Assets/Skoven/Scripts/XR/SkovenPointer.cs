using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Skoven.UI.Panels;

namespace Skoven.XR
{
    /// <summary>
    /// Interactive 6DOF VR Ray Pointer for UI and Workspace Panel interaction on Meta Quest.
    /// Implements patterns from the portfolio meta-quest-controller-input skill:
    /// - Dual-path OpenXR device binding (characteristics + XRNode fallback)
    /// - Separation of tracking root from pointer nozzle emitter
    /// - Interruption recovery (focus, pause, connect, disconnect)
    /// - Edge-detected trigger/primary button click with procedural audio + haptics
    /// - Visual button hover state (scale bounce, color highlight, haptic tick)
    /// - Grip-to-move for floating workspace panels with layout persistence
    /// </summary>
    public class SkovenPointer : MonoBehaviour
    {
        public enum HandType { Left, Right }

        [Header("Hand Setup")]
        public HandType hand = HandType.Right;
        public float pointerPitchOffset = -25.0f; // Calibrated parallel to faceplate plane (-25 deg)
        public float maxRayDistance = 8.0f;
        public LayerMask raycastMask = ~0;

        [Header("Tracking Calibration")]
        public Vector3 rotationOffset = new Vector3(88f, 0f, 0f); // Calibrated OpenXR Grip Pose (+88 deg)

        [Header("Visual Components")]
        public LineRenderer lineRenderer;
        public Transform reticle;
        public Color normalBeamColor = new Color(0.2f, 0.7f, 1f, 0.6f);
        public Color hoverBeamColor = new Color(0.3f, 0.95f, 0.4f, 0.85f);

        [Header("State")]
        public bool isPointerActive = true;
        public bool IsPositionTracked { get; private set; }
        public bool IsDeviceValid => _xrDevice.isValid;

        private InputDevice _xrDevice;
        private bool _triggerWasPressed;
        private bool _gripWasPressed;
        private GameObject _hoveredObject;
        private AudioSource _audioSource;
        private MeshRenderer _reticleRenderer;
        private Material _beamMat;
        private float _lastBindCheck;

        // Panel grabbing
        private FloatingPanel _grabbedPanel;
        private float _grabDistance;
        private Quaternion _grabControllerRot;
        private Quaternion _grabPanelRot;

        private void Awake()
        {
            SetupAudio();
            SetupVisuals();
        }

        private void OnEnable()
        {
            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;
        }

        private void OnDisable()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
            InputDevices.deviceDisconnected -= OnDeviceDisconnected;
            ClearInteractionState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                _triggerWasPressed = false;
                _gripWasPressed = false;
                TryBindDevice();
            }
            else
            {
                ClearInteractionState();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                _triggerWasPressed = false;
                _gripWasPressed = false;
                TryBindDevice();
            }
            else
            {
                ClearInteractionState();
            }
        }

        private void OnDeviceConnected(InputDevice dev)
        {
            TryBindDevice();
        }

        private void OnDeviceDisconnected(InputDevice dev)
        {
            if (dev == _xrDevice)
            {
                _xrDevice = default;
                ClearInteractionState();
            }
        }

        private void ClearInteractionState()
        {
            IsPositionTracked = false;
            UpdateHover(null);
            if (_grabbedPanel != null)
            {
                _grabbedPanel.SaveLayout();
                _grabbedPanel = null;
            }
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (reticle != null) reticle.gameObject.SetActive(false);
        }

        private void SetupAudio()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.5f;
        }

        private void SetupVisuals()
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
                lineRenderer = gameObject.AddComponent<LineRenderer>();

            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = 0.005f;
            lineRenderer.endWidth = 0.002f;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;

            _beamMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default"));
            _beamMat.color = (hand == HandType.Left)
                ? new Color(0.25f, 0.85f, 0.95f, 0.65f) // Teal/Cyan for Left
                : new Color(0.28f, 0.55f, 1.0f, 0.65f);  // Blue for Right
            lineRenderer.material = _beamMat;

            if (reticle == null)
            {
                GameObject retObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                retObj.name = hand + "_Reticle";
                retObj.transform.localScale = Vector3.one * 0.018f;
                Destroy(retObj.GetComponent<Collider>());
                reticle = retObj.transform;
            }

            _reticleRenderer = reticle.GetComponent<MeshRenderer>();
            if (_reticleRenderer != null)
                _reticleRenderer.material = _beamMat;
        }

        private void Update()
        {
            UpdateDeviceBinding();
            UpdateControllerPose();
            ProcessRaycastAndInteraction();
        }

        private void UpdateDeviceBinding()
        {
            if (!_xrDevice.isValid)
            {
                TryBindDevice();
            }
            else if (!IsPositionTracked && Time.unscaledTime - _lastBindCheck > 0.5f)
            {
                _lastBindCheck = Time.unscaledTime;
                TryBindDevice();
            }
        }

        private void TryBindDevice()
        {
            var desiredChars = InputDeviceCharacteristics.Controller |
                (hand == HandType.Left ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);

            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(desiredChars, devices);

            if (devices.Count > 0 && devices[0].isValid)
            {
                _xrDevice = devices[0];
            }
            else
            {
                XRNode node = (hand == HandType.Left) ? XRNode.LeftHand : XRNode.RightHand;
                _xrDevice = InputDevices.GetDeviceAtXRNode(node);
            }

            if (_xrDevice.isValid)
            {
                Debug.Log($"[SkovenPointer] Bound {hand} controller: '{_xrDevice.name}'");
            }
        }

        private void UpdateControllerPose()
        {
            if (!_xrDevice.isValid)
            {
                IsPositionTracked = false;
                return;
            }

            if (_xrDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 devPos))
            {
                transform.localPosition = devPos;
                IsPositionTracked = true;
            }
            else
            {
                IsPositionTracked = false;
            }

            if (_xrDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion devRot))
            {
                // +88 degrees pitch aligns OpenXR grip coordinate frame with physical controller barrel
                transform.localRotation = devRot * Quaternion.Euler(rotationOffset);
            }
        }

        private void ProcessRaycastAndInteraction()
        {
            if (!isPointerActive || !IsPositionTracked)
            {
                if (lineRenderer != null) lineRenderer.enabled = false;
                if (reticle != null) reticle.gameObject.SetActive(false);
                UpdateHover(null);
                return;
            }

            // Ray shoots from the faceplate front rim parallel to top surface
            Quaternion faceRot = transform.rotation * Quaternion.Euler(pointerPitchOffset, 0f, 0f);
            Vector3 rayOrigin = transform.position + faceRot * new Vector3(0f, 0.004f, 0.031f);
            Vector3 rayDir = faceRot * Vector3.forward;

            // Handle active panel dragging
            bool gripPressed = ReadGripPressed();
            if (_grabbedPanel != null)
            {
                if (gripPressed)
                {
                    Vector3 targetPos = rayOrigin + rayDir * _grabDistance;
                    _grabbedPanel.transform.position = targetPos;

                    Quaternion rotDelta = transform.rotation * Quaternion.Inverse(_grabControllerRot);
                    _grabbedPanel.transform.rotation = rotDelta * _grabPanelRot;

                    if (lineRenderer != null)
                    {
                        lineRenderer.enabled = true;
                        lineRenderer.SetPosition(0, rayOrigin);
                        lineRenderer.SetPosition(1, targetPos);
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
                    _grabbedPanel.SaveLayout();
                    _grabbedPanel = null;
                    SendHapticImpulse(0.35f, 0.06f);
                }
            }

            if (lineRenderer != null) lineRenderer.enabled = true;

            Ray ray = new Ray(rayOrigin, rayDir);
            Vector3 hitPoint = rayOrigin + rayDir * maxRayDistance;
            GameObject hitObj = null;
            bool hitInteractive = false;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, raycastMask))
            {
                hitPoint = hit.point;
                hitObj = hit.collider.gameObject;
                Button btn = hitObj.GetComponentInParent<Button>();
                if (btn != null && btn.interactable)
                    hitInteractive = true;
            }

            UpdateHover(hitObj);

            // Update line & reticle
            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, rayOrigin);
                lineRenderer.SetPosition(1, hitPoint);
                Color targetCol = hitInteractive ? hoverBeamColor : normalBeamColor;
                _beamMat.color = targetCol;
            }

            if (reticle != null)
            {
                reticle.position = hitPoint;
                reticle.rotation = Quaternion.LookRotation(rayDir);
                reticle.gameObject.SetActive(true);
            }

            // Click handling (Trigger or Primary A/X button)
            bool triggerPressed = ReadTriggerPressed();
            if (triggerPressed && !_triggerWasPressed && hitInteractive)
            {
                ExecuteClick(hitObj);
            }
            _triggerWasPressed = triggerPressed;

            // Panel Grab handling (Left hand grab, or either hand when not clicking a button)
            bool canGrab = (hand == HandType.Left) || (hand == HandType.Right && !hitInteractive);
            if (canGrab && gripPressed && !_gripWasPressed && hitObj != null)
            {
                var panel = hitObj.GetComponentInParent<FloatingPanel>();
                if (panel != null)
                {
                    _grabbedPanel = panel;
                    _grabDistance = Vector3.Distance(rayOrigin, hit.point);
                    _grabControllerRot = transform.rotation;
                    _grabPanelRot = panel.transform.rotation;
                    SendHapticImpulse(0.45f, 0.08f);
                }
            }
            _gripWasPressed = gripPressed;
        }

        private void UpdateHover(GameObject newObject)
        {
            if (newObject != _hoveredObject)
            {
                if (_hoveredObject != null)
                {
                    Button prevBtn = _hoveredObject.GetComponentInParent<Button>();
                    if (prevBtn != null)
                    {
                        prevBtn.transform.localScale = Vector3.one;
                    }
                }

                _hoveredObject = newObject;

                if (_hoveredObject != null)
                {
                    Button newBtn = _hoveredObject.GetComponentInParent<Button>();
                    if (newBtn != null && newBtn.interactable)
                    {
                        newBtn.transform.localScale = Vector3.one * 1.035f;
                        SendHapticImpulse(0.2f, 0.04f);
                    }
                }
            }
        }

        private void ExecuteClick(GameObject target)
        {
            if (target == null) return;
            Button button = target.GetComponentInParent<Button>();
            if (button != null && button.interactable)
            {
                Debug.Log($"[SkovenPointer] {hand} clicked button: {button.name}");
                button.onClick.Invoke();
                SendHapticImpulse(0.85f, 0.12f);
                PlayClickSound();
            }
        }

        public bool ReadTriggerPressed()
        {
            if (_xrDevice.isValid)
            {
                if (_xrDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool btn) && btn) return true;
                if (_xrDevice.TryGetFeatureValue(CommonUsages.trigger, out float val) && val > 0.4f) return true;
                if (_xrDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool pBtn) && pBtn) return true;
            }
            return false;
        }

        public bool ReadGripPressed()
        {
            if (_xrDevice.isValid)
            {
                if (_xrDevice.TryGetFeatureValue(CommonUsages.gripButton, out bool btn) && btn) return true;
                if (_xrDevice.TryGetFeatureValue(CommonUsages.grip, out float val) && val > 0.45f) return true;
            }
            return false;
        }

        public void SendHapticImpulse(float amplitude, float duration)
        {
            if (_xrDevice.isValid && _xrDevice.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                _xrDevice.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
            }
        }

        private void PlayClickSound()
        {
            if (_audioSource != null)
            {
                _audioSource.PlayOneShot(GetClickClip());
            }
        }

        private static AudioClip s_CachedClickClip;
        private static AudioClip GetClickClip()
        {
            if (s_CachedClickClip != null) return s_CachedClickClip;
            int sampleRate = 48000;
            int clickSamples = (int)(sampleRate * 0.035f);
            float[] clickData = new float[clickSamples];
            for (int i = 0; i < clickSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 90f);
                clickData[i] = Mathf.Sin(2f * Mathf.PI * 1800f * t) * env * 0.65f;
            }
            s_CachedClickClip = AudioClip.Create("SkovenPointer_Click", clickSamples, 1, sampleRate, false);
            s_CachedClickClip.SetData(clickData, 0);
            return s_CachedClickClip;
        }
    }
}
