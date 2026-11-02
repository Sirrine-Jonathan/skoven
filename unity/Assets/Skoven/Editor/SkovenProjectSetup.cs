#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using Skoven.Agent;
using Skoven.Network;
using Skoven.Settings;
using Skoven.UI;
using Skoven.UI.Panels;
using Skoven.Voice;
using Skoven.XR;

namespace Skoven.Editor
{
    /// <summary>
    /// Programmatic Scene Builder and Project Setup for Skoven.
    /// Constructs SkovenMain.unity hierarchy, XR origin rig, core WebSocket/voice/agent
    /// systems, and floating world-space panels.
    /// </summary>
    public static class SkovenProjectSetup
    {
        public const string SceneFolder = "Assets/Skoven/Scenes";
        public const string ScenePath = SceneFolder + "/SkovenMain.unity";

        [MenuItem("Skoven/Import TMP Resources")]
        public static void ImportTMPResources()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            {
                Debug.Log("[TMP] Importing TMP Essential Resources headlessly...");
                string[] packages = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                if (packages.Length > 0)
                {
                    AssetDatabase.ImportPackage(packages[0], false);
                    Debug.Log($"[TMP] Successfully imported {packages[0]}");
                }
            }
        }

        [MenuItem("Skoven/Configure OpenXR")]
        public static void ConfigureOpenXR()
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings != null)
            {
                foreach (var feature in settings.GetFeatures())
                {
                    string typeName = feature.GetType().Name;
                    if (typeName.Contains("MetaQuestFeature") ||
                        typeName.Contains("MetaXRFeature") ||
                        typeName.Contains("TouchPlus") ||
                        typeName.Contains("TouchPro") ||
                        typeName.Contains("OculusTouch") ||
                        typeName.Contains("HandInteraction") ||
                        typeName.Contains("HandTracking"))
                    {
                        feature.enabled = true;
                        Debug.Log($"[OpenXR] Enabled feature: {typeName}");
                    }
                }
                EditorUtility.SetDirty(settings);
            }

            var config = OVRProjectConfig.CachedProjectConfig;
            if (config != null)
            {
                config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
                config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
                config.systemLoadingScreenBackground = OVRProjectConfig.SystemLoadingScreenBackground.ContextualPassthrough;
                OVRProjectConfig.CommitProjectConfig(config);
                Debug.Log("[OVRProjectConfig] Configured passthrough support as Required and hands+controllers with contextual passthrough loading");
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Skoven/Build Main Scene")]
        public static void BuildScene()
        {
            Debug.Log("=== Assembling Skoven Main Scene ===");

            ImportTMPResources();
            ConfigureOpenXR();

            if (!Directory.Exists(SceneFolder))
                Directory.CreateDirectory(SceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Clear skybox so camera clears to transparent alpha 0 for Passthrough (AR) underlay
            RenderSettings.skybox = null;

            // 1. Directional Sun Light
            GameObject lightGO = new GameObject("Directional Light");
            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(0.95f, 0.95f, 1.0f);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. XR Rig & Passthrough Camera (AR Mode)
            GameObject xrOrigin = new GameObject("XROrigin");
            xrOrigin.transform.position = Vector3.zero;

            // Dedicated OVRManager & Passthrough GameObject under XROrigin (matching Meta Best Practice & DeadZone)
            GameObject ovrGO = new GameObject("OVRManager");
            ovrGO.transform.SetParent(xrOrigin.transform);
            OVRManager ovr = ovrGO.AddComponent<OVRManager>();
            ovr.isInsightPassthroughEnabled = true;
            ovr.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            OVRPassthroughLayer ptLayer = ovrGO.AddComponent<OVRPassthroughLayer>();

            GameObject cameraOffset = new GameObject("CameraOffset");
            cameraOffset.transform.SetParent(xrOrigin.transform);
            cameraOffset.transform.localPosition = Vector3.zero;

            GameObject mainCamGO = new GameObject("Main Camera");
            mainCamGO.transform.SetParent(cameraOffset.transform);
            mainCamGO.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            mainCamGO.tag = "MainCamera";

            Camera mainCam = mainCamGO.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0f, 0f, 0f, 0f); // Pure transparent for passthrough underlay
            mainCam.nearClipPlane = 0.05f;
            mainCam.farClipPlane = 100f;
            mainCamGO.AddComponent<AudioListener>();

            var tpd = mainCamGO.AddComponent<TrackedPoseDriver>();
            tpd.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);

            // Left & Right Controllers with 6DOF tracking and laser pointer rays
            GameObject leftCtrlGO = CreateController(cameraOffset.transform, "LeftController", SkovenPointer.HandType.Left);
            GameObject rightCtrlGO = CreateController(cameraOffset.transform, "RightController", SkovenPointer.HandType.Right);

            // 3. Skoven Core Systems
            GameObject coreGO = new GameObject("Skoven Core");
            WebSocketClient wsClient = coreGO.AddComponent<WebSocketClient>();
            MessageRouter msgRouter = coreGO.AddComponent<MessageRouter>();
            msgRouter.wsClient = wsClient;

            VoiceCapture voiceCapture = coreGO.AddComponent<VoiceCapture>();
            VoiceInputController voiceCtrl = coreGO.AddComponent<VoiceInputController>();
            TtsPlayer ttsPlayer = coreGO.AddComponent<TtsPlayer>();
            WorkspaceSettings wsSettings = coreGO.AddComponent<WorkspaceSettings>();
            WorkspaceCommandHandler cmdHandler = coreGO.AddComponent<WorkspaceCommandHandler>();
            PanelManager panelManager = coreGO.AddComponent<PanelManager>();

            SkovenBootstrap bootstrap = coreGO.AddComponent<SkovenBootstrap>();
            bootstrap.wsClient = wsClient;
            bootstrap.voiceCapture = voiceCapture;

            // Load custom UI and monospace code fonts
            LoadFonts();

            // 4. Floating Workspace Panels (Centered at comfortable eye-level ~1.35m)
            GameObject panelsRoot = new GameObject("Panels");
            panelsRoot.transform.position = new Vector3(0f, 1.35f, 0f);

            ChatPanel chatPanel = CreateChatPanel(panelsRoot.transform, mainCam);
            DiffPanel diffPanel = CreateDiffPanel(panelsRoot.transform, mainCam);
            TaskBoardPanel taskBoardPanel = CreateTaskBoardPanel(panelsRoot.transform, mainCam);
            AgentLogPanel agentLogPanel = CreateAgentLogPanel(panelsRoot.transform, mainCam);
            TerminalPanel terminalPanel = CreateTerminalPanel(panelsRoot.transform, mainCam);

            // Assign panels to PanelManager and Bootstrap
            panelManager.chatPanel = chatPanel;
            panelManager.diffPanel = diffPanel;
            panelManager.taskBoardPanel = taskBoardPanel;
            panelManager.agentLogPanel = agentLogPanel;
            panelManager.terminalPanel = terminalPanel;

            bootstrap.chatPanel = chatPanel;

            // Wire MessageRouter events
            UnityEventTools.AddPersistentListener(msgRouter.onTranscriptAck, chatPanel.OnTranscriptAck);
            UnityEventTools.AddPersistentListener(msgRouter.onSpeech, chatPanel.OnSpeech);
            UnityEventTools.AddPersistentListener(msgRouter.onSpeech, ttsPlayer.OnSpeech);
            UnityEventTools.AddPersistentListener(msgRouter.onHandshakeAck, chatPanel.OnHandshakeAck);
            UnityEventTools.AddPersistentListener(msgRouter.onHandshakeAck, taskBoardPanel.OnHandshakeAck);
            UnityEventTools.AddPersistentListener(msgRouter.onAgentUpdate, chatPanel.OnAgentUpdate);
            UnityEventTools.AddPersistentListener(msgRouter.onAgentUpdate, agentLogPanel.OnAgentUpdate);
            UnityEventTools.AddPersistentListener(msgRouter.onAgentUpdate, taskBoardPanel.OnAgentUpdate);
            UnityEventTools.AddPersistentListener(msgRouter.onPlanReady, chatPanel.OnPlanReady);
            UnityEventTools.AddPersistentListener(msgRouter.onPlanReady, taskBoardPanel.OnPlanReady);
            UnityEventTools.AddPersistentListener(msgRouter.onDiffReady, diffPanel.OnDiffReady);
            UnityEventTools.AddPersistentListener(msgRouter.onVerifyResult, diffPanel.OnVerifyResult);
            UnityEventTools.AddPersistentListener(msgRouter.onVerifyResult, terminalPanel.OnVerifyResult);
            UnityEventTools.AddPersistentListener(msgRouter.onCommitReady, chatPanel.OnCommitReady);
            UnityEventTools.AddPersistentListener(msgRouter.onAgentComplete, chatPanel.OnAgentComplete);
            UnityEventTools.AddPersistentListener(msgRouter.onAgentComplete, agentLogPanel.OnAgentComplete);
            UnityEventTools.AddPersistentListener(msgRouter.onAgentError, chatPanel.OnAgentError);
            UnityEventTools.AddPersistentListener(msgRouter.onAgentError, agentLogPanel.OnAgentError);
            UnityEventTools.AddPersistentListener(msgRouter.onSessionState, cmdHandler.OnSessionState);

            // 5. Save Scene and configure EditorBuildSettings
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log($"=== Skoven Main Scene Successfully Built and Saved to {ScenePath} ===");
        }

        private static GameObject CreatePanelRoot<T>(Transform parent, string name, Vector3 localPos, Vector2 canvasSize, FloatingPanel.PanelId panelId, Camera worldCam = null, Vector3? localEuler = null) where T : FloatingPanel
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            Vector3 euler = localEuler ?? Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(euler);
            root.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (worldCam != null)
                canvas.worldCamera = worldCam;
            root.AddComponent<GraphicRaycaster>();

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = canvasSize;
            rect.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(canvasSize.x, canvasSize.y, 10f);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;

            var rb = root.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            T panel = root.AddComponent<T>();
            panel.panelId = panelId;
            panel.defaultLocalPosition = localPos;
            panel.defaultLocalEuler = euler;
            panel.defaultScale = rect.localScale;

            // Background panel graphic
            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(root.transform, false);
            bgGO.layer = LayerMask.NameToLayer("UI");
            Image bg = bgGO.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);
            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.localScale = Vector3.one;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.localPosition = Vector3.zero;

            return root;
        }

        private static TMP_FontAsset s_InterRegular;
        private static TMP_FontAsset s_InterBold;
        private static TMP_FontAsset s_JetBrainsMono;

        private static void LoadFonts()
        {
            s_InterRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Skoven/Fonts/Inter-Regular SDF.asset");
            s_InterBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Skoven/Fonts/Inter-Bold SDF.asset");
            s_JetBrainsMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Skoven/Fonts/JetBrainsMono-Regular SDF.asset");
        }

        private static GameObject CreateController(Transform parent, string name, SkovenPointer.HandType hand)
        {
            GameObject ctrlGO = new GameObject(name);
            ctrlGO.transform.SetParent(parent, false);
            ctrlGO.transform.localPosition = (hand == SkovenPointer.HandType.Left) ? new Vector3(-0.25f, 1.25f, 0.35f) : new Vector3(0.25f, 1.25f, 0.35f);

            SkovenPointer pointer = ctrlGO.AddComponent<SkovenPointer>();
            pointer.hand = hand;

            // LineRenderer laser beam
            LineRenderer line = ctrlGO.AddComponent<LineRenderer>();
            line.startWidth = 0.005f;
            line.endWidth = 0.002f;
            line.useWorldSpace = true;
            line.positionCount = 2;

            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            lineMat.color = new Color(0.25f, 0.75f, 1.0f, 0.65f);
            line.material = lineMat;
            pointer.pointerLine = line;

            // Reticle sphere at ray endpoint
            GameObject reticleGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reticleGO.name = "Reticle";
            reticleGO.transform.SetParent(ctrlGO.transform, false);
            reticleGO.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
            Object.DestroyImmediate(reticleGO.GetComponent<Collider>());
            reticleGO.GetComponent<Renderer>().material = lineMat;
            reticleGO.SetActive(false);
            pointer.reticle = reticleGO.transform;

            return ctrlGO;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchoredPos, Color bgColor, out TextMeshProUGUI btnText, out Image btnImg)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            rect.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, -1f);

            btnImg = go.AddComponent<Image>();
            btnImg.color = bgColor;

            Button btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.25f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            // Box collider for laser pointer physics raycast
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 15f);

            btnText = CreateTMP(go.transform, "Label", label, 18, size, Vector2.zero, s_InterBold);
            btnText.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        private static ChatPanel CreateChatPanel(Transform parent, Camera worldCam)
        {
            GameObject root = CreatePanelRoot<ChatPanel>(parent, "ChatPanel", new Vector3(0f, 0f, 1.4f), new Vector2(800, 580), FloatingPanel.PanelId.Chat, worldCam, Vector3.zero);
            ChatPanel panel = root.GetComponent<ChatPanel>();

            // Header: Title & status
            panel.statusTextMesh = CreateTMP(root.transform, "StatusText", "<b>Skoven Orchestrator</b>  ·  <color=#88FF88>Online</color>", 20, new Vector2(640, 40), new Vector2(-40, 250), s_InterBold);

            // Voice Indicator Dot
            GameObject voiceInd = new GameObject("VoiceIndicator");
            voiceInd.transform.SetParent(root.transform, false);
            voiceInd.transform.localPosition = new Vector3(350, 250, 0);
            voiceInd.layer = LayerMask.NameToLayer("UI");
            Image indImg = voiceInd.AddComponent<Image>();
            indImg.color = new Color(0.2f, 0.9f, 0.4f, 1f);
            RectTransform indRect = voiceInd.GetComponent<RectTransform>();
            indRect.localScale = Vector3.one;
            indRect.sizeDelta = new Vector2(22, 22);
            panel.voiceIndicator = voiceInd;

            // Instructions banner
            panel.instructionsText = CreateTMP(root.transform, "Instructions", "<color=#70B5FF><b>Controls:</b></color> Hold <b>[Right Grip]</b> or click <b>[Talk]</b> to speak  ·  Hold <b>[Left Grip]</b> on panel to move  ·  <b>[Trigger]</b> click", 15, new Vector2(740, 32), new Vector2(0, 205), s_InterRegular);

            // Chat conversation stream
            panel.chatTextMesh = CreateTMP(root.transform, "ChatText", "Ready. Speak your request or connect companion.", 19, new Vector2(740, 300), new Vector2(0, 25), s_InterRegular, isContentText: true);

            // Talk Button
            Button talkBtn = CreateButton(root.transform, "TalkButton", "🎙️ <b>Push to Talk</b> (or Hold Right Grip)", new Vector2(480, 56), new Vector2(0, -185), new Color(0.18f, 0.52f, 0.88f, 0.95f), out panel.talkButtonText, out panel.talkButtonImage);
            panel.talkButton = talkBtn;

            // Approval Bar (hidden by default)
            GameObject approvalBar = new GameObject("ApprovalBar");
            approvalBar.transform.SetParent(root.transform, false);
            approvalBar.layer = LayerMask.NameToLayer("UI");
            RectTransform appRect = approvalBar.AddComponent<RectTransform>();
            appRect.localScale = Vector3.one;
            appRect.sizeDelta = new Vector2(740, 60);
            appRect.anchoredPosition = new Vector2(0, -245);

            panel.approvalPromptText = CreateTMP(approvalBar.transform, "PromptText", "Approve proposed action?", 18, new Vector2(360, 40), new Vector2(-170, 0), s_InterBold);
            panel.approveButton = CreateButton(approvalBar.transform, "ApproveBtn", "✓ Approve", new Vector2(160, 46), new Vector2(100, 0), new Color(0.2f, 0.75f, 0.35f, 0.95f), out _, out _);
            panel.rejectButton = CreateButton(approvalBar.transform, "RejectBtn", "✗ Reject", new Vector2(160, 46), new Vector2(275, 0), new Color(0.85f, 0.25f, 0.25f, 0.95f), out _, out _);

            approvalBar.SetActive(false);
            panel.approvalBar = approvalBar;

            return panel;
        }

        private static DiffPanel CreateDiffPanel(Transform parent, Camera worldCam)
        {
            // Angled inward at -25° yaw to face the user
            GameObject root = CreatePanelRoot<DiffPanel>(parent, "DiffPanel", new Vector3(-1.15f, 0f, 1.3f), new Vector2(900, 600), FloatingPanel.PanelId.Diff, worldCam, new Vector3(0f, -25f, 0f));
            DiffPanel panel = root.GetComponent<DiffPanel>();

            panel.summaryTextMesh = CreateTMP(root.transform, "SummaryText", "Diff Viewer - No active diffs", 20, new Vector2(840, 40), new Vector2(0, 250), s_InterBold);
            panel.diffTextMesh = CreateTMP(root.transform, "DiffText", "No diff loaded.", 18, new Vector2(840, 480), new Vector2(0, -30), s_JetBrainsMono, isContentText: true);

            return panel;
        }

        private static TaskBoardPanel CreateTaskBoardPanel(Transform parent, Camera worldCam)
        {
            GameObject root = CreatePanelRoot<TaskBoardPanel>(parent, "TaskBoardPanel", new Vector3(0f, 0.52f, 1.4f), new Vector2(800, 320), FloatingPanel.PanelId.TaskBoard, worldCam, Vector3.zero);
            TaskBoardPanel panel = root.GetComponent<TaskBoardPanel>();

            panel.taskTitleText = CreateTMP(root.transform, "TaskTitle", "Task Board", 24, new Vector2(740, 50), new Vector2(0, 110), s_InterBold);
            panel.currentStageText = CreateTMP(root.transform, "CurrentStage", "Stage: Idle", 20, new Vector2(740, 40), new Vector2(0, 50), s_InterRegular);
            panel.stepsText = CreateTMP(root.transform, "StepsText", "Awaiting task...", 18, new Vector2(740, 120), new Vector2(0, -50), s_InterRegular, isContentText: true);

            return panel;
        }

        private static AgentLogPanel CreateAgentLogPanel(Transform parent, Camera worldCam)
        {
            // Angled inward at +25° yaw to face the user
            GameObject root = CreatePanelRoot<AgentLogPanel>(parent, "AgentLogPanel", new Vector3(1.15f, 0f, 1.3f), new Vector2(800, 550), FloatingPanel.PanelId.AgentLog, worldCam, new Vector3(0f, 25f, 0f));
            AgentLogPanel panel = root.GetComponent<AgentLogPanel>();

            panel.logTextMesh = CreateTMP(root.transform, "LogText", "Agent Log - Waiting for activity...", 18, new Vector2(740, 480), new Vector2(0, 0), s_JetBrainsMono, isContentText: true);

            return panel;
        }

        private static TerminalPanel CreateTerminalPanel(Transform parent, Camera worldCam)
        {
            GameObject root = CreatePanelRoot<TerminalPanel>(parent, "TerminalPanel", new Vector3(0f, -0.52f, 1.4f), new Vector2(800, 320), FloatingPanel.PanelId.Terminal, worldCam, Vector3.zero);
            TerminalPanel panel = root.GetComponent<TerminalPanel>();

            panel.outputTextMesh = CreateTMP(root.transform, "OutputText", "Terminal: Idle", 18, new Vector2(740, 260), new Vector2(0, 0), s_JetBrainsMono, isContentText: true);

            return panel;
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, Vector2 size, Vector2 anchoredPos, TMP_FontAsset font = null, bool isContentText = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            if (isContentText)
            {
                go.tag = "PanelContentText";
            }

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;

            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.richText = true;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            rect.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);

            return tmp;
        }
    }
}
#endif
