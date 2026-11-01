using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Skoven.Network;

namespace Skoven.UI.Panels
{
    /// <summary>
    /// Task Board panel — shows the current task, pipeline stage progress
    /// bar, and the list of plan steps.
    /// </summary>
    public class TaskBoardPanel : FloatingPanel
    {
        [Header("UI References")]
        public TextMeshProUGUI taskTitleText;
        public TextMeshProUGUI currentStageText;
        public TextMeshProUGUI stepsText;
        public Slider progressSlider;
        public TextMeshProUGUI projectInfoText;

        [Header("Stage Colors")]
        public Color stageRunningColor = new(0.67f, 0.84f, 1f);
        public Color stageDoneColor = new(0.3f, 0.8f, 0.3f);
        public Color stagePausedColor = new(1f, 0.82f, 0.3f);
        public Color stageFailedColor = new(0.88f, 0.33f, 0.33f);

        private static readonly string[] StageOrder =
            { "understand", "plan", "implement", "verify", "commit", "pr", "monitor" };

        // ── Public API ─────────────────────────────────────────────────────

        public void OnHandshakeAck(HandshakeAckMessage msg)
        {
            if (projectInfoText != null)
                projectInfoText.text = $"{msg.projectName}  /  {msg.branch}";
        }

        public void OnPlanReady(PlanReadyMessage msg)
        {
            if (taskTitleText != null)
                taskTitleText.text = Truncate(msg.task, 80);

            if (stepsText != null && msg.steps != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var step in msg.steps)
                    sb.AppendLine($"  {step.index + 1}. {step.description}");
                stepsText.text = sb.ToString();
            }
        }

        public void OnAgentUpdate(AgentUpdateMessage msg)
        {
            UpdateStageDisplay(msg.stage, msg.status);
        }

        public void OnAgentComplete(AgentCompleteMessage msg)
        {
            if (currentStageText != null)
                currentStageText.text = "Complete ✓";
            if (progressSlider != null)
                progressSlider.value = 1f;
        }

        // ── Internal ───────────────────────────────────────────────────────

        private void UpdateStageDisplay(string stage, string status)
        {
            // Update progress bar
            int stageIndex = System.Array.IndexOf(StageOrder, stage);
            if (stageIndex >= 0 && progressSlider != null)
                progressSlider.value = (float)(stageIndex + 1) / StageOrder.Length;

            // Update stage label
            if (currentStageText != null)
            {
                Color color = status switch
                {
                    "complete" => stageDoneColor,
                    "paused"   => stagePausedColor,
                    "failed"   => stageFailedColor,
                    _          => stageRunningColor
                };
                currentStageText.color = color;
                currentStageText.text = $"{Capitalize(stage)}: {status}";
            }
        }

        private static string Truncate(string s, int max) =>
            s?.Length > max ? s[..max] + "…" : s ?? string.Empty;

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
    }
}
