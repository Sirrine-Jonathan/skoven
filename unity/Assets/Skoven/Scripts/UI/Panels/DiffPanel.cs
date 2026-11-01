using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Skoven.Network;

namespace Skoven.UI.Panels
{
    /// <summary>
    /// Diff Viewer panel — displays code diffs with syntax-highlighted coloring.
    ///
    /// Uses TMP rich-text color tags to approximate diff syntax highlighting:
    ///   + lines → green
    ///   - lines → red
    ///   @@ lines → cyan (hunk headers)
    ///   filename headers → yellow bold
    ///   context lines → default color
    ///
    /// Scrollable via the scroll rect. Font is set to a monospace font in the
    /// Inspector (JetBrains Mono recommended).
    /// </summary>
    public class DiffPanel : FloatingPanel
    {
        [Header("UI References")]
        public TextMeshProUGUI diffTextMesh;
        public TextMeshProUGUI summaryTextMesh;
        public TextMeshProUGUI fileTabsTextMesh; // shows "file 1 of N"
        public ScrollRect scrollRect;
        public GameObject approvalBar;
        public TextMeshProUGUI approvalPromptText;

        [Header("Colors (hex)")]
        public string addedColor = "#4EC94E";
        public string removedColor = "#E05555";
        public string hunkHeaderColor = "#56D4E0";
        public string fileHeaderColor = "#E0C050";
        public string contextColor = "#CCCCCC";
        public string metaColor = "#888888";

        [Header("Settings")]
        [Tooltip("Maximum characters rendered per diff to avoid TMP vertex limits.")]
        public int maxRenderChars = 8000;

        private FileDiff[] _currentDiffs;
        private int _currentFileIndex;

        // ── Public API ─────────────────────────────────────────────────────

        public void OnDiffReady(DiffReadyMessage msg)
        {
            _currentDiffs = msg.diff;
            _currentFileIndex = 0;

            if (summaryTextMesh != null)
                summaryTextMesh.text = msg.summary;

            RenderCurrentFile();

            if (msg.awaitingApproval)
                ShowApprovalBar("Approve changes?");
        }

        public void OnVerifyResult(VerifyResultMessage msg)
        {
            // Show test results in the diff panel when no diff is active
            if (_currentDiffs != null && _currentDiffs.Length > 0) return;

            var sb = new StringBuilder();
            foreach (var r in msg.results)
            {
                string color = r.passed ? addedColor : removedColor;
                string icon = r.passed ? "✓" : "✗";
                sb.AppendLine($"<color={metaColor}>$ {Sanitize(r.command)}</color>");
                sb.AppendLine($"<color={color}>{icon} exit {r.exitCode}</color>");
                if (!string.IsNullOrEmpty(r.output))
                    sb.AppendLine($"<color={contextColor}>{Sanitize(r.output)}</color>");
                sb.AppendLine();
            }

            if (diffTextMesh != null)
                diffTextMesh.text = sb.ToString();

            if (summaryTextMesh != null)
                summaryTextMesh.text = msg.passed ? "All checks passed ✓" : "Some checks failed ✗";

            if (msg.awaitingApproval)
                ShowApprovalBar("Commit anyway?");
        }

        /// Navigate to the next file in the diff
        public void NextFile()
        {
            if (_currentDiffs == null || _currentDiffs.Length == 0) return;
            _currentFileIndex = (_currentFileIndex + 1) % _currentDiffs.Length;
            RenderCurrentFile();
        }

        /// Navigate to the previous file
        public void PrevFile()
        {
            if (_currentDiffs == null || _currentDiffs.Length == 0) return;
            _currentFileIndex = (_currentFileIndex - 1 + _currentDiffs.Length) % _currentDiffs.Length;
            RenderCurrentFile();
        }

        public void ClearDiff()
        {
            _currentDiffs = null;
            _currentFileIndex = 0;
            if (diffTextMesh != null) diffTextMesh.text = string.Empty;
            if (summaryTextMesh != null) summaryTextMesh.text = string.Empty;
            if (fileTabsTextMesh != null) fileTabsTextMesh.text = string.Empty;
            HideApprovalBar();
        }

        // ── Rendering ──────────────────────────────────────────────────────

        private void RenderCurrentFile()
        {
            if (_currentDiffs == null || _currentDiffs.Length == 0) return;

            if (fileTabsTextMesh != null)
                fileTabsTextMesh.text = $"File {_currentFileIndex + 1} / {_currentDiffs.Length}";

            var diff = _currentDiffs[_currentFileIndex];
            var rendered = RenderPatch(diff);

            if (diffTextMesh != null)
            {
                diffTextMesh.text = rendered;
                // Scroll to top when new diff is shown
                if (scrollRect != null)
                    scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private string RenderPatch(FileDiff diff)
        {
            var sb = new StringBuilder();

            // File header
            string statusIcon = diff.status switch
            {
                "added" => "++ ",
                "deleted" => "-- ",
                _ => "~~ "
            };
            sb.AppendLine($"<color={fileHeaderColor}><b>{statusIcon}{Sanitize(diff.path)}</b></color>");
            sb.AppendLine($"<color={metaColor}>+{diff.additions} -{diff.deletions}</color>");
            sb.AppendLine();

            if (string.IsNullOrEmpty(diff.patch))
            {
                sb.AppendLine($"<color={metaColor}>(no patch data)</color>");
                return sb.ToString();
            }

            int charCount = 0;
            foreach (var line in diff.patch.Split('\n'))
            {
                if (charCount > maxRenderChars)
                {
                    sb.AppendLine($"<color={metaColor}>... (truncated — use scroll or voice to navigate)</color>");
                    break;
                }

                string rendered = RenderLine(line);
                sb.AppendLine(rendered);
                charCount += rendered.Length;
            }

            return sb.ToString();
        }

        private string RenderLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            string safe = Sanitize(line);

            if (line.StartsWith("+") && !line.StartsWith("+++"))
                return $"<color={addedColor}>{safe}</color>";

            if (line.StartsWith("-") && !line.StartsWith("---"))
                return $"<color={removedColor}>{safe}</color>";

            if (line.StartsWith("@@"))
                return $"<color={hunkHeaderColor}><i>{safe}</i></color>";

            if (line.StartsWith("diff ") || line.StartsWith("index ") ||
                line.StartsWith("---") || line.StartsWith("+++"))
                return $"<color={fileHeaderColor}>{safe}</color>";

            return $"<color={contextColor}>{safe}</color>";
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
            return input?
                .Replace("<", "\u003C")
                .Replace(">", "\u003E")
                .Replace("&", "&amp;") ?? string.Empty;
        }
    }
}
