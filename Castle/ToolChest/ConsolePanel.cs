using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
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
            public override bool HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
                return true;
            }
        }

        private static readonly List<string> _allLogLines = new List<string>();
        private static readonly object _logLock = new object();
        private static TextWriter _originalOut;
        private static bool _captureStarted;
        private static bool _isPaused;
        private static readonly HashSet<string> _enabledLevels = new HashSet<string> { "ERROR", "WARN", "INFO", "DEBUG", "UNKNOWN" };
        private static ConsolePanel _activeInstance;

        private string _filter = "";
        private int _lastLogCount;
        private bool _dirty;
        private double _lastRebuildTime;

        static ConsolePanel()
        {
            try
            {
                _originalOut = Console.Out;
                Console.SetOut(new LogCaptureWriter());
                _captureStarted = true;
            }
            catch { }
        }

        private class LogCaptureWriter : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
            public override void WriteLine(string value)
            {
                if (_originalOut != null) _originalOut.WriteLine(value);
                if (_captureStarted && !_isPaused) AddLogInternal(value ?? "");
            }
            public override void Write(char value) { }
        }

        public ConsolePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            BaseWidth = 720f;
            BaseHeight = 380f;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new ConsoleUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            _activeInstance = this;
            LoadConsoleUI();
        }

        public override void Detach()
        {
            if (_activeInstance == this) _activeInstance = null;
            base.Detach();
        }

        private void LoadConsoleUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ConsolePanelUI.html");
            if (!File.Exists(htmlPath)) { AddLogInternal("[Console] ERROR: ConsolePanelUI.html missing"); return; }
            _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            if (_allLogLines.Count == 0) AddLog("Console ready — capturing all Console.WriteLine (levels + text filter active).");
        }

        public static void AddLogInternal(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            string formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            lock (_logLock)
            {
                _allLogLines.Add(formatted);
                if (_allLogLines.Count > 2500) _allLogLines.RemoveAt(0);
            }
            if (_activeInstance != null) _activeInstance._dirty = true;   // just mark dirty — no heavy work
        }

        public void AddLog(string message) { AddLogInternal(message); }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            if (_uiOverlay == null || !Visible) return;

            // real-time text filter (cheap)
            var filterEl = _uiOverlay.FindElementById("filterInput") as InputElement;
            if (filterEl != null)
            {
                string cur = filterEl.Value ?? "";
                if (cur != _filter) { _filter = cur; _dirty = true; }
            }

            // throttled rebuild (max ~12 times/sec) — keeps it at rest
            double now = _controlContext.GetTime();
            if (_dirty && (now - _lastRebuildTime) > 0.08)
            {
                RebuildLogUI();
                _lastRebuildTime = now;
                _dirty = false;
            }
        }

        private void RebuildLogUI()
        {
            if (_uiOverlay == null) return;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ConsolePanelUI.html");
            if (!File.Exists(path)) return;

            string html = File.ReadAllText(path).Replace("<!--LOGS-->", BuildFilteredLogHtml());
            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string BuildFilteredLogHtml()
        {
            var sb = new StringBuilder();
            List<string> snap; lock (_logLock) { snap = new List<string>(_allLogLines); }
            string f = _filter?.ToUpperInvariant() ?? "";
            foreach (var line in snap)
            {
                if (!string.IsNullOrEmpty(f) && !line.ToUpperInvariant().Contains(f)) continue;
                string lvl = GetLevel(line);
                if (!_enabledLevels.Contains(lvl)) continue;
                string col = lvl == "ERROR" ? "#ff6b6b" : lvl == "WARN" ? "#ffd93d" : lvl == "DEBUG" ? "#6bcb77" : "#cccccc";
                string safe = line.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                sb.AppendLine($"<div class=\"log-line\" style=\"color:{col}\">{safe}</div>");
            }
            return sb.ToString();
        }

        private static string GetLevel(string line)
        {
            string u = line.ToUpperInvariant();
            if (u.Contains("ERROR") || u.Contains("[ERR")) return "ERROR";
            if (u.Contains("WARN") || u.Contains("[WRN")) return "WARN";
            if (u.Contains("DEBUG") || u.Contains("[DBG")) return "DEBUG";
            if (u.Contains("INFO") || u.Contains("[INF")) return "INFO";
            return "UNKNOWN";
        }

        public void HandleDataHook(string hook)
        {
            if (hook == "Clear")
            {
                lock (_logLock) _allLogLines.Clear();
                _lastLogCount = 0;
                _dirty = true;
            }
            else if (hook == "TogglePause")
            {
                _isPaused = !_isPaused;
                _dirty = true;
            }
            else if (hook.StartsWith("ToggleLevel:"))
            {
                string lvl = hook.Substring(12).ToUpperInvariant();
                if (_enabledLevels.Contains(lvl)) _enabledLevels.Remove(lvl); else _enabledLevels.Add(lvl);
                _dirty = true;
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string h = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(h)) HandleDataHook(h);
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var p = new ConsolePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(p) { Mode = OpenMode.Overlay });
        }
    }
}