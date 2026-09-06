using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
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

        private const int LogCapacity = 2500;

        private static readonly string[] _logLines = new string[LogCapacity];
        private static int _logHead;
        private static int _logCount;
        private static int _logSeq;
        private static readonly object _logLock = new object();
        private static TextWriter _originalOut;
        private static bool _captureStarted;
        private static bool _isPaused;
        private static readonly HashSet<string> _enabledLevels = new HashSet<string> { "ERROR", "WARN", "INFO", "DEBUG", "UNKNOWN" };
        private static ConsolePanel _activeInstance;
        private static readonly StringBuilder _partialLine = new StringBuilder();

        private string _filter = "";
        private bool _metricsReady;
        private float _charWidth = 7f;
        private float _lineHeight = 14f;
        private float _scrollOffsetY;
        private bool _autoScroll = true;
        private int _viewSeq = -1;
        private string _viewFilter = "";
        private int _viewCharsPerLine;
        private readonly List<ViewRow> _viewRows = new List<ViewRow>(LogCapacity);
        private float _viewTotalHeight;
        private const float ToolbarHeight = 32f;
        private const float LogPadding = 8f;
        private const float FontSize = 12f;
        private const string FontFamily = "Consolas";
        private const float ScrollbarWidth = 7f;

        private struct ViewRow
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

            public override void Write(char value)
            {
                _originalOut?.Write(value);
                if (!_captureStarted) return;
                if (value == '\n') FlushPartial();
                else if (value != '\r') _partialLine.Append(value);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                if (buffer == null || count <= 0) return;
                _originalOut?.Write(buffer, index, count);
                if (!_captureStarted) return;
                int end = index + count;
                for (int i = index; i < end; i++)
                {
                    char c = buffer[i];
                    if (c == '\n') FlushPartial();
                    else if (c != '\r') _partialLine.Append(c);
                }
            }

            public override void Write(string value)
            {
                if (value == null) return;
                _originalOut?.Write(value);
                if (!_captureStarted) return;
                AppendChunk(value);
            }

            public override void WriteLine(string value)
            {
                _originalOut?.WriteLine(value);
                if (!_captureStarted) return;
                if (_partialLine.Length > 0)
                {
                    _partialLine.Append(value ?? "");
                    FlushPartial();
                }
                else
                {
                    AddLogInternal(value ?? "");
                }
            }

            public override void WriteLine()
            {
                _originalOut?.WriteLine();
                if (!_captureStarted) return;
                FlushPartial();
            }

            private static void AppendChunk(string value)
            {
                int start = 0;
                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] != '\n') continue;
                    if (i > start) _partialLine.Append(value, start, i - start);
                    if (_partialLine.Length > 0 && _partialLine[_partialLine.Length - 1] == '\r')
                        _partialLine.Length--;
                    FlushPartial();
                    start = i + 1;
                }
                if (start < value.Length)
                    _partialLine.Append(value, start, value.Length - start);
            }

            private static void FlushPartial()
            {
                string line = _partialLine.ToString();
                _partialLine.Clear();
                AddLogInternal(line);
            }
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
            EnsureMetrics();
            _viewSeq = -1;
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
            bool empty;
            lock (_logLock) empty = _logCount == 0;
            if (empty) AddLog("Console ready — capturing all Console.WriteLine (levels + text filter active).");
        }

        private void EnsureMetrics()
        {
            if (_metricsReady || _uiOverlay?.TextRenderer == null) return;
            _lineHeight = _uiOverlay.TextRenderer.GetLineHeight(FontSize, FontFamily);
            if (_lineHeight <= 0f) _lineHeight = 14f;
            Vector2 em = _uiOverlay.TextRenderer.GetTextSize("M", FontSize, FontFamily);
            _charWidth = em.X > 0.5f ? em.X : 7f;
            _metricsReady = true;
        }

        public static void AddLogInternal(string message)
        {
            if (_isPaused) return;
            string formatted = $"[{DateTime.Now:HH:mm:ss}] {message ?? ""}";
            lock (_logLock)
            {
                if (_logCount == LogCapacity)
                    _logHead = (_logHead + 1) % LogCapacity;
                else
                    _logCount++;
                int slot = (_logHead + _logCount - 1) % LogCapacity;
                _logLines[slot] = formatted;
                _logSeq++;
            }
        }

        public void AddLog(string message) { AddLogInternal(message); }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            if (_uiOverlay == null || !Visible) return;

            var filterEl = _uiOverlay.FindElementById("filterInput") as InputElement;
            if (filterEl != null)
            {
                string cur = filterEl.Value ?? "";
                if (cur != _filter)
                {
                    _filter = cur;
                    _scrollOffsetY = 0f;
                    _autoScroll = true;
                    _viewSeq = -1;
                }
            }

            if (scrollDelta != 0f)
            {
                _scrollOffsetY = Math.Max(0f, _scrollOffsetY - scrollDelta * 28f);
                if (scrollDelta < 0f) _autoScroll = false;
            }

            float logH = Size.Y - (HasTitleBar ? TitleHeight : 0f) - ToolbarHeight;
            float maxScroll = Math.Max(0f, _viewTotalHeight - logH + 20f);
            if (_scrollOffsetY >= maxScroll - 5f)
                _autoScroll = true;
        }

        private void RebuildViewIfNeeded()
        {
            EnsureMetrics();
            int seq;
            lock (_logLock) seq = _logSeq;

            bool filterChanged = _viewFilter != (_filter ?? "");
            if (filterChanged)
            {
                _viewFilter = _filter ?? "";
                _viewSeq = -1;
            }
            if (_viewSeq == seq) return;

            float maxWidth = Math.Max(50f, Size.X - 2f * LogPadding);
            int charsPerLine = Math.Max(8, (int)(maxWidth / _charWidth));
            bool hasFilter = _viewFilter.Length > 0;

            if (_viewSeq < 0 || charsPerLine != _viewCharsPerLine)
            {
                _viewCharsPerLine = charsPerLine;
                _viewRows.Clear();
                _viewTotalHeight = 0f;
                lock (_logLock)
                {
                    for (int i = 0; i < _logCount; i++)
                    {
                        string line = _logLines[(_logHead + i) % LogCapacity];
                        if (line == null) continue;
                        if (hasFilter && line.IndexOf(_viewFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string lvl = GetLevel(line);
                        if (!_enabledLevels.Contains(lvl)) continue;
                        AddWrappedRows(line, GetLevelColor(lvl), charsPerLine);
                    }
                    _viewSeq = _logSeq;
                }
            }
            else
            {
                lock (_logLock)
                {
                    int pending = _logSeq - _viewSeq;
                    if (pending < 0) pending = _logCount;
                    if (pending > _logCount) pending = _logCount;
                    int start = _logCount - pending;
                    if (start < 0) start = 0;
                    for (int i = start; i < _logCount; i++)
                    {
                        string line = _logLines[(_logHead + i) % LogCapacity];
                        if (line == null) continue;
                        if (hasFilter && line.IndexOf(_viewFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string lvl = GetLevel(line);
                        if (!_enabledLevels.Contains(lvl)) continue;
                        AddWrappedRows(line, GetLevelColor(lvl), charsPerLine);
                    }
                    _viewSeq = _logSeq;
                }
                const int viewCap = LogCapacity * 2;
                if (_viewRows.Count > viewCap)
                {
                    int drop = _viewRows.Count - LogCapacity;
                    float lost = 0f;
                    for (int i = 0; i < drop; i++)
                        lost += _viewRows[i].Height + 1f;
                    _viewRows.RemoveRange(0, drop);
                    _viewTotalHeight = Math.Max(0f, _viewTotalHeight - lost);
                    if (!_autoScroll)
                        _scrollOffsetY = Math.Max(0f, _scrollOffsetY - lost);
                }
            }

            if (_autoScroll)
            {
                float logH = Size.Y - (HasTitleBar ? TitleHeight : 0f) - ToolbarHeight;
                _scrollOffsetY = Math.Max(0f, _viewTotalHeight - logH + 20f);
            }
        }

        private void AddWrappedRows(string line, Vector4 color, int charsPerLine)
        {
            if (string.IsNullOrEmpty(line))
            {
                _viewRows.Add(new ViewRow { Text = "", Color = color, Height = _lineHeight });
                _viewTotalHeight += _lineHeight + 1f;
                return;
            }

            int remaining = line.Length;
            int offset = 0;
            while (remaining > 0)
            {
                int take = remaining > charsPerLine ? charsPerLine : remaining;
                if (take < remaining)
                {
                    int br = LastBreak(line, offset, take);
                    if (br > 0) take = br;
                }
                _viewRows.Add(new ViewRow
                {
                    Text = line.Substring(offset, take),
                    Color = color,
                    Height = _lineHeight
                });
                _viewTotalHeight += _lineHeight + 1f;
                offset += take;
                remaining -= take;
                if (remaining > 0 && offset < line.Length && line[offset] == ' ')
                {
                    offset++;
                    remaining--;
                }
            }
        }

        private static int LastBreak(string line, int offset, int take)
        {
            int end = offset + take;
            for (int i = end - 1; i > offset; i--)
            {
                if (line[i] == ' ') return i - offset;
            }
            return 0;
        }

        private static string GetLevel(string line)
        {
            if (Has(line, "ERROR") || Has(line, "[ERR")) return "ERROR";
            if (Has(line, "WARN") || Has(line, "[WRN")) return "WARN";
            if (Has(line, "DEBUG") || Has(line, "[DBG")) return "DEBUG";
            if (Has(line, "INFO") || Has(line, "[INF")) return "INFO";
            return "UNKNOWN";
        }

        private static bool Has(string line, string token)
        {
            return line.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
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
                lock (_logLock)
                {
                    _logHead = 0;
                    _logCount = 0;
                    _logSeq++;
                    Array.Clear(_logLines, 0, _logLines.Length);
                }
                _scrollOffsetY = 0f;
                _autoScroll = true;
                _viewSeq = -1;
            }
            else if (hook == "TogglePause")
            {
                _isPaused = !_isPaused;
            }
            else if (hook.StartsWith("ToggleLevel:"))
            {
                string lvl = hook.Substring(12).ToUpperInvariant();
                if (_enabledLevels.Contains(lvl)) _enabledLevels.Remove(lvl); else _enabledLevels.Add(lvl);
                _scrollOffsetY = 0f;
                _autoScroll = true;
                _viewSeq = -1;
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string h = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(h)) HandleDataHook(h);
        }

        protected override void RenderContentLayer()
        {
            base.RenderContentLayer();
            if (_uiOverlay == null || !Visible) return;

            RebuildViewIfNeeded();

            float titleH = HasTitleBar ? TitleHeight : 0f;
            float logAreaTop = titleH + ToolbarHeight;
            float logAreaLeft = LogPadding;
            float logAreaWidth = Size.X - 2f * LogPadding;
            float logAreaHeight = Size.Y - logAreaTop;
            if (logAreaWidth < 20f || logAreaHeight < 10f) return;

            float maxScroll = Math.Max(0f, _viewTotalHeight - logAreaHeight + 20f);
            _scrollOffsetY = Math.Clamp(_scrollOffsetY, 0f, maxScroll);

            float y = logAreaTop - _scrollOffsetY;
            int count = _viewRows.Count;
            for (int i = 0; i < count; i++)
            {
                ViewRow entry = _viewRows[i];
                float next = y + entry.Height + 1f;
                if (next < logAreaTop) { y = next; continue; }
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
                y = next;
            }

            DrawScrollbar(logAreaTop, logAreaLeft, logAreaWidth, logAreaHeight, _viewTotalHeight);
        }

        private void DrawScrollbar(float logTop, float logLeft, float logW, float logH, float totalH)
        {
            if (totalH <= logH + 1f) return;
            float trackX = logLeft + logW - ScrollbarWidth - 1f;
            float trackY = logTop + 2f;
            float trackW = ScrollbarWidth;
            float trackH = logH - 4f;
            float[] trackNdc = HtmlLayoutUtils.GetNdcQuad(trackX, trackY, trackW, trackH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(trackNdc, new Vector4(0.18f, 0.18f, 0.18f, 0.95f));
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
            _metricsReady = false;
            _viewSeq = -1;
        }

        public override void OnLiveResize(float w, float h)
        {
            base.OnLiveResize(w, h);
            _viewSeq = -1;
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var p = new ConsolePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(p) { Mode = OpenMode.Overlay });
        }
    }
}
