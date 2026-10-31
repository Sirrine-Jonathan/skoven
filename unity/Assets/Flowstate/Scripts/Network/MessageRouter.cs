using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Flowstate.Network
{
    /// <summary>
    /// Deserializes raw JSON messages from the companion app and routes
    /// them to the appropriate event listeners.
    /// </summary>
    public class MessageRouter : MonoBehaviour
    {
        [Header("Dependencies")]
        public WebSocketClient wsClient;

        // ── Events — subscribe in the Inspector or from other components ──

        public UnityEvent<HandshakeAckMessage> onHandshakeAck;
        public UnityEvent<TranscriptAckMessage> onTranscriptAck;
        public UnityEvent<AgentUpdateMessage> onAgentUpdate;
        public UnityEvent<PlanReadyMessage> onPlanReady;
        public UnityEvent<DiffReadyMessage> onDiffReady;
        public UnityEvent<VerifyResultMessage> onVerifyResult;
        public UnityEvent<CommitReadyMessage> onCommitReady;
        public UnityEvent<PrReadyMessage> onPrReady;
        public UnityEvent<AgentCompleteMessage> onAgentComplete;
        public UnityEvent<AgentErrorMessage> onAgentError;
        public UnityEvent<SessionStateMessage> onSessionState;
        public UnityEvent<SpeechMessage> onSpeech;

        private void Awake()
        {
            if (wsClient == null)
                wsClient = FindFirstObjectByType<WebSocketClient>();

            wsClient.OnMessageReceived += Route;
        }

        private void OnDestroy()
        {
            if (wsClient != null)
                wsClient.OnMessageReceived -= Route;
        }

        private void Route(string json)
        {
            if (json.Contains("\"_meta\":\"connected\"")) return; // internal sentinel

            try
            {
                var obj = JObject.Parse(json);
                var type = obj["type"]?.Value<string>();

                switch (type)
                {
                    case "handshake_ack":
                        onHandshakeAck?.Invoke(JsonConvert.DeserializeObject<HandshakeAckMessage>(json));
                        break;
                    case "transcript_ack":
                        onTranscriptAck?.Invoke(JsonConvert.DeserializeObject<TranscriptAckMessage>(json));
                        break;
                    case "agent_update":
                        onAgentUpdate?.Invoke(JsonConvert.DeserializeObject<AgentUpdateMessage>(json));
                        break;
                    case "plan_ready":
                        onPlanReady?.Invoke(JsonConvert.DeserializeObject<PlanReadyMessage>(json));
                        break;
                    case "diff_ready":
                        onDiffReady?.Invoke(JsonConvert.DeserializeObject<DiffReadyMessage>(json));
                        break;
                    case "verify_result":
                        onVerifyResult?.Invoke(JsonConvert.DeserializeObject<VerifyResultMessage>(json));
                        break;
                    case "commit_ready":
                        onCommitReady?.Invoke(JsonConvert.DeserializeObject<CommitReadyMessage>(json));
                        break;
                    case "pr_ready":
                        onPrReady?.Invoke(JsonConvert.DeserializeObject<PrReadyMessage>(json));
                        break;
                    case "agent_complete":
                        onAgentComplete?.Invoke(JsonConvert.DeserializeObject<AgentCompleteMessage>(json));
                        break;
                    case "agent_error":
                        onAgentError?.Invoke(JsonConvert.DeserializeObject<AgentErrorMessage>(json));
                        break;
                    case "session_state":
                        onSessionState?.Invoke(JsonConvert.DeserializeObject<SessionStateMessage>(json));
                        break;
                    case "speech":
                        onSpeech?.Invoke(JsonConvert.DeserializeObject<SpeechMessage>(json));
                        break;
                    default:
                        Debug.LogWarning($"[Flowstate] Unknown message type: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Flowstate] Failed to route message: {ex.Message}\nJSON: {json}");
            }
        }
    }

    // ── Message types (mirrors protocol.ts) ──────────────────────────────

    [Serializable]
    public class HandshakeAckMessage
    {
        public string type;
        public string projectName;
        public string projectPath;
        public string branch;
        public CommitSummary[] recentCommits;
        public string status;
    }

    [Serializable]
    public class CommitSummary
    {
        public string hash;
        public string message;
        public string author;
        public string date;
    }

    [Serializable]
    public class TranscriptAckMessage
    {
        public string type;
        public string text;
    }

    [Serializable]
    public class AgentUpdateMessage
    {
        public string type;
        public string stage;
        public string status;
        public string message;
        public long timestamp;
    }

    [Serializable]
    public class PlanStep
    {
        public int index;
        public string description;
        public string file;
    }

    [Serializable]
    public class PlanReadyMessage
    {
        public string type;
        public string task;
        public PlanStep[] steps;
        public string[] affectedFiles;
        public string estimatedScope;
        public bool awaitingApproval;
    }

    [Serializable]
    public class FileDiff
    {
        public string path;
        public string status;
        public string patch;
        public int additions;
        public int deletions;
    }

    [Serializable]
    public class DiffReadyMessage
    {
        public string type;
        public FileDiff[] diff;
        public string summary;
        public bool awaitingApproval;
    }

    [Serializable]
    public class VerifyStepResult
    {
        public string command;
        public int exitCode;
        public string output;
        public bool passed;
    }

    [Serializable]
    public class VerifyResultMessage
    {
        public string type;
        public bool passed;
        public VerifyStepResult[] results;
        public bool awaitingApproval;
    }

    [Serializable]
    public class CommitReadyMessage
    {
        public string type;
        public string message;
        public string branch;
        public int filesChanged;
        public bool awaitingApproval;
    }

    [Serializable]
    public class PrReadyMessage
    {
        public string type;
        public string url;
        public string title;
        public string body;
        public bool awaitingApproval;
    }

    [Serializable]
    public class AgentCompleteMessage
    {
        public string type;
        public string summary;
    }

    [Serializable]
    public class AgentErrorMessage
    {
        public string type;
        public string stage;
        public string message;
        public string suggestion;
    }

    [Serializable]
    public class SessionStateMessage
    {
        public string type;
        public string projectName;
        public string branch;
    }

    [Serializable]
    public class SpeechMessage
    {
        public string type;
        public string text;
        public string priority;
    }
}
