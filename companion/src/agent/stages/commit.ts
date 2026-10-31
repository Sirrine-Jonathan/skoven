import type { LLMClient } from "../../llm/index.js";
import type { AgentContext, StageResult } from "../orchestrator.js";
import type { FileDiff } from "../../types/protocol.js";

export async function runCommit(
  task: string,
  diffs: FileDiff[],
  ctx: AgentContext,
  llm: LLMClient
): Promise<StageResult> {
  ctx.send({
    type: "agent_update",
    stage: "commit",
    status: "running",
    message: "Generating commit message...",
    timestamp: Date.now(),
  });

  const diffSummary = diffs
    .map((d) => `${d.status} ${d.path} (+${d.additions} -${d.deletions})`)
    .join("\n");

  const message = await llm.generateCommitMessage(task, diffSummary);

  ctx.send({
    type: "commit_ready",
    message,
    branch: await ctx.git.getCurrentBranch(),
    filesChanged: diffs.length,
    awaitingApproval: true,
  });

  ctx.send({
    type: "agent_update",
    stage: "commit",
    status: "paused",
    message: `Commit message ready: "${message}"`,
    timestamp: Date.now(),
  });

  return {
    status: "paused",
    awaitingApproval: true,
    data: { message, diffs },
    speech: `Ready to commit with message: "${message}". Say approve to commit, or dictate a different message.`,
  };
}

export async function executeCommit(
  message: string,
  ctx: AgentContext
): Promise<StageResult> {
  ctx.send({
    type: "agent_update",
    stage: "commit",
    status: "running",
    message: "Committing...",
    timestamp: Date.now(),
  });

  const hash = await ctx.git.commit(message);

  ctx.send({
    type: "agent_update",
    stage: "commit",
    status: "complete",
    message: `Committed: ${hash.slice(0, 7)} — ${message}`,
    timestamp: Date.now(),
  });

  return {
    status: "complete",
    data: { hash, message },
    speech: `Committed. Hash ${hash.slice(0, 7)}.`,
  };
}
