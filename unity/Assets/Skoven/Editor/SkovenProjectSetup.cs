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

        [MenuItem("Skoven/Build Main Scene")]
        public static void BuildScene()
        {
            Debug.Log("=== Assembling Skoven Main Scene ===");

            if (!Directory.Exists(SceneFolder))
                Directory.CreateDirectory(SceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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

            OVRManager ovrManager = xrOrigin.AddComponent<OVRManager>();
            ovrManager.isInsightPassthroughEnabled = true;

            GameObject cameraOffset = new GameObject("CameraOffset");
            cameraOffset.transform.SetParent(xrOrigin.transform);
            cameraOffset.transform.localPosition = Vector3.zero;

            GameObject mainCamGO = new GameObject("Main Camera");
            mainCamGO.transform.SetParent(cameraOffset.transform);
            mainCamGO.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            mainCamGO.tag = "MainCamera";

            Camera mainCam = mainCamGO.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0f, 0f, 0f, 0f); // Pure transparent for passthrough (AR)
            mainCam.nearClipPlane = 0.05f;
            mainCam.farClipPlane = 100f;
            mainCamGO.AddComponent<AudioListener>();

            mainCamGO.AddComponent<OVRPassthroughLayer>();

            var tpd = mainCamGO.AddComponent<TrackedPoseDriver>();
            tpd.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);

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

            // 4. Floating Workspace Panels
            GameObject panelsRoot = new GameObject("Panels");
            panelsRoot.transform.position = Vector3.zero;

            ChatPanel chatPanel = CreateChatPanel(panelsRoot.transform);
            DiffPanel diffPanel = CreateDiffPanel(panelsRoot.transform);
            TaskBoardPanel taskBoardPanel = CreateTaskBoardPanel(panelsRoot.transform);
            AgentLogPanel agentLogPanel = CreateAgentLogPanel(panelsRoot.transform);
            TerminalPanel terminalPanel = CreateTerminalPanel(panelsRoot.transform);

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

        private static GameObject CreatePanelRoot<T>(Transform parent, string name, Vector3 localPos, Vector2 canvasSize, FloatingPanel.PanelId panelId) where T : FloatingPanel
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.localPosition = localPos;
            root.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = canvasSize;
            rect.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(canvasSize.x, canvasSize.y, 10f);

            root.AddComponent<XRGrabInteractable>();

            T panel = root.AddComponent<T>();
            panel.panelId = panelId;
            panel.defaultLocalPosition = localPos;

            // Background panel graphic
            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(root.transform);
            bgGO.layer = LayerMask.NameToLayer("UI");
            Image bg = bgGO.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.localPosition = Vector3.zero;

            return root;
        }

        private static ChatPanel CreateChatPanel(Transform parent)
        {
            GameObject root = CreatePanelRoot<ChatPanel>(parent, "ChatPanel", new Vector3(0f, 0f, 1.5f), new Vector2(800, 550), FloatingPanel.PanelId.Chat);
            ChatPanel panel = root.GetComponent<ChatPanel>();

            panel.chatTextMesh = CreateTMP(root.transform, "ChatText", "<b>Skoven Orchestrator</b>\nReady. Speak or connect companion.", 22, new Vector2(740, 420), new Vector2(0, -30));
            panel.statusTextMesh = CreateTMP(root.transform, "StatusText", "Online", 18, new Vector2(740, 40), new Vector2(0, 230));

            GameObject voiceInd = new GameObject("VoiceIndicator");
            voiceInd.transform.SetParent(root.transform);
            voiceInd.transform.localPosition = new Vector3(340, 230, 0);
            Image indImg = voiceInd.AddComponent<Image>();
            indImg.color = new Color(0.2f, 0.8f, 0.3f, 1f);
            voiceInd.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 20);
            panel.voiceIndicator = voiceInd;

            return panel;
        }

        private static DiffPanel CreateDiffPanel(Transform parent)
        {
            GameObject root = CreatePanelRoot<DiffPanel>(parent, "DiffPanel", new Vector3(-0.95f, 0f, 1.4f), new Vector2(900, 600), FloatingPanel.PanelId.Diff);
            DiffPanel panel = root.GetComponent<DiffPanel>();

            panel.summaryTextMesh = CreateTMP(root.transform, "SummaryText", "Diff Viewer - No active diffs", 20, new Vector2(840, 40), new Vector2(0, 250));
            panel.diffTextMesh = CreateTMP(root.transform, "DiffText", "No diff loaded.", 18, new Vector2(840, 480), new Vector2(0, -30));

            return panel;
        }

        private static TaskBoardPanel CreateTaskBoardPanel(Transform parent)
        {
            GameObject root = CreatePanelRoot<TaskBoardPanel>(parent, "TaskBoardPanel", new Vector3(0f, 0.65f, 1.5f), new Vector2(800, 320), FloatingPanel.PanelId.TaskBoard);
            TaskBoardPanel panel = root.GetComponent<TaskBoardPanel>();

            panel.taskTitleText = CreateTMP(root.transform, "TaskTitle", "Task Board", 24, new Vector2(740, 50), new Vector2(0, 110));
            panel.currentStageText = CreateTMP(root.transform, "CurrentStage", "Stage: Idle", 20, new Vector2(740, 40), new Vector2(0, 50));
            panel.stepsText = CreateTMP(root.transform, "StepsText", "Awaiting task...", 18, new Vector2(740, 120), new Vector2(0, -50));

            return panel;
        }

        private static AgentLogPanel CreateAgentLogPanel(Transform parent)
        {
            GameObject root = CreatePanelRoot<AgentLogPanel>(parent, "AgentLogPanel", new Vector3(0.95f, 0f, 1.4f), new Vector2(800, 550), FloatingPanel.PanelId.AgentLog);
            AgentLogPanel panel = root.GetComponent<AgentLogPanel>();

            panel.logTextMesh = CreateTMP(root.transform, "LogText", "Agent Log - Waiting for activity...", 18, new Vector2(740, 480), new Vector2(0, 0));

            return panel;
        }

        private static TerminalPanel CreateTerminalPanel(Transform parent)
        {
            GameObject root = CreatePanelRoot<TerminalPanel>(parent, "TerminalPanel", new Vector3(0f, -0.65f, 1.5f), new Vector2(800, 320), FloatingPanel.PanelId.Terminal);
            TerminalPanel panel = root.GetComponent<TerminalPanel>();

            panel.outputTextMesh = CreateTMP(root.transform, "OutputText", "Terminal: Idle", 18, new Vector2(740, 260), new Vector2(0, 0));

            return panel;
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, Vector2 size, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.layer = LayerMask.NameToLayer("UI");

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.richText = true;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            rect.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);

            return tmp;
        }
    }
}
#endif
