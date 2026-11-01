using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Skoven.Network;

namespace Skoven.UI.Panels
{
    /// <summary>
    /// Agent Log panel — streams step-by-step agent execution updates,
    /// timestamps, and errors in real time.
    /// </summary>
    public class AgentLogPanel : FloatingPanel
    {
        [Header("UI References")]
        public TextMeshProUGUI logTextMesh;
        public ScrollRect scrollRect;

        [Header("Colors (hex)")]
        public string runningColor = "#AAAAAA";
        public string completeColor = "#4EC94E";
        public string pausedColor = "#FFD080";
        public string failedColor = "#E05555";
        public string timestampColor = "#666666";

        [Header("Settings")]
        public int maxEntries = 200;

        private readonly List<string> _entries = new();

        // ── Public API ─────────────────────────────────────────────────────

        public void OnAgentUpdate(AgentUpdateMessage msg)
        {
            string color = msg.status switch
            {
                "complete" => completeColor,
                "paused"   => pausedColor,
                "failed"   => failedColor,
                _          => runningColor
            };

            string icon = msg.status switch
            {
                "complete" => "✓",
                "paused"   => "⏸",
                "failed"   => "✗",
                _          => "▶"
            };

            var ts = System.DateTimeOffset.FromUnixTimeMilliseconds(msg.timestamp)
                .ToLocalTime()
                .ToString("HH:mm:ss");

            string entry =
                $"<color={timestampColor}>{ts}</color> " +
                $"<color={color}>{icon} <b>[{msg.stage}]</b> {Sanitize(msg.message)}</color>";

            AddEntry(entry);
        }

        public void OnAgentError(AgentErrorMessage msg)
        {
            AddEntry($"<color={failedColor}>✗ <b>[{msg.stage}]</b> {Sanitize(msg.message)}</color>");
            if (!string.IsNullOrEmpty(msg.suggestion))
                AddEntry($"<color={runningColor}>  → {Sanitize(msg.suggestion)}</color>");
        }

        public void OnAgentComplete(AgentCompleteMessage msg)
        {
            AddEntry($"<color={completeColor}>── Pipeline complete ── {Sanitize(msg.summary)}</color>");
        }

        public void Clear()
        {
            _entries.Clear();
            if (logTextMesh != null) logTextMesh.text = string.Empty;
        }

        // ── Internal ───────────────────────────────────────────────────────

        private void AddEntry(string entry)
        {
            _entries.Add(entry);
            if (_entries.Count > maxEntries)
                _entries.RemoveAt(0);

            if (logTextMesh != null)
            {
                logTextMesh.text = string.Join("\n", _entries);
                // Auto-scroll to bottom
                Canvas.ForceUpdateCanvases();
                if (scrollRect != null)
                    scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private static string Sanitize(string input) =>
            input?.Replace("<", "\u003C").Replace(">", "\u003E") ?? string.Empty;
    }
}
