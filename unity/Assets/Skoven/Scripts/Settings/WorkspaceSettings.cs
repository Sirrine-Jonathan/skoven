using UnityEngine;
using Skoven.UI.Panels;

namespace Skoven.Settings
{
    /// <summary>
    /// Persists and applies workspace-level settings:
    ///   - Companion app host address and port
    ///   - LLM provider and model (mirrored from companion app config)
    ///   - Voice mode
    ///   - Environment
    ///   - Font size
    ///   - Accessibility settings
    ///
    /// Settings are stored in PlayerPrefs.
    /// </summary>
    public class WorkspaceSettings : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────
        public static WorkspaceSettings Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // ── Settings properties ────────────────────────────────────────────

        public string HostAddress
        {
            get => PlayerPrefs.GetString("fs.host.address", "192.168.1.100");
            set { PlayerPrefs.SetString("fs.host.address", value); PlayerPrefs.Save(); }
        }

        public int HostPort
        {
            get => PlayerPrefs.GetInt("fs.host.port", 8765);
            set { PlayerPrefs.SetInt("fs.host.port", value); PlayerPrefs.Save(); }
        }

        public string DeviceId
        {
            get
            {
                var id = PlayerPrefs.GetString("fs.device.id", "");
                if (string.IsNullOrEmpty(id))
                {
                    id = SystemInfo.deviceUniqueIdentifier;
                    PlayerPrefs.SetString("fs.device.id", id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        public VoiceMode VoiceMode
        {
            get => (VoiceMode)PlayerPrefs.GetInt("fs.voice.mode", (int)VoiceMode.PushToTalk);
            set { PlayerPrefs.SetInt("fs.voice.mode", (int)value); PlayerPrefs.Save(); }
        }

        public WorkspaceEnvironment Environment
        {
            get => (WorkspaceEnvironment)PlayerPrefs.GetInt("fs.env", (int)WorkspaceEnvironment.Void);
            set { PlayerPrefs.SetInt("fs.env", (int)value); Apply(); PlayerPrefs.Save(); }
        }

        public FontSizePreset FontSize
        {
            get => (FontSizePreset)PlayerPrefs.GetInt("fs.fontsize", (int)FontSizePreset.Medium);
            set { PlayerPrefs.SetInt("fs.fontsize", (int)value); Apply(); PlayerPrefs.Save(); }
        }

        public bool DyslexiaFontEnabled
        {
            get => PlayerPrefs.GetInt("fs.font.dyslexia", 0) == 1;
            set { PlayerPrefs.SetInt("fs.font.dyslexia", value ? 1 : 0); Apply(); PlayerPrefs.Save(); }
        }

        public bool CaptionsEnabled
        {
            get => PlayerPrefs.GetInt("fs.captions", 1) == 1;
            // Captions cannot be fully disabled — this is a floor, not a toggle
            set { PlayerPrefs.SetInt("fs.captions", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ── Environment objects (assign in Inspector) ──────────────────────

        public GameObject voidEnvironment;
        public GameObject studioEnvironment;
        public GameObject exteriorEnvironment;

        // ── Font references ────────────────────────────────────────────────
        public TMPro.TMP_FontAsset codeFont;         // JetBrains Mono
        public TMPro.TMP_FontAsset dyslexiaFont;     // OpenDyslexic

        // ── Load / Apply ───────────────────────────────────────────────────

        private void Load()
        {
            Apply();
        }

        public void Apply()
        {
            ApplyEnvironment();
            ApplyFontSize();
        }

        private void ApplyEnvironment()
        {
            if (voidEnvironment)     voidEnvironment.SetActive(Environment == WorkspaceEnvironment.Void);
            if (studioEnvironment)   studioEnvironment.SetActive(Environment == WorkspaceEnvironment.Studio);
            if (exteriorEnvironment) exteriorEnvironment.SetActive(Environment == WorkspaceEnvironment.Exterior);
        }

        private void ApplyFontSize()
        {
            float size = FontSize switch
            {
                FontSizePreset.Small      => 14f,
                FontSizePreset.Large      => 22f,
                FontSizePreset.ExtraLarge => 28f,
                _                         => 18f // Medium default
            };

            // Apply to all TMP text elements in the scene that have the
            // [PanelText] tag component. Simplified: broadcast to all FloatingPanels.
            var panels = FindObjectsByType<FloatingPanel>(FindObjectsSortMode.None);
            foreach (var panel in panels)
            {
                var texts = panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
                foreach (var t in texts)
                {
                    // Only override code/content text, not labels (which have fixed sizes)
                    bool isContent = false;
                    try
                    {
                        isContent = t.CompareTag("PanelContentText");
                    }
                    catch
                    {
                        isContent = false;
                    }

                    if (isContent)
                        t.fontSize = size;

                    if (DyslexiaFontEnabled && dyslexiaFont != null)
                        t.font = dyslexiaFont;
                    else if (!DyslexiaFontEnabled && codeFont != null)
                        t.font = codeFont;
                }
            }
        }
    }

    public enum VoiceMode { PushToTalk = 0, AlwaysListening = 1 }
    public enum WorkspaceEnvironment { Void = 0, Studio = 1, Exterior = 2 }
    public enum FontSizePreset { Small = 0, Medium = 1, Large = 2, ExtraLarge = 3 }
}
