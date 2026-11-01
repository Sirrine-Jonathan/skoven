# Skoven — Voice-Driven AI Agent Workspace for Meta Quest

Skoven is a native Meta Quest app built around three pillars:

1. **Orchestrator conversation** — natural, continuous verbal dialogue with a central AI that understands intent, remembers context, and directs work on your behalf.
2. **Empowered agents** — a configurable pipeline of agents capable of carrying out complex workflows end-to-end: software development, research, writing, system operations, and beyond.
3. **Transparent spatial visualization** — every running workflow is visible as a live spatial structure around you in VR/AR, scalable from a high-level status overview down to individual step output, diffs, logs, and decisions.

The Quest headset is the interface. Compute lives on the host machine or in the cloud. You speak; the world responds.

## Product direction

- Working product: **Skoven**, a voice-first VR/AR workspace for operating AI agent workflows spatially and conversationally.
- The core promise: describe any complex task by voice, watch agents plan and execute it in real time around you, review and approve at any level of detail, ship — without leaving the headset.
- Audience: developers, knowledge workers, and power users who want hands-free, distraction-free, spatially organized AI-powered work.
- The three pillars are equally important. The orchestrator conversation (voice quality, latency, context) and the spatial visualization (legibility, detail scalability) are as load-bearing as the agent capability itself.
- Dev workflows (code, git, CI) are the initial vertical. The agent and visualization architecture is designed to generalize beyond dev work from the start.

## Build path

- Target platform: **Meta Quest 3** (primary), Quest 2 (secondary / reduced fidelity).
- Engine: **Unity 6 LTS** with Meta XR SDK (All-in-One) and OpenXR backend.
- Voice pipeline: Quest mic → VAD → STT (host-side Whisper) → LLM intent layer → agent dispatch → structured result → spatial UI update.
- Host compute: companion app (Node.js/TypeScript) on developer's Mac/Windows machine, or cloud environment post-launch.
- LLM: provider-agnostic (OpenAI, Anthropic, OpenRouter, Ollama, custom endpoint). All BYOK.
- Agent orchestrator: custom lightweight TypeScript pipeline, 7 stages, configurable pause points, per-project `.skoven/workflow.yml`.
- Spatial visualization: world-space Unity panels (Chat/Orchestrator, Diff Viewer, Task Board, Agent Log, Terminal) + flow graph view (Stage 6+).

## Repository

https://github.com/Sirrine-Jonathan/skoven

Default branch: `main`. Follow the workspace [WORKFLOW.md](../WORKFLOW.md) for branch naming, PR conventions, and handoff.

## Meta app identity

| Field | Value |
|---|---|
| App ID | `1122808136998612` |
| Package | `com.sirrine.skoven` |
| Dashboard | https://developer.oculus.com/manage/applications/1122808136998612 |

Full signing, channel, and entitlement details in `docs/APP-IDENTITY.md`. Do not alter the App ID in `OculusSettings.asset` or `OculusAppConfig.cs` without updating that document.

## Required verification and product constraints

- Read the workspace `AGENTS.md`, `docs/VR-PRODUCT-PIPELINE.md`, and relevant shared skills under `.agents/skills/` before project work.
- Never claim a headset build, device run, Store check, or test passed unless actually performed. Report what was checked and what remains pending.
- Treat voice latency, agent reliability, and spatial panel legibility as blocking product requirements. All three must be verified on real Quest 3 hardware before Stage 5.
- Record all asset provenance and license status in `docs/ASSET-AND-LICENSE-REGISTER.md` before any Store release.
- Do not upload, publish, create production app identity, or alter signing material without required project information and explicit user direction.
- Comfort: the entire core workflow must be achievable while seated. No locomotion required for any productive task.

## Current product docs

- `docs/PRODUCT-BRIEF.md` — concept, audience, three pillars, core loop, MLR scope, assumptions.
- `docs/TECHNICAL-DESIGN.md` — voice pipeline, agent orchestration, host companion, spatial UI, performance targets.
- `docs/PIPELINE-STATUS.md` — current stage, gate decisions, risk register.
- `docs/ASSET-AND-LICENSE-REGISTER.md` — asset sources, license obligations.
- `docs/DEV-SETUP.md` — how to run the companion app and open the Unity project.

## GitHub Project

Not yet created. Create under `Sirrine-Jonathan` when Stage 4 prototype is underway. Link here once created.
