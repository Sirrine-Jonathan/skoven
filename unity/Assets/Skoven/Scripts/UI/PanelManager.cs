using System.Collections.Generic;
using UnityEngine;
using Skoven.UI.Panels;

namespace Skoven.UI
{
    /// <summary>
    /// Manages show/hide and layout reset for all floating panels.
    /// Handles voice commands routed through WorkspaceCommandHandler.
    /// </summary>
    public class PanelManager : MonoBehaviour
    {
        [Header("Panels")]
        public ChatPanel chatPanel;
        public DiffPanel diffPanel;
        public TaskBoardPanel taskBoardPanel;
        public AgentLogPanel agentLogPanel;
        public TerminalPanel terminalPanel;

        private Dictionary<string, FloatingPanel> _panels;

        private void Awake()
        {
            _panels = new Dictionary<string, FloatingPanel>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "chat",      chatPanel      },
                { "diff",      diffPanel      },
                { "taskboard", taskBoardPanel },
                { "agentlog",  agentLogPanel  },
                { "terminal",  terminalPanel  },
            };
        }

        // ── Public API — called by WorkspaceCommandHandler ────────────────

        public void ShowPanel(string panelName)
        {
            if (_panels.TryGetValue(panelName, out var panel))
                panel?.Show();
            else
                Debug.LogWarning($"[Skoven] Unknown panel: {panelName}");
        }

        public void HidePanel(string panelName)
        {
            if (_panels.TryGetValue(panelName, out var panel))
                panel?.Hide();
        }

        public void TogglePanel(string panelName)
        {
            if (_panels.TryGetValue(panelName, out var panel))
                panel?.Toggle();
        }

        public void ShowAll()
        {
            foreach (var p in _panels.Values)
                p?.Show();
        }

        public void HideAll()
        {
            foreach (var p in _panels.Values)
                p?.Hide();
        }

        public void ResetAllLayouts()
        {
            foreach (var p in _panels.Values)
                p?.ResetToDefault();
        }

        public void SaveAllLayouts()
        {
            foreach (var p in _panels.Values)
                p?.SaveLayout();
        }
    }
}
