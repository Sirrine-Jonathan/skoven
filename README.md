# Skoven

**Speak. Direct. See it happen.**

Skoven is a voice-driven AI agent workspace for Meta Quest. Put on the headset, describe what you want done, and watch a spatial workspace come alive around you — agents executing, diffs appearing, logs streaming, decisions waiting for your approval. All by voice. No keyboard. No context switching.

Three pillars:

1. **Orchestrator conversation** — natural voice dialogue with a central AI that understands intent, remembers context, and coordinates everything on your behalf.
2. **Empowered agents** — a configurable pipeline capable of complex end-to-end workflows: software development, research, writing, system operations, and beyond.
3. **Transparent spatial visualization** — every running workflow is a live structure around you in VR/AR, scalable from high-level status down to individual diffs, logs, and approvals.

---

## Status

Active development. Stage 4 prototype in progress.

See [`docs/PIPELINE-STATUS.md`](docs/PIPELINE-STATUS.md) for current stage gate decisions and open risks.

---

## Repository

https://github.com/Sirrine-Jonathan/skoven

---

## Project structure

```
skoven/
├── companion/          Node.js/TypeScript host app — WebSocket bridge, agent orchestrator, LLM layer
├── unity/              Unity 6 Quest app — spatial UI, voice capture, XR interaction
└── docs/               Product brief, technical design, pipeline status, dev setup
```

---

## Quick start

### 1. Start the Companion App (Host Machine)

```bash
cd skoven/companion
cp .env.example .env
# Add your LLM API key (OpenAI, Anthropic, OpenRouter, Ollama, or custom) in .env
npm install
npm run dev
```

### 2. Forward WebSocket over USB (Zero-Config)

With your Quest 3 connected via USB debugging:
```bash
adb reverse tcp:8765 tcp:8765
```

### 3. Put on Headset & Interact

| Action | Control |
|---|---|
| **Speak to Orchestrator** | Hold **Right Controller Grip** or click **[🎙️ Push to Talk]** |
| **Move / Reposition Panels** | Point laser ray with **Left Hand** and hold **Left Grip** to grab, move, and rotate; release to save |
| **Click Buttons / Approvals** | Point laser ray and pull **Trigger** (or tap **A / X**) |

Full setup at [`docs/DEV-SETUP.md`](docs/DEV-SETUP.md).

---

## Project docs

| Document | Purpose |
|---|---|
| [`docs/PRODUCT-BRIEF.md`](docs/PRODUCT-BRIEF.md) | Audience, three pillars, core loop, MLR scope, assumptions |
| [`docs/TECHNICAL-DESIGN.md`](docs/TECHNICAL-DESIGN.md) | Voice pipeline, agent orchestration, host companion, spatial UI |
| [`docs/PIPELINE-STATUS.md`](docs/PIPELINE-STATUS.md) | Current pipeline stage, gate decisions, risk register |
| [`docs/ASSET-AND-LICENSE-REGISTER.md`](docs/ASSET-AND-LICENSE-REGISTER.md) | Asset sources, license status |
| [`docs/DEV-SETUP.md`](docs/DEV-SETUP.md) | Full setup guide for companion app and Unity project |
| [`AGENTS.md`](AGENTS.md) | Build path, verification requirements, project conventions |

---

## Target platform

- **Meta Quest 3** (primary — full VR + color passthrough AR)
- **Meta Quest 2** (secondary — VR only, reduced fidelity)

Engine: Unity 6 LTS · Meta XR SDK (All-in-One) · OpenXR
