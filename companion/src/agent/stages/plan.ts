import type { LLMClient, ProjectContext } from "../../llm/index.js";
import type { AgentContext, StageResult } from "../orchestrator.js";
import type { PlanStep } from "../../types/protocol.js";

export async function runPlan(
  task: string,
  ctx: AgentContext,
  llm: LLMClient,
  projectContext: ProjectContext
): Promise<StageResult> {
  ctx.send({
    type: "agent_update",
    stage: "plan",
    status: "running",
    message: "Generating implementation plan...",
    timestamp: Date.now(),
  });

  const fileTree = (await ctx.git.getFileTree()).join("\n");
  const { steps, affectedFiles, scope } = await llm.generatePlan(
    task,
    projectContext,
    fileTree
  );

  const planSteps: PlanStep[] = steps.map((description, index) => ({
    index,
    description,
    file: affectedFiles[index],
  }));

  ctx.send({
    type: "plan_ready",
    task,
    steps: planSteps,
    affectedFiles,
    estimatedScope: scope,
    awaitingApproval: true,
  });

  ctx.send({
    type: "agent_update",
    stage: "plan",
    status: "paused",
    message: `Plan ready: ${steps.length} steps, ${affectedFiles.length} files (${scope})`,
    timestamp: Date.now(),
  });

  return {
    status: "paused",
    awaitingApproval: true,
    data: { task, steps, affectedFiles, scope },
    speech: `I have a ${scope} plan with ${steps.length} steps. Take a look and say approve when you're ready, or tell me what to change.`,
  };
}
