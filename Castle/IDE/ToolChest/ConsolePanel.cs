using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
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
        private bool _dirty;
        private double _lastRebuildTime;
        private float _scrollOffsetY; // pixels from top of log content
        private bool _autoScroll = true;
        private readonly List<LogEntry> _visibleLines = new List<LogEntry>();
        private const float ToolbarHeight = 32f;
        private const float LogPadding = 8f;
        private const float FontSize = 12f;
        private const string FontFamily = "Consolas";
        private const float ScrollbarWidth = 7f;

        private class LogEntry
        {
            public string Text;
            public Vector4 Color;
            public float Height;
        }

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
            if (_allLogLines.Count > 0)
            {
                _dirty = true;
            }
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
            if (_activeInstance != null)
            {
                _activeInstance._dirty = true;
            }
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
                if (cur != _filter)
                {
                    _filter = cur;
                    _scrollOffsetY = 0f;
                    _autoScroll = true;
                    _dirty = true;
                }
            }

            // scroll handling + auto-scroll logic
            if (scrollDelta != 0f)
            {
                _scrollOffsetY = Math.Max(0f, _scrollOffsetY - scrollDelta * 28f);
                if (scrollDelta < 0f) _autoScroll = false; // user scrolled up → stop auto-follow
            }

            // throttled rebuild (~12 fps max when dirty) — keeps CPU at rest when idle
            double now = _controlContext.GetTime();
            if (_dirty && (now - _lastRebuildTime) > 0.08)
            {
                RebuildVisibleLines();
                _lastRebuildTime = now;
                _dirty = false;
            }

            // re-enable auto-scroll when user reaches bottom
            float totalH = 0f;
            foreach (var e in _visibleLines) totalH += e.Height + 1f;
            float maxScroll = Math.Max(0f, totalH - (Size.Y - (HasTitleBar ? TitleHeight : 0f) - ToolbarHeight) + 20f);
            if (_scrollOffsetY >= maxScroll - 5f)
            {
                _autoScroll = true;
            }
        }

        private void RebuildVisibleLines()
        {
            _visibleLines.Clear();
            List<string> snap;
            lock (_logLock) { snap = new List<string>(_allLogLines); }

            string f = _filter?.ToUpperInvariant() ?? "";
            float maxWidth = Math.Max(50f, Size.X - 2f * LogPadding);

            foreach (var line in snap)
            {
                if (!string.IsNullOrEmpty(f) && !line.ToUpperInvariant().Contains(f)) continue;
                string lvl = GetLevel(line);
                if (!_enabledLevels.Contains(lvl)) continue;

                Vector4 col = GetLevelColor(lvl);
                WrapAndAddLine(line, col, maxWidth);
            }

            // auto-scroll to bottom on new content (unless user has scrolled up)
            if (_autoScroll)
            {
                float totalH = 0f;
                foreach (var e in _visibleLines) totalH += e.Height + 1f;
                float logH = Size.Y - (HasTitleBar ? TitleHeight : 0f) - ToolbarHeight;
                _scrollOffsetY = Math.Max(0f, totalH - logH + 20f);
            }
        }

        private void WrapAndAddLine(string line, Vector4 color, float maxWidth)
        {
            if (string.IsNullOrEmpty(line))
            {
                float h = _uiOverlay.TextRenderer.GetLineHeight(FontSize, FontFamily);
                _visibleLines.Add(new LogEntry { Text = "", Color = color, Height = h });
                return;
            }

            string[] words = line.Split(' ');
            string current = "";
            float lineH = _uiOverlay.TextRenderer.GetLineHeight(FontSize, FontFamily);

            foreach (var word in words)
            {
                string test = current.Length == 0 ? word : current + " " + word;
                Vector2 sz = _uiOverlay.TextRenderer.GetTextSize(test, FontSize, FontFamily);
                if (sz.X > maxWidth && current.Length > 0)
                {
                    _visibleLines.Add(new LogEntry { Text = current, Color = color, Height = lineH });
                    current = word;
                }
                else
                {
                    current = test;
                }
            }
            if (current.Length > 0)
            {
                _visibleLines.Add(new LogEntry { Text = current, Color = color, Height = lineH });
            }
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

        private static Vector4 GetLevelColor(string lvl)
        {
            return lvl switch
            {
                "ERROR" => new Vector4(1.0f, 0.42f, 0.42f, 1.0f),
                "WARN" => new Vector4(1.0f, 0.85f, 0.24f, 1.0f),
                "DEBUG" => new Vector4(0.42f, 0.80f, 0.47f, 1.0f),
                _ => new Vector4(0.80f, 0.80f, 0.80f, 1.0f)
            };
        }

        public void HandleDataHook(string hook)
        {
            if (hook == "Clear")
            {
                lock (_logLock) _allLogLines.Clear();
                _visibleLines.Clear();
                _scrollOffsetY = 0f;
                _autoScroll = true;
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
                _scrollOffsetY = 0f;
                _autoScroll = true;
                _dirty = true;
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string h = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(h)) HandleDataHook(h);
        }

        protected override void RenderContentLayer()
        {
            base.RenderContentLayer(); // draws toolbar HTML + chrome
            if (_uiOverlay == null || !Visible) return;

            if (_dirty)
            {
                RebuildVisibleLines();
                _dirty = false;
            }

            float titleH = HasTitleBar ? TitleHeight : 0f;
            float logAreaTop = titleH + ToolbarHeight;           // panel-local Y
            float logAreaLeft = LogPadding;
            float logAreaWidth = Size.X - 2f * LogPadding;
            float logAreaHeight = Size.Y - logAreaTop;

            if (logAreaWidth < 20f || logAreaHeight < 10f) return;

            // clamp scroll
            float totalHeight = 0f;
            foreach (var e in _visibleLines) totalHeight += e.Height + 1f;
            float maxScroll = Math.Max(0f, totalHeight - logAreaHeight + 20f);
            _scrollOffsetY = Math.Clamp(_scrollOffsetY, 0f, maxScroll);

            float y = logAreaTop - _scrollOffsetY;
            float lineSpacing = 1f;

            for (int i = 0; i < _visibleLines.Count; i++)
            {
                var entry = _visibleLines[i];
                if (y + entry.Height < logAreaTop) { y += entry.Height + lineSpacing; continue; }
                if (y > logAreaTop + logAreaHeight) break;

                _uiOverlay.TextRenderer.RenderText(
                    entry.Text,
                    logAreaLeft,
                    y,
                    Size.X,
                    Size.Y,
                    FontSize,
                    entry.Color,
                    FontFamily);

                y += entry.Height + lineSpacing;
            }

            // draw scrollbar (only when needed)
            DrawScrollbar(logAreaTop, logAreaLeft, logAreaWidth, logAreaHeight, totalHeight);
        }

        private void DrawScrollbar(float logTop, float logLeft, float logW, float logH, float totalH)
        {
            if (totalH <= logH + 1f) return; // nothing to scroll

            float trackX = logLeft + logW - ScrollbarWidth - 1f;
            float trackY = logTop + 2f;
            float trackW = ScrollbarWidth;
            float trackH = logH - 4f;

            // track
            float[] trackNdc = HtmlLayoutUtils.GetNdcQuad(trackX, trackY, trackW, trackH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(trackNdc, new Vector4(0.18f, 0.18f, 0.18f, 0.95f));

            // thumb
            float thumbRatio = logH / totalH;
            float thumbH = Math.Max(18f, trackH * thumbRatio);
            float thumbTravel = trackH - thumbH;
            float thumbY = trackY + (_scrollOffsetY / Math.Max(1f, totalH - logH)) * thumbTravel;

            float[] thumbNdc = HtmlLayoutUtils.GetNdcQuad(trackX + 1f, thumbY, trackW - 2f, thumbH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(thumbNdc, new Vector4(0.45f, 0.45f, 0.45f, 1.0f));
        }

        public override void OnPanelResize(float w, float h)
        {
            base.OnPanelResize(w, h);
            _dirty = true; // re-wrap for new width
        }

        public override void OnLiveResize(float w, float h)
        {
            base.OnLiveResize(w, h);
            _dirty = true;
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var p = new ConsolePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(p) { Mode = OpenMode.Overlay });
        }
    }
}