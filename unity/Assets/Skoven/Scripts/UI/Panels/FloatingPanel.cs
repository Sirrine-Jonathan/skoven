using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Skoven.UI.Panels
{
    /// <summary>
    /// Base class for all floating workspace panels.
    ///
    /// - Pinned to world space (no head-lock)
    /// - Grabbable and repositionable via XR Grab Interactable
    /// - Scalable via pinch gesture (handled by PanelScaler)
    /// - Individually dismissable and re-summoned
    /// - Layout position saved to PlayerPrefs
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class FloatingPanel : MonoBehaviour
    {
        [Header("Panel Identity")]
        public PanelId panelId;

        [Header("Default Transform")]
        public Vector3 defaultLocalPosition = new(0f, 0f, 1.5f);
        public Vector3 defaultLocalEuler = Vector3.zero;
        public Vector3 defaultScale = new(0.0015f, 0.0015f, 0.0015f);

        [Header("Constraints")]
        [Tooltip("Minimum distance from origin (prevents panels behind the player).")]
        public float minDistance = 0.4f;
        public float maxDistance = 3.0f;

        private Transform _origin; // XR camera rig origin
        private bool _visible = true;

        public enum PanelId { Chat, Diff, TaskBoard, AgentLog, Terminal }

        // ── Unity lifecycle ────────────────────────────────────────────────

        protected virtual void Awake()
        {
            // Ensure panel does not drop to the floor under gravity
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            // Find the XR rig origin
            var rig = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            _origin = rig != null ? rig.transform : transform.parent;
        }

        protected virtual void Start()
        {
            if (_origin == null)
            {
                var rig = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                _origin = rig != null ? rig.transform : transform.parent;
            }

            LoadLayout();
        }

        protected virtual void OnDestroy()
        {
            SaveLayout();
        }

        // ── Visibility ─────────────────────────────────────────────────────

        public bool IsVisible => _visible;

        public void Show()
        {
            _visible = true;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _visible = false;
            gameObject.SetActive(false);
        }

        public void Toggle() { if (_visible) Hide(); else Show(); }

        // ── Layout persistence ─────────────────────────────────────────────

        private string PositionKey => $"skoven.panel.v3.{panelId}.pos";
        private string RotationKey => $"skoven.panel.v3.{panelId}.rot";
        private string ScaleKey    => $"skoven.panel.v3.{panelId}.scale";
        private string VisibleKey  => $"skoven.panel.v3.{panelId}.visible";

        public void SaveLayout()
        {
            var p = transform.position;
            var r = transform.rotation;
            var s = transform.localScale;

            PlayerPrefs.SetFloat(PositionKey + "x", p.x);
            PlayerPrefs.SetFloat(PositionKey + "y", p.y);
            PlayerPrefs.SetFloat(PositionKey + "z", p.z);
            PlayerPrefs.SetFloat(RotationKey + "x", r.x);
            PlayerPrefs.SetFloat(RotationKey + "y", r.y);
            PlayerPrefs.SetFloat(RotationKey + "z", r.z);
            PlayerPrefs.SetFloat(RotationKey + "w", r.w);
            PlayerPrefs.SetFloat(ScaleKey + "x", s.x);
            PlayerPrefs.SetFloat(ScaleKey + "y", s.y);
            PlayerPrefs.SetFloat(ScaleKey + "z", s.z);
            PlayerPrefs.SetInt(VisibleKey, _visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void LoadLayout()
        {
            if (!PlayerPrefs.HasKey(PositionKey + "x"))
            {
                ResetToDefault();
                return;
            }

            var pos = new Vector3(
                PlayerPrefs.GetFloat(PositionKey + "x"),
                PlayerPrefs.GetFloat(PositionKey + "y"),
                PlayerPrefs.GetFloat(PositionKey + "z")
            );
            var rot = new Quaternion(
                PlayerPrefs.GetFloat(RotationKey + "x"),
                PlayerPrefs.GetFloat(RotationKey + "y"),
                PlayerPrefs.GetFloat(RotationKey + "z"),
                PlayerPrefs.GetFloat(RotationKey + "w")
            );
            var scale = new Vector3(
                PlayerPrefs.GetFloat(ScaleKey + "x"),
                PlayerPrefs.GetFloat(ScaleKey + "y"),
                PlayerPrefs.GetFloat(ScaleKey + "z")
            );

            // Safety check against zero, corrupted, or giant legacy scales
            if (scale.sqrMagnitude < 0.0000001f || scale.x > 0.05f)
            {
                scale = defaultScale;
            }

            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = scale;

            bool visible = PlayerPrefs.GetInt(VisibleKey, 1) == 1;
            if (visible) Show(); else Hide();
        }

        public void ResetToDefault()
        {
            if (transform.parent != null)
            {
                transform.localPosition = defaultLocalPosition;
                transform.localEulerAngles = defaultLocalEuler;
            }
            else if (_origin != null)
            {
                transform.position = _origin.TransformPoint(defaultLocalPosition);
                transform.rotation = _origin.rotation * Quaternion.Euler(defaultLocalEuler);
            }
            else
            {
                transform.localPosition = defaultLocalPosition;
                transform.localEulerAngles = defaultLocalEuler;
            }
            transform.localScale = defaultScale;
            Show();
        }
    }
}
