import type { LLMClient, ParsedIntent, ProjectContext } from "../../llm/index.js";
import type { AgentContext, StageResult } from "../orchestrator.js";

export async function runUnderstand(
  intent: ParsedIntent,
  ctx: AgentContext,
  llm: LLMClient,
  projectContext: ProjectContext
): Promise<StageResult> {
  ctx.send({
    type: "agent_update",
    stage: "understand",
    status: "running",
    message: `Processing: "${intent.understood}"`,
    timestamp: Date.now(),
  });

  // Queries are answered directly — no implementation pipeline needed
  if (intent.type === "query" && intent.question) {
    const fileTree = (await ctx.git.getFileTree()).slice(0, 50).join("\n");
    const answer = await llm.answerQuery(
      intent.question,
      projectContext,
      `File tree:\n${fileTree}`
    );

    ctx.send({
      type: "agent_update",
      stage: "understand",
      status: "complete",
      message: "Query answered",
      timestamp: Date.now(),
    });

    return { status: "query_answered", data: {}, speech: answer };
  }

  // Clarifications pause and wait for the developer to respond
  if (intent.type === "clarification") {
    ctx.send({
      type: "agent_update",
      stage: "understand",
      status: "paused",
      message: "Needs clarification",
      timestamp: Date.now(),
    });

    return {
      status: "paused",
      data: {},
      speech:
        intent.clarificationQuestion ??
        "Could you clarify what you'd like to do?",
      awaitingApproval: true,
    };
  }

  // Well-defined task — proceed
  const task = intent.task ?? intent.understood;

  ctx.send({
    type: "agent_update",
    stage: "understand",
    status: "complete",
    message: `Task understood: ${task}`,
    timestamp: Date.now(),
  });

  return {
    status: "complete",
    data: { task },
    speech: intent.speech,
  };
}
