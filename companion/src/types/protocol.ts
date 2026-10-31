/**
 * Flowstate Wire Protocol
 *
 * All messages exchanged between the Quest app and the companion app
 * over the WebSocket connection are typed here.
 *
 * Quest → Companion: QuestMessage
 * Companion → Quest: HostMessage
 */

// ── Quest → Companion ────────────────────────────────────────────────────────

export type QuestMessage =
  | QuestHandshake
  | QuestAudioChunk
  | QuestTranscript
  | QuestApproval
  | QuestCommand
  | QuestStop;

/** First message after connection. Identifies the device and requests a project. */
export interface QuestHandshake {
  type: "handshake";
  deviceId: string;
  projectPath?: string; // optional override; host uses default if absent
}

/** Raw PCM audio chunk (16 kHz, mono, 16-bit little-endian, base64-encoded). */
export interface QuestAudioChunk {
  type: "audio_chunk";
  data: string; // base64
  isFinal: boolean; // true on PTT release — triggers STT flush
}

/**
 * Pre-transcribed text from the Quest (used when Quest-side STT is active,
 * or for desktop testing via the passthrough STT backend).
 */
export interface QuestTranscript {
  type: "transcript";
  text: string;
}

/** Developer approves or rejects the current pipeline pause point. */
export interface QuestApproval {
  type: "approval";
  decision: "approve" | "reject";
  comment?: string; // spoken revision comment on reject
}

/** Explicit workspace / session commands that bypass the agent pipeline. */
export interface QuestCommand {
  type: "command";
  action:
    | "stop"
    | "save_session"
    | "end_session"
    | "show_panel"
    | "hide_panel"
    | "switch_environment"
    | "open_project"
    | "switch_branch";
  payload?: Record<string, string>;
}

/** Hard stop — halt all agent execution immediately. */
export interface QuestStop {
  type: "stop";
}

// ── Companion → Quest ────────────────────────────────────────────────────────

export type HostMessage =
  | HostHandshakeAck
  | HostTranscriptAck
  | HostAgentUpdate
  | HostPlanReady
  | HostDiffReady
  | HostVerifyResult
  | HostCommitReady
  | HostPrReady
  | HostAgentComplete
  | HostAgentError
  | HostSessionState
  | HostSpeech;

/** Acknowledges the handshake and provides initial project context. */
export interface HostHandshakeAck {
  type: "handshake_ack";
  projectName: string;
  projectPath: string;
  branch: string;
  recentCommits: CommitSummary[];
  status: "connected" | "no_project";
}

/** Echoes the recognized transcript back to the Quest for display. */
export interface HostTranscriptAck {
  type: "transcript_ack";
  text: string;
}

/** Live progress update as a pipeline stage runs. */
export interface HostAgentUpdate {
  type: "agent_update";
  stage: PipelineStage;
  status: "running" | "paused" | "complete" | "failed";
  message: string; // human-readable log line
  timestamp: number; // ms since epoch
}

/** Plan ready — waiting for developer approval before implement. */
export interface HostPlanReady {
  type: "plan_ready";
  task: string;
  steps: PlanStep[];
  affectedFiles: string[];
  estimatedScope: "small" | "medium" | "large";
  awaitingApproval: true;
}

/** Implement complete — diff ready for review. */
export interface HostDiffReady {
  type: "diff_ready";
  diff: FileDiff[];
  summary: string; // AI-generated summary of what changed
  awaitingApproval: boolean; // false if implement pause is 'never'
}

/** Verify complete — test/lint results. */
export interface HostVerifyResult {
  type: "verify_result";
  passed: boolean;
  results: VerifyStepResult[];
  awaitingApproval: boolean; // true if pause is 'on_failure' and tests failed
}

/** Commit ready — waiting for approval of commit message. */
export interface HostCommitReady {
  type: "commit_ready";
  message: string;
  branch: string;
  filesChanged: number;
  awaitingApproval: true;
}

/** PR opened/updated. */
export interface HostPrReady {
  type: "pr_ready";
  url: string;
  title: string;
  body: string;
  awaitingApproval: boolean;
}

/** Full pipeline complete. */
export interface HostAgentComplete {
  type: "agent_complete";
  summary: string;
}

/** Unrecoverable agent error. */
export interface HostAgentError {
  type: "agent_error";
  stage: PipelineStage;
  message: string;
  suggestion?: string;
}

/** Full workspace/session state snapshot (sent on connect and on save). */
export interface HostSessionState {
  type: "session_state";
  projectName: string;
  branch: string;
  activeTasks: TaskSummary[];
  panelLayout: PanelLayout;
  environment: WorkspaceEnvironment;
}

/** AI speech for the Quest to play via TTS. */
export interface HostSpeech {
  type: "speech";
  text: string;
  priority: "normal" | "high"; // high interrupts current speech
}

// ── Shared sub-types ─────────────────────────────────────────────────────────

export type PipelineStage =
  | "understand"
  | "plan"
  | "implement"
  | "verify"
  | "commit"
  | "pr"
  | "monitor";

export interface CommitSummary {
  hash: string;
  message: string;
  author: string;
  date: string;
}

export interface PlanStep {
  index: number;
  description: string;
  file?: string;
}

export interface FileDiff {
  path: string;
  status: "added" | "modified" | "deleted";
  /** Unified diff text (the patch). Syntax-highlighted on the Quest side. */
  patch: string;
  additions: number;
  deletions: number;
}

export interface VerifyStepResult {
  command: string;
  exitCode: number;
  output: string; // trimmed stdout + stderr
  passed: boolean;
}

export interface TaskSummary {
  id: string;
  description: string;
  stage: PipelineStage;
  status: "running" | "paused" | "complete" | "failed";
}

export type WorkspaceEnvironment = "void" | "studio" | "exterior";

export interface PanelLayout {
  panels: PanelState[];
}

export interface PanelState {
  id: "chat" | "diff" | "taskboard" | "agentlog" | "terminal";
  visible: boolean;
  position: { x: number; y: number; z: number };
  rotation: { x: number; y: number; z: number; w: number };
  scale: { x: number; y: number; z: number };
}
