# Skoven — Pipeline Status

_Last updated: 2026-10-31_
_Priority project in the VR Games Factory workspace._

This file tracks Skoven's position in the [VR Product Pipeline](../../docs/VR-PRODUCT-PIPELINE.md), records gate decisions, and surfaces open risks. Update after every stage gate decision.

---

## Current stage: Stage 1 → Stage 2

The concept brief is written (Stage 1 complete). Stage 2 market and feasibility validation is the next required step.

---

## Stage 1 — Frame the opportunity

**Status: Complete**

| Item | Answer |
|---|---|
| Intended audience | Software developers who use AI coding tools and want deeper flow and hands-free operation |
| Player need / fantasy | Run your entire dev workflow by voice, distraction-free, in a spatial workspace |
| Core activity | Voice-driven AI agent orchestration across the full SDLC: plan → implement → review → ship |
| VR improvement | Distraction blocking (physical boundary), spatial multi-panel layout without window management, hands-free operation, deliberate work rituals |
| Differentiator | No Quest app combines voice-first SDLC workflow, configurable agent pipelines, and a spatial developer workspace |
| Comparable products | Horizon Workrooms (collaboration, not coding), VR desktops (mirror existing UI, no AI), AI coding tools (desktop-only, no VR) |
| Session shape | Focused 30–90 minute deep work sessions; resumable across sessions |
| First-session promise | Connected, first successful voice-driven code change within 10 minutes of first launch |

**Riskiest assumptions named:**
1. Developers will accept voice-only task input for complex, real coding work
2. Agent round-trip latency is low enough for a conversational workflow
3. Code diff panels are legible at VR reading distances
4. Quest 3 Wi-Fi connection to a local host is reliable for sustained work sessions
5. Developers will trust AI agents with commit/push on real repositories

**Gate decision: Continue**

---

## Stage 2 — Validate market and feasibility

**Status: Not started**

Required evidence before Stage 3 is closed:

- [ ] **Competitor audit:** Search Meta Horizon Store for developer tools, VR desktops, coding assistants, and productivity apps. Document what exists, what's missing, and current ratings/reviews. Search date and methodology recorded.
- [ ] **Developer demand signal:** Survey or interview at least 5 working developers. Key questions: Do you use AI coding tools? Would you use a VR workspace for deep work? What's your biggest friction with current AI tools? What would make you trust an agent with a push?
- [ ] **Agent latency feasibility:** Benchmark a simple voice → LLM → code change → result loop on a representative host machine (M2/M3 Mac). Measure wall-clock time. Confirm it fits within the 3-second first-response target.
- [ ] **Text legibility feasibility:** Quick Unity prototype — render a code diff on a world-space panel and test readability at 0.8–1.2 m at Quest 3 resolution. This can be done before a full prototype build.
- [ ] **Meta Store category check:** Confirm Meta Horizon Store accepts developer productivity / tools apps. Review VRC requirements for apps using microphone, internet, and local network permissions.
- [ ] **LLM cost model:** Estimate cost per session (N turns × token estimate × API pricing). Model at 30-minute and 60-minute session lengths. Assess against subscription pricing ($9.99/mo, $14.99/mo).
- [ ] **Agent framework survey:** Evaluate custom orchestrator vs. LangChain vs. AutoGen vs. Kiro agents. Choose based on simplicity, TypeScript/Python fit, and long-term maintainability.

**Gate decision: Pending**

---

## Stage 3 — Define the product

**Status: Draft complete — pending Stage 2 validation**

Core documents written:
- [PRODUCT-BRIEF.md](PRODUCT-BRIEF.md) — concept, audience, core loop, onboarding, spatial UI, agent workflow, comfort, MLR scope, non-goals, assumptions
- [TECHNICAL-DESIGN.md](TECHNICAL-DESIGN.md) — voice pipeline, agent orchestration, host companion app, spatial UI system, connectivity budget, performance targets
- [AGENTS.md](../AGENTS.md) — build path, verification constraints, project conventions

Remaining Stage 3 work:
- [ ] Update PRODUCT-BRIEF.md with findings from Stage 2 competitor audit and developer interviews
- [ ] Confirm LLM provider strategy and record in TECHNICAL-DESIGN.md
- [ ] Confirm agent framework choice and record in TECHNICAL-DESIGN.md
- [ ] Add pricing hypothesis once Meta Store category and comparables are confirmed
- [ ] Name decision: confirm "Skoven" is clear of trademarks and available as a Meta app identifier

**Gate decision: Pending Stage 2 evidence**

---

## Stage 4 — Prototype the largest risk

**Status: Not started**

**Largest risks (in order):**
1. Voice → agent → code change → diff displayed at acceptable latency
2. Code diff legibility in VR at comfortable reading distance
3. Developer trust and willingness to use voice for real tasks

**Prototype scope (minimum testable slice):**
- Unity 6 project, Meta XR SDK, OpenXR backend
- Quest mic → STT (host-side, streamed over local WebSocket)
- Host companion app (Electron or Node.js server): receives transcript, calls LLM, runs a simple agent (implement a code change in a test repo), returns diff
- Diff Viewer panel + Chat/AI panel in VR — no other panels required for prototype
- One workspace environment (Void)
- Push-to-talk only
- Test with a real Git repository, real LLM API calls, real code changes

**Acceptance criteria:**
- A working developer, using their own real project, makes a successful voice-driven code change and reviews the diff in VR — without leaving the headset
- Voice → first AI response (plan or clarification): P80 < 3 seconds
- Voice → diff displayed: P80 < 30 seconds for a small change
- 3 out of 5 developer test participants say they would use this for real work
- Diff panel legibility: all 5 test participants can read a 50-line diff without leaning or squinting

**Evidence required:**
- Screen recording + OVR Metrics Tool capture of 5 developer sessions
- Latency log: STT time, LLM time, agent execution time, total round trip
- Session notes from each participant: friction points, trust moments, desired changes

**Gate decision: Not started**

---

## Stage 5 — Reproducible foundation

**Status: Not started**

Required before Stage 6:
- [ ] Pinned Unity version, Meta XR SDK version, and package manifest committed
- [ ] Companion app dependencies pinned in package.json / requirements.txt
- [ ] Application ID registered in Meta developer portal (test org)
- [ ] Clean development build installs and runs on Quest 3 from documented steps
- [ ] Companion app installs and connects to Quest from documented steps
- [ ] Secrets and API keys excluded from repository; `.env.example` provided
- [ ] Second team member can reproduce the build from documented inputs

---

## Open risks

| Risk | Severity | Mitigation | Status |
|---|---|---|---|
| Voice → agent latency too high for conversation | High | Prototype and measure early; host-side STT reduces Quest compute bottleneck | Pending Stage 4 |
| Developers won't trust agents with real repos | High | Explicit approval gates at plan and commit; test with real repos in prototype | Pending Stage 4 |
| Code diff illegible at VR resolution | High | Text legibility test before full prototype; configurable font size | Pending Stage 4 |
| Quest Wi-Fi reliability during work sessions | Medium | Prototype test: sustained 60-minute session; document minimum network requirements | Pending Stage 4 |
| LLM API cost unsustainable | Medium | Cost model in Stage 2; model-agnostic architecture lets devs use own keys | Pending Stage 2 |
| Meta Store rejects developer tools category | Medium | Review VRC and category policies in Stage 2 before building | Pending Stage 2 |
| Companion app complexity underestimated | Medium | Scope companion app as a thin WebSocket bridge first; add features incrementally | Pending Stage 4 |
| Agent produces incorrect or destructive code | Medium | Approval gates before every commit/push; undo always available | Design decision — ongoing |
| "Skoven" name conflicts | Low | Trademark search in Stage 2 | Pending Stage 2 |
