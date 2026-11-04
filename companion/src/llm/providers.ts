/**
 * LLM Provider registry
 *
 * Supported providers:
 *   openai      — OpenAI API (GPT-4o, GPT-4 Turbo, etc.)
 *   anthropic   — Anthropic API (Claude 3.5 Sonnet, Claude 3 Opus, etc.)
 *   openrouter  — OpenRouter.ai (proxies 100+ models: Gemini, Mistral, Llama, etc.)
 *   ollama      — Local Ollama instance (fully offline, any pulled model)
 *   custom      — Any OpenAI-compatible endpoint (LM Studio, vLLM, Together AI, etc.)
 *
 * All providers are BYOK (bring your own API key).
 * Keys are read from environment variables and never stored in project state.
 */

export type ProviderID =
  | "openai"
  | "anthropic"
  | "openrouter"
  | "ollama"
  | "custom";

export interface ProviderConfig {
  id: ProviderID;
  displayName: string;
  baseUrl: string;
  apiKeyEnvVar: string | null; // null = no key required (Ollama)
  defaultModel: string;
  supportsStreaming: boolean;
  /** true = uses the OpenAI-compatible chat completions API */
  openAICompatible: boolean;
  modelListUrl?: string; // endpoint to fetch available models
}

export const PROVIDERS: Record<ProviderID, ProviderConfig> = {
  openai: {
    id: "openai",
    displayName: "OpenAI",
    baseUrl: "https://api.openai.com/v1",
    apiKeyEnvVar: "OPENAI_API_KEY",
    defaultModel: "gpt-4o",
    supportsStreaming: true,
    openAICompatible: true,
    modelListUrl: "https://api.openai.com/v1/models",
  },

  anthropic: {
    id: "anthropic",
    displayName: "Anthropic",
    baseUrl: "https://api.anthropic.com",
    apiKeyEnvVar: "ANTHROPIC_API_KEY",
    defaultModel: "claude-3-5-sonnet-20241022",
    supportsStreaming: true,
    openAICompatible: false, // uses its own messages API format
  },

  openrouter: {
    id: "openrouter",
    displayName: "OpenRouter",
    baseUrl: "https://openrouter.ai/api/v1",
    apiKeyEnvVar: "OPENROUTER_API_KEY",
    defaultModel: "anthropic/claude-3.5-sonnet",
    supportsStreaming: true,
    openAICompatible: true, // OpenRouter mirrors the OpenAI chat completions API
    modelListUrl: "https://openrouter.ai/api/v1/models",
  },

  ollama: {
    id: "ollama",
    displayName: "Ollama (local)",
    // baseUrl overrideable via OLLAMA_BASE_URL
    baseUrl: "http://localhost:11434/v1",
    apiKeyEnvVar: null,
    defaultModel: "qwen2.5-coder:7b",
    supportsStreaming: true,
    openAICompatible: true, // Ollama exposes an OpenAI-compatible /v1 endpoint
    modelListUrl: "http://localhost:11434/api/tags",
  },

  custom: {
    id: "custom",
    displayName: "Custom endpoint",
    // baseUrl must be provided via SKOVEN_CUSTOM_LLM_BASE_URL
    baseUrl: "",
    apiKeyEnvVar: "SKOVEN_CUSTOM_LLM_API_KEY",
    defaultModel: "default",
    supportsStreaming: true,
    openAICompatible: true,
  },
};

/** Resolve runtime config, applying env var overrides */
export function resolveProviderConfig(id: ProviderID): ProviderConfig {
  const base = { ...PROVIDERS[id] };

  if (id === "ollama") {
    base.baseUrl =
      (process.env["OLLAMA_BASE_URL"]
        ? `${process.env["OLLAMA_BASE_URL"]}/v1`
        : null) ?? base.baseUrl;
    base.defaultModel =
      process.env["OLLAMA_MODEL"] ?? base.defaultModel;
  }

  if (id === "custom") {
    base.baseUrl = process.env["SKOVEN_CUSTOM_LLM_BASE_URL"] ?? "";
    base.defaultModel =
      process.env["SKOVEN_CUSTOM_LLM_MODEL"] ?? base.defaultModel;
  }

  return base;
}

/** Read the API key for a provider from the environment */
export function resolveApiKey(config: ProviderConfig): string {
  if (config.apiKeyEnvVar === null) return "ollama"; // Ollama ignores the key
  const key = process.env[config.apiKeyEnvVar];
  if (!key) {
    throw new Error(
      `Missing API key for provider "${config.displayName}". ` +
        `Set ${config.apiKeyEnvVar} in your .env file.`
    );
  }
  return key;
}

/** Well-known model suggestions per provider (non-exhaustive) */
export const SUGGESTED_MODELS: Record<ProviderID, string[]> = {
  openai: [
    "gpt-4o",
    "gpt-4o-mini",
    "gpt-4-turbo",
    "gpt-3.5-turbo",
  ],
  anthropic: [
    "claude-3-5-sonnet-20241022",
    "claude-3-5-haiku-20241022",
    "claude-3-opus-20240229",
  ],
  openrouter: [
    "anthropic/claude-3.5-sonnet",
    "openai/gpt-4o",
    "google/gemini-pro-1.5",
    "meta-llama/llama-3.1-70b-instruct",
    "mistralai/mixtral-8x7b-instruct",
    "cohere/command-r-plus",
    "deepseek/deepseek-coder",
  ],
  ollama: [
    "llama3.1:8b",
    "llama3.1:70b",
    "codellama:13b",
    "deepseek-coder-v2",
    "mistral:7b",
    "qwen2.5-coder:7b",
  ],
  custom: [],
};
