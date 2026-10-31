# Flowstate — Technical Design

_Pipeline stage: 3 — Define the product_
_Last updated: 2026-10-31_
_Status: Draft — architecture pending prototype validation_

---

## Platform and engine

| Decision | Choice | Status |
|---|---|---|
| Target hardware | Meta Quest 3 (primary), Quest 2 (secondary) | Decided |
| Engine | Unity 6 LTS | Recommended — confirm at Stage 5 |
| XR backend | OpenXR via Meta XR SDK (All-in-One) | Recommended — confirm at Stage 5 |
| Render pipeline | Universal Render Pipeline (URP) | Recommended for Quest performance |
| Passthrough | Not required at launch (productivity, not AR world-building) | Revisit post-launch |

Flowstate is a seated productivity environment, not a spatially-mapped AR experience. Passthrough is not needed for the core loop. The workspace is a purpose-built VR room, not an overlay on the physical world.

---

## System architecture overview

Flowstate is a **two-process system**: the Quest app is the interface layer, and a host process (companion app or cloud environment) is the compute layer. Agents, LLM calls, and code execution happen on the host. The Quest streams voice input to the host and receives structured UI updates in return.

```
┌─────────────────────────────────────┐     ┌──────────────────────────────────────────┐
│         Meta Quest 3 (UI layer)     │     │       Host (compute layer)               │
│                                     │     │                                          │
│  Mic → VAD → STT                    │────▶│  Voice intent parser (LLM)              │
│  Spatial UI (panels, environments)  │◀────│  Agent orchestrator                     │
│  Voice confirmation + transcripts   │     │  Code execution sandbox                 │
│  Session state / workspace layout   │     │  Git operations                         │
│                                     │     │  LLM API calls (OpenAI / Anthropic)     │
│  Unity 6 / Meta XR SDK / OpenXR     │     │  File system access                     │
└─────────────────────────────────────┘     └──────────────────────────────────────────┘
         WebSocket / local network                  macOS / Windows companion app
                                                    OR cloud dev environment
```

This architecture keeps the Quest doing what it's good at (rendering, voice capture, spatial UI) and offloads everything compute-intensive to hardware that can handle it. It also means the agent layer can be iterated without a Unity rebuild.

---

## Voice pipeline

```
Quest built-in mic
  → Push-to-talk gate / Voice Activity Detector (VAD)
  → Streaming STT (on-device Whisper or cloud)
  → Transcript → host via WebSocket
  → LLM intent parser (on host)
  → Structured command / agent dispatch
  → Agent execution
  → Structured result (diff, log, status) → Quest via WebSocket
  → Spatial UI update + voice confirmation
```

### Microphone capture

Unity `Microphone` API at 16 kHz mono. Push-to-talk default (right controller grip or dedicated button). Continuous VAD mode available in settings. Noise gate applied before STT submission.

### Speech-to-Text

Two options to evaluate in Stage 4:

**Option A — On-device Whisper (Quest 3)**
- Whisper Small via ONNX Runtime Unity binding or whisper.net.
- Fully offline; no transcript leaves the device.
- Prototype to measure real-time factor. Whisper Small targets ~real-time on Quest 3 NPU.

**Option B — Host-side STT**
- Audio streamed to companion app; STT runs on host CPU/GPU (faster, more accurate).
- Adds one network hop but reduces Quest compute load.
- Viable when host is on the same local network (low latency).

**Option C — Cloud STT (OpenAI Whisper API, Google, Deepgram)**
- Best accuracy; requires internet.
- Adds ~100–400 ms network latency.
- Fallback if neither on-device nor host-side meets accuracy targets.

Recommendation: prototype host-side STT first (fastest path, no Quest compute risk), then evaluate on-device for offline/privacy use case.

### Intent parsing (LLM on host)

The STT transcript is sent to the host's LLM layer with a structured system prompt. The LLM produces a typed intent object that the agent orchestrator acts on.

The system prompt includes:
- Current project context (repo name, active branch, recent commits, open tasks)
- Available command types and agent pipeline stages
- Instruction to output structured JSON for machine-readable commands and natural language for spoken response
- Instruction to ask one clarifying question rather than guess on ambiguous input
- Rolling conversation history (last N turns)

**LLM options:**

| Option | Latency | Cost | Offline | Notes |
|---|---|---|---|---|
| OpenAI GPT-4o API | ~300–600 ms | ~$0.01–0.05/turn | No | Best intent understanding; highest cost |
| Anthropic Claude 3.5 Sonnet | ~300–700 ms | ~$0.01–0.04/turn | No | Strong at code tasks |
| Local Ollama (Llama 3.1 70B on host) | ~1–3 s on M2/M3 Mac | Free | Yes | Viable for developer-owned hardware; slower |
| Local Ollama (Llama 3.1 8B on host) | ~200–500 ms on M2/M3 Mac | Free | Yes | Faster; weaker intent understanding |

Recommendation: model-agnostic architecture with the developer supplying their own API key or pointing at a local Ollama instance. Ship with OpenAI GPT-4o as the tested default.

---

## Agent orchestration

### Framework decision (pending Stage 2)

Options to evaluate:
- **Custom orchestrator** — simple sequential pipeline, full control, no external dependency. Appropriate given the workflow is well-defined and linear.
- **LangChain / LangGraph** — widely used, Python-native, good tool-calling support. Overhead for simple pipelines.
- **AutoGen / CrewAI** — multi-agent coordination; more relevant if parallel agents are needed.
- **Kiro agents** — workspace-native; evaluate for integration with existing workflow conventions.

Recommendation: start with a **custom lightweight orchestrator** (TypeScript or Python) targeting the 7-stage pipeline. Avoid framework lock-in until the pipeline shape is stable.

### Pipeline execution model

Each pipeline stage is a typed step with:
- `input` — what the step receives (transcript, plan, diff, test results)
- `execute` — the work done (LLM call, shell command, git operation)
- `output` — what the step produces (structured result sent to Quest UI)
- `pause_policy` — `always`, `on_failure`, `never`, or `if_configured`
- `timeout` — maximum execution time before the agent reports a stall

Stages run sequentially by default. The developer can configure parallel execution for independent stages (e.g., run tests and lint simultaneously) in `.flowstate/workflow.yml`.

### `.flowstate/workflow.yml` (per-project configuration)

```yaml
version: 1
pipeline:
  - stage: understand
    pause: on_ambiguity
  - stage: plan
    pause: always            # always show the plan before implementing
  - stage: implement
    pause: never
  - stage: verify
    steps:
      - run: npm test
      - run: npm run lint
      - run: npx tsc --noEmit
    pause: on_failure
  - stage: commit
    pause: always            # always approve commit message
    auto_approve_if:
      branch_pattern: "feat/*"
      tests_pass: true
  - stage: pr
    pause: always
  - stage: monitor
    pause: on_failure

preferences:
  llm: openai                # openai | anthropic | ollama
  model: gpt-4o
  voice_mode: push_to_talk   # push_to_talk | always_listening
  auto_save_session: true
```

---

## Host companion app

The companion app runs on the developer's macOS or Windows machine. It:
- Exposes a local WebSocket server the Quest connects to over Wi-Fi
- Receives voice transcripts from the Quest; sends structured UI updates back
- Runs the agent orchestrator and LLM calls
- Has full access to the local file system and git repository
- Executes shell commands in a sandboxed subprocess (configurable allowed commands)
- Manages LLM API keys locally (never sent to Quest or stored on headset)

### Security model

- The companion app only accepts connections from the paired Quest device (device ID handshake on first connect).
- Shell command execution is limited to a configurable allowlist. Destructive commands (force push, branch deletion, database drops) require explicit developer confirmation.
- LLM API keys are stored in the OS keychain, not in config files.
- No developer code or credentials are sent to any third party beyond the configured LLM API.

### Companion app tech stack (recommendation)

- **Electron** (macOS + Windows, single codebase) or native macOS app (Swift) for tighter OS integration.
- WebSocket server: Node.js `ws` or Python `websockets`.
- Git operations: `simple-git` (Node.js) or `GitPython`.
- Shell execution: Node.js `child_process` or Python `subprocess` with allowlist enforcement.
- LLM SDK: OpenAI Node.js SDK or Python SDK, with Anthropic and Ollama as switchable providers.

Decision pending Stage 4 prototype: Electron is the fastest cross-platform path; evaluate native macOS if performance or OS integration requires it.

---

## Spatial UI system

### Panel architecture

Each panel is a Unity `RenderTexture`-backed world-space canvas. Panels are:
- Positioned in world space (not head-locked)
- Grabbable and repositionable via hand tracking or controller
- Scalable via pinch gesture
- Individually dismissable and re-summoned by voice or hand menu
- Layout persisted per developer profile

### Panel types

| Panel | Content | Update trigger |
|---|---|---|
| **Chat / AI** | Conversation history, AI responses, transcripts | Every voice turn |
| **Diff Viewer** | Syntax-highlighted code diff (unified or split view) | On implement stage output |
| **Task Board** | Current task, pipeline stage progress, queued tasks | On task state change |
| **Agent Log** | Step-by-step agent execution log, timestamps, errors | Streaming during execution |
| **Terminal** | stdout/stderr from test runs, build output, shell commands | On verify stage output |

### Text rendering

Code legibility is the primary constraint. Requirements:
- Monospace font (JetBrains Mono or Fira Code — both OFL licensed)
- Minimum 18pt equivalent at default panel distance (0.8–1.2 m)
- Syntax highlighting via a lightweight shader-based approach (no heavy DOM/browser rendering)
- High-contrast dark theme default; light theme option
- Configurable font size (small / medium / large / extra-large)
- Line length capped at ~80 characters per panel width to avoid horizontal scrolling

Prototype test: can a developer read and understand a 50-line diff at a comfortable seated distance without leaning or squinting? This must pass before any other spatial UI work proceeds.

### Hand menu

A persistent radial menu attached to the non-dominant hand (left by default). Sections:
- **Panels** — show/hide each panel
- **Workspace** — switch environment, reset layout, save layout
- **Session** — save, end session, settings
- **Agent** — stop, pause, resume

All hand menu actions are also available by voice.

---

## Connectivity and latency budget

Target: **voice end → world update start < 3 seconds** (P80) on Quest 3 with local Wi-Fi connection to host.

| Step | Target latency |
|---|---|
| VAD detection + audio capture | < 200 ms |
| STT (host-side) | < 500 ms |
| LLM intent parse | < 700 ms |
| Agent stage 1 (understand) | < 500 ms |
| WebSocket round trip (local) | < 20 ms |
| Quest UI update | < 100 ms |
| **Total (to first visible response)** | **< 2 s** |

Full pipeline (understand → plan displayed to developer): < 5 seconds.
Full pipeline (understand → implement → diff displayed): < 30 seconds for a typical small change.

These are targets for Stage 4 measurement, not guarantees.

---

## Performance targets (Quest 3, UI rendering only)

| Metric | Target |
|---|---|
| Sustained frame rate | 72 fps |
| Draw calls | < 80 per frame (panels + environment) |
| GPU time | < 8 ms |
| Memory (app) | < 800 MB |
| Panel RenderTexture resolution | 1024×768 per panel (adjustable) |

The Quest is rendering UI panels and a static environment — compute load is low compared to a game. Performance risk is in text rendering quality, not raw GPU throughput.

---

## Open technical decisions

| Decision | Options | Resolve at |
|---|---|---|
| STT location: Quest on-device vs. host-side vs. cloud | Stage 4 latency + accuracy test | Stage 4 |
| LLM provider default | OpenAI GPT-4o vs. Anthropic vs. model-agnostic | Stage 4 (ship model-agnostic) |
| Agent framework | Custom orchestrator vs. LangChain vs. AutoGen | Stage 2 evaluation |
| Companion app tech | Electron vs. native macOS | Stage 4 prototype |
| Panel text rendering | Unity TextMeshPro + custom shader vs. embedded browser | Stage 4 legibility test |
| Syntax highlighting approach | Pre-processed on host vs. shader on Quest | Stage 4 |
| Cloud dev environment (post-launch) | GitHub Codespaces, Gitpod, custom | Post-launch scoping |
