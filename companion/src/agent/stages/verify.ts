import { exec } from "child_process";
import { promisify } from "util";
import type { AgentContext, StageResult } from "../orchestrator.js";
import type { VerifyStepResult } from "../../types/protocol.js";
import type { ShellStep } from "../../types/workflow.js";

const execAsync = promisify(exec);

/** Commands that are never allowed regardless of configuration. */
const ALWAYS_BLOCKED = [
  "rm -rf",
  "git push --force",
  "git push -f",
  "git branch -D",
  "git branch -d",
  "drop table",
  "DROP TABLE",
  "drop database",
  "DROP DATABASE",
  "format c:",
  "mkfs",
  "dd if=",
];

function isCommandAllowed(command: string, allowlist: string[]): boolean {
  const lower = command.toLowerCase();
  for (const blocked of ALWAYS_BLOCKED) {
    if (lower.includes(blocked.toLowerCase())) return false;
  }
  return allowlist.some((allowed) =>
    command.trim().startsWith(allowed.trim())
  );
}

export async function runVerify(
  steps: ShellStep[],
  ctx: AgentContext,
  repoPath: string
): Promise<StageResult> {
  const allowedCommands = (
    process.env["SKOVEN_ALLOWED_COMMANDS"] ?? ""
  )
    .split(",")
    .map((c) => c.trim())
    .filter(Boolean);

  const results: VerifyStepResult[] = [];
  let allPassed = true;

  for (const step of steps) {
    if (!isCommandAllowed(step.run, allowedCommands)) {
      const result: VerifyStepResult = {
        command: step.run,
        exitCode: -1,
        output: `Command blocked: "${step.run}" is not in the allowed commands list.`,
        passed: false,
      };
      results.push(result);
      allPassed = false;

      ctx.send({
        type: "agent_update",
        stage: "verify",
        status: "running",
        message: `BLOCKED: ${step.run}`,
        timestamp: Date.now(),
      });
      continue;
    }

    ctx.send({
      type: "agent_update",
      stage: "verify",
      status: "running",
      message: `Running: ${step.run}`,
      timestamp: Date.now(),
    });

    try {
      const { stdout, stderr } = await execAsync(step.run, {
        cwd: repoPath,
        timeout: 120_000, // 2 minute cap per command
      });

      const output = [stdout, stderr].filter(Boolean).join("\n").slice(0, 4000);
      results.push({ command: step.run, exitCode: 0, output, passed: true });
    } catch (err: unknown) {
      const error = err as { code?: number; stdout?: string; stderr?: string };
      const output = [error.stdout, error.stderr]
        .filter(Boolean)
        .join("\n")
        .slice(0, 4000);

      results.push({
        command: step.run,
        exitCode: error.code ?? 1,
        output,
        passed: false,
      });
      allPassed = false;
    }
  }

  const passCount = results.filter((r) => r.passed).length;
  const summary = allPassed
    ? `All ${results.length} check${results.length !== 1 ? "s" : ""} passed.`
    : `${results.length - passCount} of ${results.length} check${results.length !== 1 ? "s" : ""} failed.`;

  ctx.send({
    type: "verify_result",
    passed: allPassed,
    results,
    awaitingApproval: !allPassed,
  });

  ctx.send({
    type: "agent_update",
    stage: "verify",
    status: allPassed ? "complete" : "paused",
    message: summary,
    timestamp: Date.now(),
  });

  return {
    status: allPassed ? "complete" : "paused",
    awaitingApproval: !allPassed,
    data: { results, allPassed },
    speech: allPassed
      ? summary
      : `${summary} Check the agent log for details. Say approve to commit anyway, or tell me what to fix.`,
  };
}
