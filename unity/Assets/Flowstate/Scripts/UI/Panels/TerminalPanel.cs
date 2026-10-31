using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flowstate.Network;

namespace Flowstate.UI.Panels
{
    /// <summary>
    /// Terminal panel — shows stdout/stderr from test runs, build output,
    /// and shell commands executed during the verify stage.
    /// </summary>
    public class TerminalPanel : FloatingPanel
    {
        [Header("UI References")]
        public TextMeshProUGUI outputTextMesh;
        public ScrollRect scrollRect;

        [Header("Colors (hex)")]
        public string promptColor = "#56D4E0";
        public string passColor = "#4EC94E";
        public string failColor = "#E05555";
        public string outputColor = "#CCCCCC";
        public string errorColor = "#FF8888";

        [Header("Settings")]
        public int maxLines = 500;

        private readonly List<string> _lines = new();

        // ── Public API ─────────────────────────────────────────────────────

        public void OnVerifyResult(VerifyResultMessage msg)
        {
            foreach (var result in msg.results)
            {
                // Print the command prompt line
                AddLine($"<color={promptColor}>$ {Sanitize(result.command)}</color>");

                // Print output lines
                if (!string.IsNullOrWhiteSpace(result.output))
                {
                    foreach (var line in result.output.Split('\n'))
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        bool isError = line.StartsWith("Error") ||
                                       line.StartsWith("error") ||
                                       line.StartsWith("FAIL") ||
                                       line.Contains("✗");
                        string color = isError ? errorColor : outputColor;
                        AddLine($"<color={color}>{Sanitize(line)}</color>");
                    }
                }

                // Print exit status
                string statusColor = result.passed ? passColor : failColor;
                string statusIcon = result.passed ? "✓ exit 0" : $"✗ exit {result.exitCode}";
                AddLine($"<color={statusColor}>{statusIcon}</color>");
                AddLine(string.Empty);
            }
        }

        public void OnAgentUpdate(AgentUpdateMessage msg)
        {
            // Only show verify-stage updates in terminal
            if (msg.stage != "verify") return;
            AddLine($"<color={promptColor}>[verify] {Sanitize(msg.message)}</color>");
        }

        public void Clear()
        {
            _lines.Clear();
            if (outputTextMesh != null) outputTextMesh.text = string.Empty;
        }

        // ── Internal ───────────────────────────────────────────────────────

        private void AddLine(string line)
        {
            _lines.Add(line);
            if (_lines.Count > maxLines)
                _lines.RemoveAt(0);

            if (outputTextMesh != null)
            {
                outputTextMesh.text = string.Join("\n", _lines);
                Canvas.ForceUpdateCanvases();
                if (scrollRect != null)
                    scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private static string Sanitize(string input) =>
            input?.Replace("<", "\u003C").Replace(">", "\u003E") ?? string.Empty;
    }
}
