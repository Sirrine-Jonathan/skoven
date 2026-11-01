/**
 * Workflow configuration types — mirrors .skoven/workflow.yml
 */

export interface WorkflowConfig {
  version: 1;
  pipeline: StageConfig[];
  preferences: WorkflowPreferences;
}

export type PausePolicy = "always" | "never" | "on_failure" | "on_ambiguity";

export interface StageConfig {
  stage: string;
  pause: PausePolicy;
  steps?: ShellStep[]; // for verify stage
  auto_approve_if?: AutoApproveCondition;
  timeout_ms?: number;
}

export interface ShellStep {
  run: string;
}

export interface AutoApproveCondition {
  branch_pattern?: string; // glob pattern
  tests_pass?: boolean;
}

export interface WorkflowPreferences {
  llm: "openai" | "anthropic" | "ollama";
  model: string;
  voice_mode: "push_to_talk" | "always_listening";
  auto_save_session: boolean;
}

export const DEFAULT_WORKFLOW: WorkflowConfig = {
  version: 1,
  pipeline: [
    { stage: "understand", pause: "on_ambiguity" },
    { stage: "plan", pause: "always" },
    { stage: "implement", pause: "never" },
    {
      stage: "verify",
      pause: "on_failure",
      steps: [{ run: "npm test" }, { run: "npm run lint" }],
    },
    { stage: "commit", pause: "always" },
    { stage: "pr", pause: "always" },
    { stage: "monitor", pause: "on_failure" },
  ],
  preferences: {
    llm: "openai",
    model: "gpt-4o",
    voice_mode: "push_to_talk",
    auto_save_session: true,
  },
};
