# Skoven — Product Brief

_Pipeline stage: 3 — Define the product_
_Last updated: 2026-10-31_
_Status: Draft — pending Stage 2 market validation_

---

## Concept summary

Skoven is a VR productivity app for Meta Quest. It gives developers a spatial, distraction-free workspace where they operate their entire development workflow through natural voice conversation with an AI. No keyboard. No Alt-Tab. No notification pull. You put on the headset, describe what you want to build, and work with AI agents to plan, implement, review, and ship it — while floating code panels, task boards, and terminal output stay organized around you in three-dimensional space.

The central insight: the Quest headset is the best distraction-blocking device most developers already own. Skoven turns it into a productive tool rather than a toy.

---

## Intended audience

**Primary:** Software developers who already use AI coding tools (Cursor, GitHub Copilot, Claude) and want deeper flow — fewer interruptions, hands-free operation, and the ability to drive complex multi-step tasks through conversation rather than context-menu navigation. Comfort with voice assistants and AI pair programming is assumed.

**Secondary:** Developers with repetitive strain injuries or physical accessibility needs who benefit from hands-free, voice-primary interaction. Remote workers who want a spatial separation between "deep work" and everything else.

**Not the primary target:** Non-technical users, designers without coding workflows, or developers who need pixel-precise code editing (Skoven delegates editing to agents and connected tools, not direct text input).

---

## Why VR materially improves this

The problem isn't capability — AI coding tools already exist on desktop. The problem is **attention**. Developers lose flow constantly: a Slack message, a browser tab, an email notification. The headset is a physical and perceptual boundary that existing tools cannot provide.

Beyond distraction blocking, VR adds:
- **Spatial organization** — a diff panel, a task board, a terminal, and a chat history can all be visible simultaneously without overlapping windows or multiple monitors.
- **Hands-free operation** — describing a change verbally while reviewing a diff is faster than typing and clicking through menus.
- **Physical context switching** — putting on and removing the headset is a more deliberate and effective ritual than minimizing windows.

This is a productivity argument, not a novelty argument. The VR form factor is justified by the work, not the other way around.

---

## Core loop

1. **Orient** — on session start, the workspace restores: active project, recent tasks, open agents, last diff. The AI briefs the developer on where things stand.
2. **Describe** — the developer speaks a task: _"Add a forgot-password flow to the auth module"_ / _"What's the status of the PR I had open?"_ / _"Run the failing tests and summarize what's broken."_
3. **Plan** — the AI proposes a plan: steps, affected files, estimated scope. The developer approves, modifies, or redirects by voice.
4. **Execute** — agents run. Code changes, test runs, lint results, and file diffs stream into spatial panels in real time.
5. **Review** — the developer reads diffs in floating panels, listens to the AI summarize changes, and approves or requests revisions by voice.
6. **Commit and ship** — on approval, the agent commits, pushes, opens a PR, and reports CI status. The developer can trigger or monitor any step by voice.
7. **Continue or end session** — the workspace state is saved on exit. The next session resumes exactly where this one left off.

---

## Install-to-first-use path

Target: **a developer has connected their project and issued their first successful voice command within 10 minutes of first launch.**

1. App launches to a clean spatial workspace. A setup assistant walks through three steps: connect to host (companion app or cloud env), select a project repository, set voice wake mode (push-to-talk recommended).
2. On completion, the AI greets the developer with a brief project summary pulled from the repo: recent commits, open issues, branch status.
3. A single floating prompt: _"What would you like to work on?"_
4. No mandatory tutorial. Contextual hints appear based on what the developer hasn't tried yet.

---

## Onboarding principles

- Setup is a one-time flow, not a tutorial gate on every session.
- The AI speaks first and establishes context before asking anything of the developer.
- Every voice command is confirmed with a transcript line and a brief audio acknowledgement.
- Push-to-talk is the default — more reliable, less fatiguing for long sessions, lower false-trigger risk.
- The developer can ask _"What can I say?"_ at any time to get a context-aware list of available commands.

---

## Spatial workspace design

The developer sits (or stands) at the center of a configurable spatial workspace. Panels are arranged in the space around them and persist between sessions.

### Default panel layout

```
         [ Task Board ]
              ↑
[ Diff Viewer ]  [ Chat / AI ]  [ Agent Log ]
              ↓
         [ Terminal ]
```

All panels:
- Pinned to world space (don't follow head movement) for stable reading
- Repositionable by grab-and-place (hand tracking or controller)
- Resizable by pinch-scale
- Individually dismissable and re-summoned by voice: _"Show the diff"_ / _"Hide the terminal"_

### Environments

The developer chooses a background environment for their workspace — not for novelty, but for focus:
- **Void** — neutral dark space, no background distractions (default)
- **Studio** — minimal warm-lit room
- **Exterior** — calm open-air setting (mountains, coast) for context switching from indoor work
- Custom environments are a post-launch feature.

Environments are static and low-detail. They are not the product. They are the room the developer works in.

---

## Agentic workflow system

The AI operates a configurable pipeline of agents. The developer can inspect, customize, and extend this pipeline.

### Default pipeline stages

| Stage | Agent role | Developer interaction |
|---|---|---|
| **Understand** | Parse intent, clarify ambiguities, identify affected files | Approve or redirect the understood task |
| **Plan** | Propose implementation steps, estimate scope, flag risks | Approve plan, modify steps, or cancel |
| **Implement** | Write or modify code, following project conventions | Review diff, approve or request changes |
| **Verify** | Run tests, lint, type-check; report results | Review results; decide to proceed or fix |
| **Commit** | Stage changes, write commit message, push to branch | Approve commit message or dictate revision |
| **PR / Review** | Open PR, write description, request reviewers | Approve PR content or revise |
| **Monitor** | Watch CI, summarize failures, propose fixes | Approve fix or deprioritize |

### Customizable workflow

Developers can modify the pipeline by voice or by editing a YAML configuration:
- Skip stages: _"Just implement it, don't run tests yet."_
- Add stages: custom scripts, linters, deployment hooks
- Set auto-approve rules: _"Auto-approve commits on feature branches when tests pass."_
- Pause points: require explicit approval before specific stages (e.g., always pause before push)

Workflow configuration is per-project and lives in the project repository alongside the code (`.skoven/workflow.yml`).

---

## Voice command taxonomy

### Project and session management
- _"Open project [name]"_ / _"Switch to [branch]"_
- _"What's the status?"_ / _"Brief me on what changed since yesterday."_
- _"Save session"_ / _"End session."_

### Task execution
- _"[Natural language task description]"_ — triggers Understand → Plan → Implement pipeline
- _"Run the tests"_ / _"Lint the project"_ / _"Check for type errors."_
- _"Show me what changed"_ / _"Explain this diff."_

### Review and approval
- _"Approve"_ / _"Looks good"_ / _"Ship it."_
- _"Request changes: [description]"_ / _"Try again with [modification]."_
- _"Revert that"_ / _"Undo the last change."_

### Spatial workspace
- _"Show [panel name]"_ / _"Hide [panel name]."_
- _"Move the diff to my left."_
- _"Switch to void environment."_

### Meta
- _"What can I say?"_ — context-aware command list
- _"Explain what the agent is doing."_
- _"Stop."_ — halt current agent execution immediately

---

## Comfort and accessibility

- **Seated use is the primary mode.** All panels within comfortable neck-rotation range. No task requires looking up, down, or behind.
- **No locomotion required.** The workspace comes to the developer; they do not move through a world.
- **Text legibility is a hard constraint.** All code panels must be readable at a comfortable focal distance without leaning. Font, contrast, and panel distance are configurable.
- **Voice-only is fully supported.** Every action available by hand or gaze is also available by voice. The experience does not degrade without hand interaction.
- **Session duration limits** — Skoven is designed for focused 30–90 minute sessions. The app gently suggests a break after 90 minutes of continuous headset use.
- **Transcripts always on** — every AI response and voice recognition result is shown in text. Captions cannot be disabled.
- **Dyslexia-friendly font option** — OpenDyslexic or equivalent available in settings.

---

## Failure and recovery

| Situation | Behavior |
|---|---|
| Voice not recognized | Transcript shows what was heard; AI says _"I didn't catch that."_ Player retries. |
| Agent fails or errors | Agent log panel shows error; AI summarizes what failed and suggests next step. |
| Host connection lost | Workspace enters offline mode; panels show last known state; reconnection prompt appears. |
| Agent produces broken code | AI flags test failures; proposes a fix; developer approves or cancels. |
| Developer says "Stop" | All agent execution halts immediately. No further changes are made. |
| App suspend/resume | Session state persists. Agent execution pauses and offers to resume on return. |
| Headset battery low | 15% warning via spatial notification. Session auto-saves. |

---

## Persistence

- Workspace layout (panel positions, environment) persists per developer profile on the headset.
- Project connections and workflow configurations are stored locally and optionally synced to a cloud profile.
- Session state (active task, agent progress, open diffs) auto-saves continuously. Sessions are resumable.
- Command history is persisted per project for context carry-over between sessions.

---

## Minimum lovable release (MLR) scope

- Full voice pipeline: speak → plan → implement → review → commit → push
- Default 7-stage agent pipeline with configurable pause points
- 5 spatial panels: Chat/AI, Diff Viewer, Task Board, Agent Log, Terminal
- Panel repositioning, resize, and show/hide by voice and hand
- 3 workspace environments: Void, Studio, Exterior
- Project connection via companion app (macOS/Windows) to developer's local machine
- Support for Git + any language the connected AI model supports
- Push-to-talk and always-listening voice modes
- Full captions; dyslexia-friendly font option; configurable text size
- Session persistence and resume
- Quest 3 primary; Quest 2 tested

---

## Explicit non-goals for launch

- Direct text input or virtual keyboard (agents handle all code writing)
- Built-in code editor or IDE replacement
- Multi-user or collaborative sessions
- Cloud-hosted dev environment (local companion app only at launch)
- Mobile companion app (macOS and Windows only at launch)
- Asset creation, visual design, or non-code workflows
- Offline agent execution (requires connected host and LLM API)
- Custom environment creation

---

## Largest assumptions and open risks

| Assumption | Risk if wrong | Test |
|---|---|---|
| Developers will accept voice-only task input for complex coding tasks | Core loop fails; users revert to keyboard | Uncoached sessions with 5 working developers |
| Agent round-trip latency is acceptable for real-time conversation | Loop feels broken; developers disengage | Stage 4: measure voice → agent result displayed, P80 target < 3 s |
| Code diff panels are legible at comfortable VR reading distance | Spatial UI fails its primary job | Stage 4: readability test at 0.5–1.5 m panel distance |
| Quest 3 + Wi-Fi connection to local host is reliable enough for a work session | Connectivity issues break flow constantly | Stage 4: sustained 60-minute session test |
| Developers trust AI agents with commit/push on their real repositories | Adoption fails; developers won't hand over control | Design explicit approval gates; test with real repos in prototype |
| No direct Quest competitor with this feature set exists | Market position weaker than assumed | Stage 2 competitor audit |
| Meta Store accepts a developer productivity app in this category | Listing blocked or misfiled | Review Meta's developer tools / productivity category; check VRC for productivity apps |

---

## Decisions needed before Stage 4

- [ ] Host integration model: local companion app vs. cloud dev environment — confirm which to prototype first
- [ ] LLM provider: OpenAI GPT-4o, Anthropic Claude, or model-agnostic (user supplies API key)
- [ ] Agent framework: custom orchestration vs. existing framework (LangChain, AutoGen, CrewAI, Kiro agents)
- [ ] Pricing: subscription (most comparable productivity tools) vs. one-time purchase — validate against Meta Horizon Store norms
- [ ] App name: confirm "Skoven" is not trademarked and is available as a Meta app identifier
