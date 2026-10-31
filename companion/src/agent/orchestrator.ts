/**
 * Agent Orchestrator
 *
 * Runs the 7-stage pipeline sequentially:
 *   understand → plan → implement → verify → commit → pr → monitor
 *
 * Each stage can pause and wait for developer approval before continuing.
 * "Stop" can interrupt execution at any point.
 */

import type { HostMessage, FileDiff } from "../types/protocol.js";
import type { LLMClient, ParsedIntent, ProjectContext } from "../llm/index.js";
import type { GitClient } from "../git/index.js";
import type { WorkflowConfig, ShellStep } from "../types/workflow.js";
import { DEFAULT_WORKFLOW } from "../types/workflow.js";
import { runUnderstand } from "./stages/understand.js";
import { runPlan } from "./stages/plan.js";
import { runImplement } from "./stages/implement.js";
import { runVerify } from "./stages/verify.js";
import { runCommit, executeCommit } from "./stages/commit.js";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface AgentContext {
  git: GitClient;
  send: (msg: HostMessage) => void;
}

export interface StageResult {
  status: "complete" | "paused" | "failed" | "query_answered" | "stopped";
  awaitingApproval?: boolean;
  data: Record<string, unknown>;
  speech: string;
}

type PipelineState =
  | { phase: "idle" }
  | { phase: "understand"; intent: ParsedIntent }
  | { phase: "plan"; task: string }
  | {
      phase: "plan_approved";
      task: string;
      steps: string[];
      affectedFiles: string[];
    }
  | {
      phase: "implement";
      task: string;
      steps: string[];
      affectedFiles: string[];
    }
  | {
      phase: "verify";
      task: string;
      diffs: FileDiff[];
      verifySteps: ShellStep[];
    }
  | { phase: "commit"; task: string; diffs: FileDiff[] }
  | { phase: "commit_pending"; message: string }
  | { phase: "pr"; commitHash: string }
  | { phase: "done" };

// ── Orchestrator ─────────────────────────────────────────────────────────────

export class AgentOrchestrator {
  private state: PipelineState = { phase: "idle" };
  private stopped = false;
  private workflow: WorkflowConfig;

  constructor(
    private readonly ctx: AgentContext,
    private readonly llm: LLMClient,
    private projectContext: ProjectContext,
    workflow?: WorkflowConfig
  ) {
    this.workflow = workflow ?? DEFAULT_WORKFLOW;
  }

  /** Update the rolling conversation context */
  updateContext(projectContext: ProjectContext): void {
    this.projectContext = projectContext;
  }

  /** Hard stop — halts at the next pipeline checkpoint */
  stop(): void {
    this.stopped = true;
    this.state = { phase: "idle" };
    this.ctx.send({
      type: "agent_update",
      stage: "understand",
      status: "failed",
      message: "Stopped by developer.",
      timestamp: Date.now(),
    });
    this.speak("Stopped.", "high");
  }

  /**
   * Handle a developer approval at a pause point.
   * @param comment Optional revision comment (for reject)
   */
  async handleApproval(
    decision: "approve" | "reject",
    comment?: string
  ): Promise<void> {
    if (this.stopped) return;

    if (decision === "reject") {
      // Re-enter at the stage that was paused, with the revision comment
      await this.handleRejection(comment ?? "");
      return;
    }

    // Approve — advance to the next stage
    const current = this.state;

    if (current.phase === "plan") {
      // Plan was shown, developer approved → run implement
      // This shouldn't be reached directly; plan stage sets state to plan_approved
      return;
    }

    if (current.phase === "plan_approved") {
      await this.runImplementStage(
        current.task,
        current.steps,
        current.affectedFiles
      );
      return;
    }

    if (current.phase === "verify") {
      // Tests failed but developer approved anyway → proceed to commit
      await this.runCommitStage(current.task, current.diffs);
      return;
    }

    if (current.phase === "commit") {
      // Commit message approved
      const diffs = current.diffs;
      const task = current.task;
      // Re-generate commit message using current plan state
      await this.runCommitApproved(task, diffs);
      return;
    }

    if (current.phase === "commit_pending") {
      await this.executeCommitApproved(current.message);
      return;
    }
  }

  /**
   * Entry point: process a new developer transcript.
   */
  async processTranscript(transcript: string): Promise<void> {
    if (this.stopped) this.stopped = false;

    // Parse intent
    const intent = await this.llm.parseIntent(transcript, this.projectContext);

    // Handle non-task intents immediately
    if (intent.type === "stop") {
      this.stop();
      return;
    }

    if (intent.type === "approve") {
      await this.handleApproval("approve");
      return;
    }

    if (intent.type === "reject") {
      await this.handleApproval("reject", intent.revisionComment);
      return;
    }

    if (intent.type === "workspace" || intent.type === "session") {
      this.speak(intent.speech);
      // These are handled by the server layer, not the pipeline
      return;
    }

    // Start a new pipeline
    this.state = { phase: "understand", intent };
    await this.runUnderstandStage(intent);
  }

  // ── Pipeline stages ────────────────────────────────────────────────────────

  private async runUnderstandStage(intent: ParsedIntent): Promise<void> {
    if (this.checkStopped()) return;

    const result = await runUnderstand(
      intent,
      this.ctx,
      this.llm,
      this.projectContext
    );

    this.speak(result.speech);

    if (result.status === "query_answered" || result.status === "paused") {
      this.state = { phase: "idle" };
      return;
    }

    const task = (result.data["task"] as string | undefined) ?? intent.understood;
    this.state = { phase: "plan", task };

    const stageConfig = this.workflow.pipeline.find(
      (s) => s.stage === "plan"
    );
    await this.runPlanStage(task, stageConfig?.pause ?? "always");
  }

  private async runPlanStage(task: string, pause: string): Promise<void> {
    if (this.checkStopped()) return;

    const result = await runPlan(task, this.ctx, this.llm, this.projectContext);
    this.speak(result.speech);

    const steps = result.data["steps"] as string[];
    const affectedFiles = result.data["affectedFiles"] as string[];

    this.state = { phase: "plan_approved", task, steps, affectedFiles };

    if (pause !== "always") {
      // Auto-approve plan
      await this.runImplementStage(task, steps, affectedFiles);
    }
    // else: wait for developer approval via handleApproval()
  }

  private async runImplementStage(
    task: string,
    steps: string[],
    affectedFiles: string[]
  ): Promise<void> {
    if (this.checkStopped()) return;

    this.state = { phase: "implement", task, steps, affectedFiles };

    const result = await runImplement(
      task,
      steps,
      affectedFiles,
      this.ctx,
      this.llm
    );

    this.speak(result.speech);

    const diffs = result.data["diffs"] as FileDiff[];

    // Determine verify steps from workflow config
    const verifyConfig = this.workflow.pipeline.find(
      (s) => s.stage === "verify"
    );
    const verifySteps = verifyConfig?.steps ?? [];

    this.state = { phase: "verify", task, diffs, verifySteps };

    if (verifySteps.length === 0) {
      // No verify steps configured — skip straight to commit
      await this.runCommitStage(task, diffs);
      return;
    }

    await this.runVerifyStage(task, diffs, verifySteps, verifyConfig?.pause ?? "on_failure");
  }

  private async runVerifyStage(
    task: string,
    diffs: FileDiff[],
    steps: ShellStep[],
    pause: string
  ): Promise<void> {
    if (this.checkStopped()) return;

    const result = await runVerify(steps, this.ctx, this.ctx.git["repoPath"] as string);
    this.speak(result.speech);

    const allPassed = result.data["allPassed"] as boolean;

    if (!allPassed && pause === "on_failure") {
      this.state = { phase: "commit", task, diffs };
      return; // wait for developer approval
    }

    await this.runCommitStage(task, diffs);
  }

  private async runCommitStage(task: string, diffs: FileDiff[]): Promise<void> {
    if (this.checkStopped()) return;

    this.state = { phase: "commit", task, diffs };
    const result = await runCommit(task, diffs, this.ctx, this.llm);
    this.speak(result.speech);

    const message = result.data["message"] as string;
    this.state = { phase: "commit_pending", message };
    // Await developer approval via handleApproval()
  }

  private async runCommitApproved(task: string, diffs: FileDiff[]): Promise<void> {
    const result = await runCommit(task, diffs, this.ctx, this.llm);
    const message = result.data["message"] as string;
    this.state = { phase: "commit_pending", message };
    this.speak(result.speech);
  }

  private async executeCommitApproved(message: string): Promise<void> {
    if (this.checkStopped()) return;

    const result = await executeCommit(message, this.ctx);
    this.speak(result.speech);

    this.state = { phase: "done" };

    // Check if push is configured
    const prConfig = this.workflow.pipeline.find((s) => s.stage === "pr");
    if (prConfig && prConfig.pause === "always") {
      this.speak("Changes committed. Should I push and open a pull request?");
    } else {
      await this.runPushStage();
    }
  }

  private async runPushStage(): Promise<void> {
    if (this.checkStopped()) return;

    this.ctx.send({
      type: "agent_update",
      stage: "pr",
      status: "running",
      message: "Pushing to remote...",
      timestamp: Date.now(),
    });

    try {
      await this.ctx.git.push();
      this.ctx.send({
        type: "agent_update",
        stage: "pr",
        status: "complete",
        message: "Pushed to remote.",
        timestamp: Date.now(),
      });
      this.ctx.send({
        type: "agent_complete",
        summary: "Changes pushed. Open a PR from GitHub or say 'open a pull request'.",
      });
      this.speak("Pushed. The branch is on the remote. Say 'open a pull request' when you're ready.");
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      this.ctx.send({
        type: "agent_error",
        stage: "pr",
        message: `Push failed: ${message}`,
        suggestion: "Check your remote configuration and try again.",
      });
      this.speak(`Push failed: ${message}`);
    }

    this.state = { phase: "idle" };
  }

  // ── Rejection handling ─────────────────────────────────────────────────────

  private async handleRejection(comment: string): Promise<void> {
    const current = this.state;

    if (current.phase === "plan" || current.phase === "plan_approved") {
      // Re-run plan with the revision comment appended to the task
      const revisedTask = comment
        ? `${current.task} — revision: ${comment}`
        : current.task;
      this.speak("Got it, I'll revise the plan.");
      await this.runPlanStage(revisedTask, "always");
      return;
    }

    if (current.phase === "commit" || current.phase === "commit_pending") {
      const message =
        current.phase === "commit_pending" ? current.message : "";
      const revised = comment || message;
      this.speak(`Okay, using: "${revised}"`);
      this.state = { phase: "commit_pending", message: revised };
      return;
    }

    this.speak("Noted. Let me know what you'd like to do instead.");
    this.state = { phase: "idle" };
  }

  // ── Utilities ──────────────────────────────────────────────────────────────

  private checkStopped(): boolean {
    return this.stopped;
  }

  private speak(text: string, priority: "normal" | "high" = "normal"): void {
    this.ctx.send({ type: "speech", text, priority });
  }

  getPhase(): string {
    return this.state.phase;
  }
}
