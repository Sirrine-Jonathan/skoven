# Flowstate — VR Vibe Coding App

Flowstate is the **priority project** in this workspace. It is a native Meta Quest productivity app that gives developers a spatial, voice-driven interface for running a full software development lifecycle through AI agents.

## Product direction

- Working concept: **Flowstate** — a VR productivity environment where developers speak to an AI, define tasks, review agent output, approve changes, and ship software, all without leaving the headset.
- The core promise: a developer can describe a feature, watch agents plan and implement it, review diffs and test results in spatial panels, and merge — entirely through voice, with the headset as a distraction-free workspace.
- Audience, pricing, host integration model, and agent orchestration approach are all hypotheses. Track and validate in `docs/PRODUCT-BRIEF.md` before committing to engineering depth.
- The Stage 4 prototype target is: voice command → AI agent executes a real code change → diff displayed in a VR panel. Everything else is downstream of proving that loop.
- This is a productivity tool first. VR is the delivery mechanism (distraction-free, spatial, hands-free), not the experience itself. Design decisions should serve developer flow, not VR novelty.

## Build path

- Target platform: **Meta Quest 3** (primary), Quest 2 (secondary / reduced fidelity).
- Recommended engine: **Unity 6 LTS** with Meta XR SDK (All-in-One) and OpenXR backend.
- Voice pipeline: Quest mic → VAD → STT → LLM intent layer → agent dispatch → structured result → spatial UI update. See `docs/TECHNICAL-DESIGN.md` for detail.
- Host integration: the agent layer runs on a connected development machine or cloud environment, not inside the headset. The Quest is the interface; compute lives outside it. This is the central architectural decision — validate the connectivity and latency model in Stage 4.
- Do not substitute simulator results for headset behavior on the voice pipeline, spatial UI legibility, or comfort checks.

## Required verification and product constraints

- Read the workspace `AGENTS.md`, `docs/VR-PRODUCT-PIPELINE.md`, and relevant shared skills under `.agents/skills/` before project work.
- Never claim a headset build, device run, Store check, or test passed unless actually performed. Report what was checked and what remains pending.
- Treat voice recognition accuracy, agent response latency, and code diff legibility at VR text sizes as blocking product requirements. All three must be verified on real Quest 3 hardware.
- Text rendering at comfortable reading distance and font size is a first-order design constraint. Prototype readable code panels before anything else in the spatial UI.
- Record all asset provenance and license status in `docs/ASSET-AND-LICENSE-REGISTER.md` before any Store release.
- Do not upload, publish, create production app identity, or alter signing material without required project information and explicit user direction.
- Comfort requirements: the entire core workflow must be achievable while seated. No locomotion required for any productive task. Hand menus and gaze/controller pointer for all spatial interaction.

## Current product docs

- `docs/PRODUCT-BRIEF.md` — concept, audience, core loop, onboarding, comfort, MLR scope, non-goals, assumptions.
- `docs/TECHNICAL-DESIGN.md` — voice pipeline, agent orchestration, host integration, spatial UI, persistence.
- `docs/ASSET-AND-LICENSE-REGISTER.md` — asset sources, license obligations.
- `docs/PIPELINE-STATUS.md` — current stage in the VR product pipeline, gate decisions, and open risks.

## GitHub Project

Not yet created. Create under `Sirrine-Jonathan` when Stage 2 validation is underway. Link here once created.
