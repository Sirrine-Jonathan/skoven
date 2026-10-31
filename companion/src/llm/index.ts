/**
 * LLM intent parsing and generation layer
 *
 * Provider-agnostic. Supports:
 *   openai     — OpenAI API (GPT-4o etc.)
 *   anthropic  — Anthropic Claude API (native messages format)
 *   openrouter — OpenRouter.ai (100+ models via OpenAI-compatible API)
 *   ollama     — Local Ollama instance (OpenAI-compatible /v1 endpoint)
 *   custom     — Any OpenAI-compatible endpoint (LM Studio, vLLM, Together AI, etc.)
 *
 * All providers are BYOK. API keys are read from env and never stored elsewhere.
 */

import OpenAI from "openai";
import Anthropic from "@anthropic-ai/sdk";
import type { CommitSummary } from "../types/protocol.js";
import {
  type ProviderID,
  resolveProviderConfig,
  resolveApiKey,
} from "./providers.js";

export type { ProviderID };

export interface ProjectContext {
  projectName: string;
  projectPath: string;
  branch: string;
  recentCommits: CommitSummary[];
  openFiles?: string[];
  conversationHistory: ConversationTurn[];
}

export interface ConversationTurn {
  role: "developer" | "assistant";
  content: string;
}

export type IntentType =
  | "task"
  | "query"
  | "approve"
  | "reject"
  | "stop"
  | "workspace"
  | "session"
  | "clarification";

export interface ParsedIntent {
  type: IntentType;
  understood: string;
  task?: string;
  question?: string;
  revisionComment?: string;
  workspaceAction?: string;
  workspacePayload?: Record<string, string>;
  sessionAction?: string;
  sessionPayload?: Record<string, string>;
  clarificationQuestion?: string;
  speech: string;
}

// ── System prompt ─────────────────────────────────────────────────────────────

const INTENT_SYSTEM_PROMPT = `You are the Flowstate AI assistant — a voice-driven coding companion inside a VR workspace.

Parse the developer's spoken command and return a structured JSON intent.

INTENT TYPES:
- "task": code change request (feature, fix, refactor). Extract task description.
- "query": information request (status, explanation, what changed). Extract question.
- "approve": developer approves the current action ("yes", "approve", "looks good", "ship it", "do it", "go ahead").
- "reject": developer rejects and wants changes ("no", "change", "try again", "actually"). Extract revision.
- "stop": halt agent execution ("stop", "cancel", "abort", "never mind").
- "workspace": spatial UI command (show/hide panels, switch environment, move panels).
- "session": session management (save, end session, switch project, switch branch).
- "clarification": genuinely ambiguous — ask ONE specific question.

RULES:
1. Return valid JSON only — no markdown, no code fences.
2. "speech" is spoken aloud: 1-2 sentences, natural, second person.
3. Prefer "task" over "clarification" — make reasonable assumptions.
4. Words like "yes", "approve", "ship it", "looks good", "do it" → always type "approve".
5. "stop", "cancel", "abort" → always type "stop".
6. Never mention JSON, schemas, or internal mechanics in "speech".

RESPONSE SCHEMA:
{
  "type": "<intent_type>",
  "understood": "<brief description>",
  "task": "<normalized task — only for type=task>",
  "question": "<question — only for type=query>",
  "revisionComment": "<revision — only for type=reject>",
  "workspaceAction": "<action — only for type=workspace>",
  "workspacePayload": {},
  "sessionAction": "<action — only for type=session>",
  "sessionPayload": {},
  "clarificationQuestion": "<question — only for type=clarification>",
  "speech": "<spoken response>"
}`;

// ── LLM Client ────────────────────────────────────────────────────────────────

export class LLMClient {
  private readonly providerId: ProviderID;
  private readonly model: string;
  private openaiClient: OpenAI | null = null;
  private anthropicClient: Anthropic | null = null;

  constructor() {
    this.providerId = (process.env["FLOWSTATE_LLM_PROVIDER"] ??
      "openai") as ProviderID;
    this.model =
      process.env["FLOWSTATE_LLM_MODEL"] ??
      resolveProviderConfig(this.providerId).defaultModel;

    this.initClient();
  }

  private initClient(): void {
    const config = resolveProviderConfig(this.providerId);
    const apiKey = resolveApiKey(config);

    if (config.openAICompatible) {
      // OpenAI, OpenRouter, Ollama, and custom all use the OpenAI SDK
      // pointed at different base URLs.
      const clientOptions: ConstructorParameters<typeof OpenAI>[0] = {
        apiKey,
        baseURL: config.baseUrl,
      };

      // OpenRouter requires these headers for usage tracking / rate limits
      if (this.providerId === "openrouter") {
        clientOptions.defaultHeaders = {
          "HTTP-Referer": "https://github.com/Sirrine-Jonathan/flowstate",
          "X-Title": "Flowstate VR Coding Companion",
        };
      }

      this.openaiClient = new OpenAI(clientOptions);
    } else if (this.providerId === "anthropic") {
      this.anthropicClient = new Anthropic({ apiKey });
    }
  }

  // ── Public API ─────────────────────────────────────────────────────────────

  async parseIntent(
    transcript: string,
    context: ProjectContext
  ): Promise<ParsedIntent> {
    const userMessage = buildContextMessage(transcript, context);
    const history = context.conversationHistory.slice(-8);

    if (this.anthropicClient) {
      return this.parseWithAnthropic(userMessage, history);
    }
    return this.parseWithOpenAICompat(userMessage, history);
  }

  async answerQuery(
    question: string,
    context: ProjectContext,
    additionalContext: string
  ): Promise<string> {
    const systemPrompt = `You are a helpful coding assistant. Answer concisely (2-4 sentences max).
Project: ${context.projectName} on branch ${context.branch}.
${additionalContext}`;

    if (this.anthropicClient) {
      const msg = await this.anthropicClient.messages.create({
        model: this.model,
        max_tokens: 400,
        system: systemPrompt,
        messages: [{ role: "user", content: question }],
      });
      const block = msg.content[0];
      return block?.type === "text" ? block.text.trim() : "I'm not sure.";
    }

    const resp = await this.requireOpenAI().chat.completions.create({
      model: this.model,
      messages: [
        { role: "system", content: systemPrompt },
        { role: "user", content: question },
      ],
      max_tokens: 400,
      temperature: 0.3,
    });
    return resp.choices[0]?.message.content?.trim() ?? "I'm not sure.";
  }

  async generatePlan(
    task: string,
    context: ProjectContext,
    fileTree: string
  ): Promise<{
    steps: string[];
    affectedFiles: string[];
    scope: "small" | "medium" | "large";
  }> {
    const systemPrompt = `You are a senior software engineer. Create a concise implementation plan.
Return JSON only:
{
  "steps": ["step 1", "step 2", ...],
  "affectedFiles": ["src/foo.ts", ...],
  "scope": "small" | "medium" | "large"
}
Project: ${context.projectName}, branch: ${context.branch}
File tree:
${fileTree}`;

    let content: string;

    if (this.anthropicClient) {
      const msg = await this.anthropicClient.messages.create({
        model: this.model,
        max_tokens: 600,
        system: systemPrompt,
        messages: [{ role: "user", content: `Task: ${task}` }],
      });
      const block = msg.content[0];
      content = block?.type === "text" ? block.text : "{}";
    } else {
      const resp = await this.requireOpenAI().chat.completions.create({
        model: this.model,
        messages: [
          { role: "system", content: systemPrompt },
          { role: "user", content: `Task: ${task}` },
        ],
        max_tokens: 600,
        temperature: 0.2,
        response_format: { type: "json_object" },
      });
      content = resp.choices[0]?.message.content ?? "{}";
    }

    const parsed = JSON.parse(content) as {
      steps?: string[];
      affectedFiles?: string[];
      scope?: "small" | "medium" | "large";
    };

    return {
      steps: parsed.steps ?? [],
      affectedFiles: parsed.affectedFiles ?? [],
      scope: parsed.scope ?? "small",
    };
  }

  async generateImplementation(
    task: string,
    plan: string[],
    fileContents: Record<string, string>
  ): Promise<Record<string, string>> {
    const filesSection = Object.entries(fileContents)
      .map(([p, c]) => `### ${p}\n\`\`\`\n${c}\n\`\`\``)
      .join("\n\n");

    const systemPrompt = `You are a senior software engineer implementing a coding task.
Return JSON only — a map of file paths to their COMPLETE new contents:
{ "src/foo.ts": "...full file content..." }
Only include files that need to change. Return complete files, not diffs.`;

    const userPrompt = `Task: ${task}

Plan:
${plan.map((s, i) => `${i + 1}. ${s}`).join("\n")}

Current files:
${filesSection}`;

    let content: string;

    if (this.anthropicClient) {
      const msg = await this.anthropicClient.messages.create({
        model: this.model,
        max_tokens: 4096,
        system: systemPrompt,
        messages: [{ role: "user", content: userPrompt }],
      });
      const block = msg.content[0];
      content = block?.type === "text" ? block.text : "{}";
    } else {
      const resp = await this.requireOpenAI().chat.completions.create({
        model: this.model,
        messages: [
          { role: "system", content: systemPrompt },
          { role: "user", content: userPrompt },
        ],
        max_tokens: 4096,
        temperature: 0.1,
        response_format: { type: "json_object" },
      });
      content = resp.choices[0]?.message.content ?? "{}";
    }

    return JSON.parse(content) as Record<string, string>;
  }

  async generateCommitMessage(
    task: string,
    diffSummary: string
  ): Promise<string> {
    const systemPrompt =
      "Generate a single Conventional Commit message (type(scope): description). No body. Under 72 characters.";
    const userPrompt = `Task: ${task}\n\nChanges:\n${diffSummary}`;

    if (this.anthropicClient) {
      const msg = await this.anthropicClient.messages.create({
        model: this.model,
        max_tokens: 80,
        system: systemPrompt,
        messages: [{ role: "user", content: userPrompt }],
      });
      const block = msg.content[0];
      return block?.type === "text"
        ? block.text.trim()
        : `feat: ${task.slice(0, 60)}`;
    }

    const resp = await this.requireOpenAI().chat.completions.create({
      model: this.model,
      messages: [
        { role: "system", content: systemPrompt },
        { role: "user", content: userPrompt },
      ],
      max_tokens: 80,
      temperature: 0.2,
    });
    return (
      resp.choices[0]?.message.content?.trim() ?? `feat: ${task.slice(0, 60)}`
    );
  }

  // ── Provider info ──────────────────────────────────────────────────────────

  getProviderInfo(): { provider: ProviderID; model: string; displayName: string } {
    const config = resolveProviderConfig(this.providerId);
    return {
      provider: this.providerId,
      model: this.model,
      displayName: config.displayName,
    };
  }

  // ── Internal helpers ───────────────────────────────────────────────────────

  private requireOpenAI(): OpenAI {
    if (!this.openaiClient) {
      throw new Error(`No OpenAI-compatible client for provider ${this.providerId}`);
    }
    return this.openaiClient;
  }

  private async parseWithOpenAICompat(
    userMessage: string,
    history: ConversationTurn[]
  ): Promise<ParsedIntent> {
    const client = this.requireOpenAI();

    const messages: OpenAI.Chat.ChatCompletionMessageParam[] = [
      { role: "system", content: INTENT_SYSTEM_PROMPT },
      ...history.map((t) => ({
        role: (t.role === "developer" ? "user" : "assistant") as
          | "user"
          | "assistant",
        content: t.content,
      })),
      { role: "user", content: userMessage },
    ];

    // response_format: json_object not universally supported by all
    // OpenRouter models — attempt it, fall back to plain text parse on error.
    let content: string;
    try {
      const resp = await client.chat.completions.create({
        model: this.model,
        messages,
        max_tokens: 400,
        temperature: 0.1,
        response_format: { type: "json_object" },
      });
      content = resp.choices[0]?.message.content ?? "{}";
    } catch {
      // Retry without response_format for models that don't support it
      const resp = await client.chat.completions.create({
        model: this.model,
        messages: [
          ...messages.slice(0, -1),
          {
            role: "user",
            content: userMessage + "\n\nRespond with JSON only, no markdown.",
          },
        ],
        max_tokens: 400,
        temperature: 0.1,
      });
      content = resp.choices[0]?.message.content ?? "{}";
    }

    // Strip potential markdown code fences from models that ignore instructions
    content = content.replace(/^```json?\s*/m, "").replace(/```\s*$/m, "").trim();

    return JSON.parse(content) as ParsedIntent;
  }

  private async parseWithAnthropic(
    userMessage: string,
    history: ConversationTurn[]
  ): Promise<ParsedIntent> {
    if (!this.anthropicClient) throw new Error("Anthropic client not initialized");

    const messages: Anthropic.MessageParam[] = [
      ...history.map((t) => ({
        role: (t.role === "developer" ? "user" : "assistant") as
          | "user"
          | "assistant",
        content: t.content,
      })),
      { role: "user", content: userMessage + "\n\nRespond with JSON only." },
    ];

    const msg = await this.anthropicClient.messages.create({
      model: this.model,
      max_tokens: 400,
      system: INTENT_SYSTEM_PROMPT,
      messages,
    });

    const block = msg.content[0];
    let content = block?.type === "text" ? block.text : "{}";
    content = content.replace(/^```json?\s*/m, "").replace(/```\s*$/m, "").trim();

    return JSON.parse(content) as ParsedIntent;
  }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function buildContextMessage(
  transcript: string,
  context: ProjectContext
): string {
  const recentCommits = context.recentCommits
    .slice(0, 3)
    .map((c) => `  ${c.hash.slice(0, 7)} ${c.message}`)
    .join("\n");

  return `Project: ${context.projectName} (branch: ${context.branch})
Recent commits:
${recentCommits || "  (none)"}

Developer said: "${transcript}"`;
}
