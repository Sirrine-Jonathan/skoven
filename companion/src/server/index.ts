/**
 * WebSocket server — Quest ↔ Companion bridge
 *
 * Listens on FLOWSTATE_PORT (default 8765).
 * Each Quest connection gets its own session: a project context, a
 * conversation history, and an AgentOrchestrator instance.
 */

import { WebSocketServer, WebSocket } from "ws";
import type { RawData } from "ws";
import type { QuestMessage, HostMessage } from "../types/protocol.js";
import { GitClient } from "../git/index.js";
import { LLMClient } from "../llm/index.js";
import { SpeechToText } from "../stt/index.js";
import { AgentOrchestrator } from "../agent/orchestrator.js";
import type { ProjectContext, ConversationTurn } from "../llm/index.js";
import { resolveProviderConfig } from "../llm/providers.js";
import type { ProviderID } from "../llm/providers.js";

// ── Session ────────────────────────────────────────────────────────────────

interface Session {
  deviceId: string;
  git: GitClient | null;
  projectContext: ProjectContext | null;
  orchestrator: AgentOrchestrator | null;
  audioBuffer: Buffer[];
  conversationHistory: ConversationTurn[];
}

// ── Server ─────────────────────────────────────────────────────────────────

export function startServer(): void {
  const port = parseInt(process.env["FLOWSTATE_PORT"] ?? "8765", 10);
  const allowedDevices = (process.env["FLOWSTATE_ALLOWED_DEVICES"] ?? "")
    .split(",")
    .map((d) => d.trim())
    .filter(Boolean);

  const stt = new SpeechToText();
  const llm = new LLMClient();

  const providerInfo = llm.getProviderInfo();
  const config = resolveProviderConfig(providerInfo.provider as ProviderID);
  console.log(
    `[Flowstate] LLM provider: ${config.displayName} / model: ${providerInfo.model}`
  );

  const wss = new WebSocketServer({ port });

  console.log(`[Flowstate] Companion app listening on ws://localhost:${port}`);
  console.log(
    `[Flowstate] Connect your Quest to the same Wi-Fi network and use this machine's IP address.`
  );
  if (allowedDevices.length > 0) {
    console.log(`[Flowstate] Allowed device IDs: ${allowedDevices.join(", ")}`);
  } else {
    console.log(
      `[Flowstate] No device allowlist configured — accepting any device (dev mode).`
    );
  }

  wss.on("connection", (ws: WebSocket) => {
    const session: Session = {
      deviceId: "",
      git: null,
      projectContext: null,
      orchestrator: null,
      audioBuffer: [],
      conversationHistory: [],
    };

    const send = (msg: HostMessage): void => {
      if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify(msg));
      }
    };

    ws.on("message", async (raw: RawData) => {
      let msg: QuestMessage;
      try {
        msg = JSON.parse(raw.toString()) as QuestMessage;
      } catch {
        console.warn("[Flowstate] Received non-JSON message — ignoring.");
        return;
      }

      try {
        await handleMessage(msg, session, send, stt, llm, allowedDevices);
      } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        console.error(`[Flowstate] Error handling message type=${msg.type}:`, message);
        send({
          type: "agent_error",
          stage: "understand",
          message: `Internal error: ${message}`,
          suggestion: "Check the companion app logs.",
        });
        send({ type: "speech", text: "Something went wrong on my end. Check the logs.", priority: "high" });
      }
    });

    ws.on("close", () => {
      if (session.deviceId) {
        console.log(`[Flowstate] Quest disconnected: ${session.deviceId}`);
      }
    });

    ws.on("error", (err) => {
      console.error("[Flowstate] WebSocket error:", err.message);
    });
  });

  wss.on("error", (err) => {
    console.error("[Flowstate] Server error:", err.message);
  });
}

// ── Message handler ────────────────────────────────────────────────────────

async function handleMessage(
  msg: QuestMessage,
  session: Session,
  send: (msg: HostMessage) => void,
  stt: SpeechToText,
  llm: LLMClient,
  allowedDevices: string[]
): Promise<void> {
  // ── Handshake ──────────────────────────────────────────────────────────

  if (msg.type === "handshake") {
    if (
      allowedDevices.length > 0 &&
      !allowedDevices.includes(msg.deviceId)
    ) {
      console.warn(
        `[Flowstate] Rejected connection from unknown device: ${msg.deviceId}`
      );
      send({
        type: "agent_error",
        stage: "understand",
        message: `Device not authorized: ${msg.deviceId}`,
        suggestion: "Add this device ID to FLOWSTATE_ALLOWED_DEVICES in .env",
      });
      return;
    }

    session.deviceId = msg.deviceId;
    console.log(`[Flowstate] Quest connected: ${msg.deviceId}`);

    const projectPath =
      msg.projectPath ??
      process.env["FLOWSTATE_DEFAULT_PROJECT"];

    if (!projectPath) {
      send({
        type: "handshake_ack",
        projectName: "(no project)",
        projectPath: "",
        branch: "",
        recentCommits: [],
        status: "no_project",
      });
      send({
        type: "speech",
        text: "Connected. No project path configured. Set FLOWSTATE_DEFAULT_PROJECT in your .env file, or open a project from the Quest.",
        priority: "normal",
      });
      return;
    }

    const git = new GitClient(projectPath);
    session.git = git;

    const [projectName, branch, recentCommits] = await Promise.all([
      git.getProjectName(),
      git.getCurrentBranch(),
      git.getRecentCommits(5),
    ]);

    const projectContext: ProjectContext = {
      projectName,
      projectPath,
      branch,
      recentCommits,
      conversationHistory: session.conversationHistory,
    };
    session.projectContext = projectContext;

    const agentCtx = { git, send };
    session.orchestrator = new AgentOrchestrator(agentCtx, llm, projectContext);

    send({
      type: "handshake_ack",
      projectName,
      projectPath,
      branch,
      recentCommits,
      status: "connected",
    });

    const lastCommit = recentCommits[0];
    const briefing = lastCommit
      ? `Connected to ${projectName} on branch ${branch}. Last commit: "${lastCommit.message}".`
      : `Connected to ${projectName} on branch ${branch}.`;

    send({ type: "speech", text: `${briefing} What would you like to work on?`, priority: "normal" });
    return;
  }

  // All subsequent messages require an initialized session
  if (!session.git || !session.orchestrator || !session.projectContext) {
    send({
      type: "speech",
      text: "No project connected. Please set up a project first.",
      priority: "high",
    });
    return;
  }

  // ── Hard stop ──────────────────────────────────────────────────────────

  if (msg.type === "stop") {
    session.orchestrator.stop();
    return;
  }

  // ── Approval / rejection ───────────────────────────────────────────────

  if (msg.type === "approval") {
    await session.orchestrator.handleApproval(msg.decision, msg.comment);
    return;
  }

  // ── Explicit workspace/session commands ────────────────────────────────

  if (msg.type === "command") {
    handleCommand(msg.action, msg.payload ?? {}, session, send);
    return;
  }

  // ── Audio chunk — accumulate until final, then transcribe ──────────────

  if (msg.type === "audio_chunk") {
    const chunkBuf = Buffer.from(msg.data, "base64");
    session.audioBuffer.push(chunkBuf);

    if (msg.isFinal) {
      const audio = Buffer.concat(session.audioBuffer);
      session.audioBuffer = [];

      let transcript: string;
      try {
        transcript = await stt.transcribe(audio);
      } catch (err) {
        const error = err instanceof Error ? err.message : String(err);
        send({ type: "speech", text: "I couldn't transcribe that. Try again.", priority: "high" });
        console.error("[Flowstate] STT error:", error);
        return;
      }

      if (!transcript.trim()) {
        send({ type: "speech", text: "I didn't catch that. Try again.", priority: "normal" });
        return;
      }

      await processTranscript(transcript, session, send, llm);
    }
    return;
  }

  // ── Pre-transcribed text (passthrough mode / desktop testing) ──────────

  if (msg.type === "transcript") {
    await processTranscript(msg.text, session, send, llm);
    return;
  }
}

// ── Transcript pipeline ───────────────────────────────────────────────────

async function processTranscript(
  transcript: string,
  session: Session,
  send: (msg: HostMessage) => void,
  _llm: LLMClient
): Promise<void> {
  if (!session.orchestrator || !session.projectContext) return;

  // Echo transcript to Quest
  send({ type: "transcript_ack", text: transcript });

  // Update conversation history
  session.conversationHistory.push({ role: "developer", content: transcript });
  if (session.conversationHistory.length > 20) {
    session.conversationHistory = session.conversationHistory.slice(-20);
  }

  // Refresh project context with latest git state + history
  const [branch, recentCommits] = await Promise.all([
    session.git!.getCurrentBranch(),
    session.git!.getRecentCommits(3),
  ]);

  session.projectContext = {
    ...session.projectContext,
    branch,
    recentCommits,
    conversationHistory: session.conversationHistory,
  };

  session.orchestrator.updateContext(session.projectContext);
  await session.orchestrator.processTranscript(transcript);
}

// ── Workspace/session command handler ─────────────────────────────────────

function handleCommand(
  action: string,
  payload: Record<string, string>,
  session: Session,
  send: (msg: HostMessage) => void
): void {
  switch (action) {
    case "stop":
      session.orchestrator?.stop();
      break;

    case "save_session":
      send({
        type: "session_state",
        projectName: session.projectContext?.projectName ?? "",
        branch: session.projectContext?.branch ?? "",
        activeTasks: [],
        panelLayout: { panels: [] },
        environment: "void",
      });
      send({ type: "speech", text: "Session saved.", priority: "normal" });
      break;

    case "switch_branch":
      if (payload["branch"] && session.git) {
        session.git
          .switchBranch(payload["branch"])
          .then(() => {
            send({ type: "speech", text: `Switched to branch ${payload["branch"]}.`, priority: "normal" });
          })
          .catch((err: Error) => {
            send({ type: "speech", text: `Failed to switch branch: ${err.message}`, priority: "high" });
          });
      }
      break;

    default:
      // Panel show/hide, environment changes, etc. are handled entirely
      // on the Quest side — companion just acknowledges.
      console.log(`[Flowstate] Workspace command: ${action}`, payload);
      break;
  }
}
