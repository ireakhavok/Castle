// Folder: ToolChest
// File: ConsolePanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ToolChest
{
    public class ConsolePanel : BasePanel
    {
        private class ConsoleUIOverlay : UIOverlay
        {
            private readonly ConsolePanel _parent;
            public ConsoleUIOverlay(ConsolePanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleDataHook(string hook)
            {
                _parent.HandleDataHook(hook);
            }
            public override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }

        private readonly List<string> _logLines = new List<string>();
        private string _filter = "";

        public ConsolePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new ConsoleUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            LoadConsoleUI();
        }

        private void LoadConsoleUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ConsolePanelUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"[ConsolePanel] ERROR: ConsolePanelUI.html not found at {htmlPath}");
                return;
            }
            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            AddLog("Console initialized. Ready for logs and commands.");
        }

        public void AddLog(string message)
        {
            _logLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            RebuildLogUI();
        }

        private void RebuildLogUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ConsolePanelUI.html");
            if (!File.Exists(htmlPath)) return;
            string template = File.ReadAllText(htmlPath);
            string filteredLogs = BuildFilteredLogHtml();
            string finalHtml = template.Replace("<!--LOGS-->", filteredLogs);
            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string BuildFilteredLogHtml()
        {
            var sb = new StringBuilder();
            foreach (var line in _logLines)
            {
                if (string.IsNullOrEmpty(_filter) || line.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"<div class=\"log-line\">{line}</div>");
                }
            }
            return sb.ToString();
        }

        public void HandleDataHook(string hook)
        {
            if (hook == "Clear")
            {
                _logLines.Clear();
                RebuildLogUI();
            }
            else if (hook.StartsWith("Filter:"))
            {
                _filter = hook.Substring(7).Trim();
                RebuildLogUI();
            }
            else if (hook == "SubmitCommand")
            {
                AddLog("Command executed (placeholder)");
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(hook))
            {
                HandleDataHook(hook);
            }
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new ConsolePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}