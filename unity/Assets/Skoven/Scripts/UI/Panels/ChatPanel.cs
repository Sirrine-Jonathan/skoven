using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Skoven.Network;

namespace Skoven.UI.Panels
{
    /// <summary>
    /// Chat / AI panel — shows conversation history, transcript acknowledgements,
    /// and AI speech text in a scrollable world-space text view.
    /// </summary>
    public class ChatPanel : FloatingPanel
    {
        [Header("UI References")]
        public TextMeshProUGUI chatTextMesh;
        public TextMeshProUGUI statusTextMesh;
        public TextMeshProUGUI instructionsText;
        public GameObject voiceIndicator; // pulsing indicator when recording
        public Button talkButton;
        public Image talkButtonImage;
        public TextMeshProUGUI talkButtonText;
        public GameObject approvalBar;    // shown when awaiting approval
        public TextMeshProUGUI approvalPromptText;
        public Button approveButton;
        public Button rejectButton;

        [Header("Settings")]
        public int maxMessages = 50;
        public string developerColor = "#A8D8FF";
        public string aiColor = "#B8FFB8";
        public string systemColor = "#888888";

        private readonly List<string> _lines = new();

        protected override void Awake()
        {
            base.Awake();
            if (talkButton != null)
                talkButton.onClick.AddListener(ToggleVoiceCapture);
            if (approveButton != null)
                approveButton.onClick.AddListener(OnApproveClicked);
            if (rejectButton != null)
                rejectButton.onClick.AddListener(OnRejectClicked);
        }

        // ── Public API ─────────────────────────────────────────────────────

        public void ToggleVoiceCapture()
        {
            var vc = FindFirstObjectByType<Voice.VoiceCapture>();
            if (vc != null)
            {
                if (vc.IsRecording)
                    vc.StopCapture();
                else
                    vc.StartCapture();
            }
        }

        public void OnApproveClicked()
        {
            var ctrl = FindFirstObjectByType<Voice.VoiceInputController>();
            ctrl?.SendApproval(true);
            HideApprovalBar();
        }

        public void OnRejectClicked()
        {
            var ctrl = FindFirstObjectByType<Voice.VoiceInputController>();
            ctrl?.SendApproval(false);
            HideApprovalBar();
        }

        /// Called when the companion app echoes a recognized transcript back
        public void OnTranscriptAck(TranscriptAckMessage msg)
        {
            AddLine($"<color={developerColor}>You:</color> {Sanitize(msg.text)}");
        }

        /// Called when the companion app sends speech for TTS / display
        public void OnSpeech(SpeechMessage msg)
        {
            AddLine($"<color={aiColor}>AI:</color> {Sanitize(msg.text)}");
        }

        /// Called on handshake acknowledgement
        public void OnHandshakeAck(HandshakeAckMessage msg)
        {
            if (msg.status == "connected")
            {
                AddLine($"<color={systemColor}>Connected to <b>{msg.projectName}</b> ({msg.branch})</color>");
                if (msg.recentCommits != null && msg.recentCommits.Length > 0)
                {
                    AddLine($"<color={systemColor}>Last commit: {Sanitize(msg.recentCommits[0].message)}</color>");
                }
            }
            else
            {
                AddLine($"<color={systemColor}>No project connected. Set SKOVEN_DEFAULT_PROJECT in companion .env</color>");
            }
        }

        /// Called on agent updates (stage progress)
        public void OnAgentUpdate(AgentUpdateMessage msg)
        {
            string color = msg.status switch
            {
                "failed" => "#FF8888",
                "paused" => "#FFD080",
                "complete" => systemColor,
                _ => systemColor
            };
            AddLine($"<color={color}>[{msg.stage}] {Sanitize(msg.message)}</color>");
        }

        public void OnPlanReady(PlanReadyMessage msg)
        {
            AddLine($"<color={aiColor}>Plan for: {Sanitize(msg.task)}</color>");
            if (msg.steps != null)
            {
                foreach (var step in msg.steps)
                    AddLine($"<color={systemColor}>  {step.index + 1}. {Sanitize(step.description)}</color>");
            }
            if (msg.awaitingApproval) ShowApprovalBar("Approve plan?");
        }

        public void OnCommitReady(CommitReadyMessage msg)
        {
            AddLine($"<color={aiColor}>Commit: \"{Sanitize(msg.message)}\"</color>");
            AddLine($"<color={systemColor}>  Branch: {msg.branch} · {msg.filesChanged} file(s)</color>");
            if (msg.awaitingApproval) ShowApprovalBar("Approve commit?");
        }

        public void OnAgentComplete(AgentCompleteMessage msg)
        {
            AddLine($"<color={aiColor}>✓ {Sanitize(msg.summary)}</color>");
            HideApprovalBar();
        }

        public void OnAgentError(AgentErrorMessage msg)
        {
            AddLine($"<color=#FF8888>✗ [{msg.stage}] {Sanitize(msg.message)}</color>");
            if (!string.IsNullOrEmpty(msg.suggestion))
                AddLine($"<color={systemColor}>  Suggestion: {Sanitize(msg.suggestion)}</color>");
            HideApprovalBar();
        }

        public void SetVoiceActive(bool active)
        {
            if (voiceIndicator != null)
                voiceIndicator.SetActive(active);

            if (talkButtonText != null)
            {
                talkButtonText.text = active
                    ? "🔴 <b>Listening...</b> (Release Grip or Click to Stop)"
                    : "🎙️ <b>Push to Talk</b> (or Hold Right Grip)";
            }

            if (talkButtonImage != null)
            {
                talkButtonImage.color = active
                    ? new Color(0.85f, 0.22f, 0.22f, 0.95f)
                    : new Color(0.18f, 0.52f, 0.88f, 0.95f);
            }
        }

        // ── Internal helpers ───────────────────────────────────────────────

        private void AddLine(string line)
        {
            _lines.Add(line);
            if (_lines.Count > maxMessages)
                _lines.RemoveAt(0);

            if (chatTextMesh != null)
                chatTextMesh.text = string.Join("\n", _lines);
        }

        private void ShowApprovalBar(string prompt)
        {
            if (approvalBar != null) approvalBar.SetActive(true);
            if (approvalPromptText != null) approvalPromptText.text = prompt;
        }

        private void HideApprovalBar()
        {
            if (approvalBar != null) approvalBar.SetActive(false);
        }

        private static string Sanitize(string input)
        {
            // Escape TMP rich-text characters that aren't intentional markup
            return input?
                .Replace("<", "\u003C")
                .Replace(">", "\u003E") ?? string.Empty;
        }
    }
}
