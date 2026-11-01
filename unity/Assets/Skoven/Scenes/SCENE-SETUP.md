# Skoven — Main Scene Setup Guide

This document describes how to configure the main Unity scene. There is no
auto-generated scene YAML here (Unity scenes are binary); follow these steps
in the Unity Editor after importing the project.

---

## Scene hierarchy

```
[Scene: SkovenMain]
├── XR Origin (Action-Based)           ← Meta XR SDK prefab
│   ├── Camera Offset
│   │   └── Main Camera
│   ├── LeftHand Controller
│   └── RightHand Controller
│
├── Skoven Core
│   ├── WebSocketClient                 ← Network/WebSocketClient.cs
│   ├── MessageRouter                   ← Network/MessageRouter.cs
│   ├── SkovenBootstrap              ← Agent/SkovenBootstrap.cs
│   ├── VoiceCapture                    ← Voice/VoiceCapture.cs
│   ├── VoiceInputController            ← Voice/VoiceInputController.cs
│   ├── TtsPlayer                       ← Agent/TtsPlayer.cs
│   ├── PanelManager                    ← UI/PanelManager.cs
│   ├── WorkspaceCommandHandler         ← UI/WorkspaceCommandHandler.cs
│   └── WorkspaceSettings               ← Settings/WorkspaceSettings.cs
│
├── Panels
│   ├── ChatPanel                       ← Canvas (World Space) + ChatPanel.cs
│   ├── DiffPanel                       ← Canvas (World Space) + DiffPanel.cs
│   ├── TaskBoardPanel                  ← Canvas (World Space) + TaskBoardPanel.cs
│   ├── AgentLogPanel                   ← Canvas (World Space) + AgentLogPanel.cs
│   └── TerminalPanel                   ← Canvas (World Space) + TerminalPanel.cs
│
├── Hand Menu                           ← UI/HandMenu.cs (child of LeftHand)
│
├── Environments
│   ├── VoidEnvironment                 ← Active by default (dark skybox, subtle grid floor)
│   ├── StudioEnvironment               ← Minimal warm-lit room geometry
│   └── ExteriorEnvironment             ← Calm outdoor skybox + distant terrain
│
├── Setup Wizard                        ← Settings/SetupWizard.cs (head-locked canvas)
│
└── Lighting
    └── Directional Light
```

---

## Panel configuration (for each panel Canvas)

1. **Canvas** component:
   - Render Mode: **World Space**
   - Width: 1.2, Height: 0.8 (adjust to taste; DiffPanel can be wider: 1.6)
   - Dynamic Pixels Per Unit: 2 (improves text sharpness)

2. Add **FloatingPanel** subclass script (e.g., `ChatPanel`) to the Canvas root.

3. Add **XRGrabInteractable** (from XR Interaction Toolkit) — this lets the player grab and reposition panels.

4. Add a **Canvas Scaler** with:
   - UI Scale Mode: Constant Physical Size
   - Physical Unit: Centimeters
   - Fallback Screen DPI: 96

5. **TextMeshPro** elements inside the panel:
   - Font: JetBrains Mono (import from `.otf`, generate TMP font asset)
   - Font Size: 18 (code panels), 14 (labels)
   - Tag content text objects with the `PanelContentText` tag for font-size scaling
   - Enable Rich Text on all code/log text elements
   - Overflow: Overflow (ScrollRect handles clipping)

6. **ScrollRect** on each panel that shows long content (Diff, AgentLog, Terminal):
   - Attach a Scroll Rect component
   - Assign a Viewport (masked RectTransform) and Content (the TMP text parent)
   - Vertical: true, Horizontal: false

---

## Default panel positions (relative to player, facing forward)

Tune these in the scene; they are saved per-player after first launch.

| Panel      | Local Position (x, y, z) | Notes |
|---|---|---|
| Chat/AI    | (0, 0, 1.5)              | Center, eye level |
| Diff       | (-0.8, 0, 1.4)           | Left of center |
| Task Board | (0, 0.5, 1.5)            | Above center |
| Agent Log  | (0.8, 0, 1.4)            | Right of center |
| Terminal   | (0, -0.5, 1.5)           | Below center |

---

## MessageRouter wiring (Inspector)

On the **MessageRouter** component, wire each UnityEvent to the correct method:

| Event              | Target                  | Method                     |
|---|---|---|
| onHandshakeAck     | ChatPanel               | OnHandshakeAck             |
| onHandshakeAck     | TaskBoardPanel          | OnHandshakeAck             |
| onTranscriptAck    | ChatPanel               | OnTranscriptAck            |
| onAgentUpdate      | ChatPanel               | OnAgentUpdate              |
| onAgentUpdate      | AgentLogPanel           | OnAgentUpdate              |
| onAgentUpdate      | TaskBoardPanel          | OnAgentUpdate              |
| onAgentUpdate      | TerminalPanel           | OnAgentUpdate              |
| onPlanReady        | ChatPanel               | OnPlanReady                |
| onPlanReady        | TaskBoardPanel          | OnPlanReady                |
| onDiffReady        | DiffPanel               | OnDiffReady                |
| onVerifyResult     | DiffPanel               | OnVerifyResult             |
| onVerifyResult     | TerminalPanel           | OnVerifyResult             |
| onCommitReady      | ChatPanel               | OnCommitReady              |
| onAgentComplete    | ChatPanel               | OnAgentComplete            |
| onAgentComplete    | AgentLogPanel           | OnAgentComplete            |
| onAgentComplete    | TaskBoardPanel          | OnAgentComplete            |
| onAgentError       | ChatPanel               | OnAgentError               |
| onAgentError       | AgentLogPanel           | OnAgentError               |
| onSessionState     | WorkspaceCommandHandler | OnSessionState             |
| onSpeech           | ChatPanel               | OnSpeech                   |
| onSpeech           | TtsPlayer               | OnSpeech                   |

---

## XR and build settings

1. **Project Settings → XR Plug-in Management**: enable OpenXR for Android.
2. **OpenXR settings**: add Meta Quest Touch Pro Controller Profile and Meta Quest Touch Controller Profile.
3. **Player Settings → Android**:
   - Package name: `com.skoven.app` (update once Meta app identity is created)
   - Minimum API: 29 (Quest minimum)
   - Target API: 32
   - Scripting Backend: IL2CPP
   - Target Architecture: ARM64 only
4. **Player Settings → Other → Internet Access**: Required (WebSocket + LLM API)
5. **Player Settings → Other → Microphone Usage Description**: "Skoven uses your microphone to capture voice commands."
6. **Quality Settings**: use the URP Quest-optimized preset (Single Pass Instanced, no MSAA).

---

## Font import

1. Download JetBrains Mono from https://www.jetbrains.com/lp/mono/ (OFL license)
2. Import `JetBrainsMono-Regular.ttf` into `Assets/Skoven/Fonts/`
3. Window → TextMeshPro → Font Asset Creator → generate atlas at 4096×4096, SDF mode
4. Assign the resulting `.asset` to `WorkspaceSettings.codeFont`
5. Repeat for OpenDyslexic (from https://opendyslexic.org, OFL license), assign to `dyslexiaFont`
