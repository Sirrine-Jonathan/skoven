import type { LLMClient } from "../../llm/index.js";
import type { AgentContext, StageResult } from "../orchestrator.js";

export async function runImplement(
  task: string,
  plan: string[],
  affectedFiles: string[],
  ctx: AgentContext,
  llm: LLMClient
): Promise<StageResult> {
  ctx.send({
    type: "agent_update",
    stage: "implement",
    status: "running",
    message: `Implementing ${affectedFiles.length} file(s)...`,
    timestamp: Date.now(),
  });

  // Read current content of affected files
  const fileContents: Record<string, string> = {};
  for (const filePath of affectedFiles) {
    const content = ctx.git.readFile(filePath);
    if (content !== null) {
      fileContents[filePath] = content;
    }
  }

  // Generate new file contents via LLM
  const newContents = await llm.generateImplementation(task, plan, fileContents);

  // Write files to disk
  let writtenCount = 0;
  for (const [filePath, content] of Object.entries(newContents)) {
    ctx.send({
      type: "agent_update",
      stage: "implement",
      status: "running",
      message: `Writing ${filePath}...`,
      timestamp: Date.now(),
    });
    ctx.git.writeFile(filePath, content);
    writtenCount++;
  }

  // Stage all changes and get the diff
  const diffs = await ctx.git.stageAll();

  const totalAdditions = diffs.reduce((sum, d) => sum + d.additions, 0);
  const totalDeletions = diffs.reduce((sum, d) => sum + d.deletions, 0);
  const summary = `Modified ${writtenCount} file(s): +${totalAdditions} −${totalDeletions} lines`;

  ctx.send({
    type: "diff_ready",
    diff: diffs,
    summary,
    awaitingApproval: false, // implement pause is 'never' by default
  });

  ctx.send({
    type: "agent_update",
    stage: "implement",
    status: "complete",
    message: summary,
    timestamp: Date.now(),
  });

  return {
    status: "complete",
    data: { diffs, writtenCount, totalAdditions, totalDeletions },
    speech: `Done. I've changed ${writtenCount} file${writtenCount !== 1 ? "s" : ""} — ${totalAdditions} lines added, ${totalDeletions} removed. Check the diff panel.`,
  };
}
