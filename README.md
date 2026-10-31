# Flowstate

**Your entire dev environment. Spoken. Spatial. Distraction-free.**

Flowstate is a VR productivity app for Meta Quest that lets developers run a complete software development lifecycle through natural voice conversation with an AI. No keyboard required. No context switching. Just you, your voice, and a spatial workspace that keeps everything visible and organized around you.

Define your project. Describe what you want to build. Review the plan, approve it, iterate on it. Watch agents execute. Review diffs in floating panels. Ship.

---

## Status

Pre-production. **Priority project.** Currently at Pipeline Stage 1–2 (concept framed, market validation next).

See [`docs/PIPELINE-STATUS.md`](docs/PIPELINE-STATUS.md) for current stage gate decisions and open risks.

---

## Core concept

1. **Speak** — _"Add user authentication with email and password"_ / _"Review what changed"_ / _"Run the tests and tell me what broke"_
2. **See** — agent panels update in real time. Code diffs float at arm's reach. Task boards shift. Terminal output streams.
3. **Approve** — review a plan, a diff, a PR. Say yes, request changes, or redirect.
4. **Ship** — commit, push, open a PR, trigger CI — all by voice.

---

## Project docs

| Document | Purpose |
|---|---|
| [`docs/PRODUCT-BRIEF.md`](docs/PRODUCT-BRIEF.md) | Audience, core loop, MLR scope, assumptions, open decisions |
| [`docs/TECHNICAL-DESIGN.md`](docs/TECHNICAL-DESIGN.md) | Voice pipeline, agent orchestration, spatial UI, host integration |
| [`docs/PIPELINE-STATUS.md`](docs/PIPELINE-STATUS.md) | Current pipeline stage, gate decisions, and risk register |
| [`docs/ASSET-AND-LICENSE-REGISTER.md`](docs/ASSET-AND-LICENSE-REGISTER.md) | Asset sources, license status, attribution obligations |
| [`AGENTS.md`](AGENTS.md) | Build path, verification requirements, project conventions |

---

## Target platform

- **Meta Quest 3** (primary — standalone + PC streaming hybrid)
- **Meta Quest 2** (secondary — reduced panel fidelity)

Engine: Unity 6 LTS · Meta XR SDK (All-in-One) · OpenXR

---

## Next steps

1. Complete Stage 2 competitor audit and technical feasibility assessment
2. Decide on host integration model (companion app vs. cloud dev environment)
3. Build Stage 4 prototype: voice → agent → code diff displayed in VR
4. Create Meta developer app identity and GitHub Project once build path is confirmed

---

## Repository

https://github.com/Sirrine-Jonathan/flowstate
