// Folder: ToolChest
// File: ScriptEditorPanel.cs
using CastleBuilder;
using Keystone;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ToolChest
{
    public class ScriptEditorPanel : BasePanel
    {
        private class ScriptEditorUIOverlay : UIOverlay
        {
            private readonly ScriptEditorPanel _parent;
            public ScriptEditorUIOverlay(ScriptEditorPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
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
                base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
                return true;
            }
        }

        private readonly List<string> _scriptFiles = new List<string>();
        private string _selectedPath;
        private string _sourceBuffer = "";
        private readonly List<string> _displayLines = new List<string>();
        private bool _displayDirty = true;
        private bool _sourceDirty = false;
        private int _cursorLine;
        private int _cursorCol;
        private float _scrollY;
        private string _statusText = "Ready";
        private Vector4 _statusColor = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        private bool _hasFocus;
        private double _lastCursorBlink;
        private bool _cursorVisible = true;
        private readonly Dictionary<Key, bool> _prevKey = new Dictionary<Key, bool>();
        private string _scriptsRoot;

        private const float ToolbarH = 32f;
        private const float FileListW = 220f;
        private const float FontSize = 13f;
        private const string FontFamily = "Consolas";
        private const float LinePad = 2f;
        private const float EditorPad = 8f;
        private const float FileRowH = 22f;

        public ScriptEditorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
            DockingMode = DockingMode.IDE;
            BaseWidth = 960f;
            BaseHeight = 640f;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new ScriptEditorUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            LoadUIOnce();
            RefreshFileList();
        }

        private void LoadUIOnce()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScriptEditorPanelUI.html");
            if (!File.Exists(htmlPath))
            {
                _statusText = "ScriptEditorPanelUI.html missing";
                _statusColor = new Vector4(1f, 0.4f, 0.4f, 1f);
                return;
            }
            _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string GetScriptsDir()
        {
            string project = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(project) || !Directory.Exists(project))
                return null;
            string dir = Path.Combine(project, "Scripts");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void RefreshFileList()
        {
            _scriptFiles.Clear();
            _scriptsRoot = GetScriptsDir();
            if (_scriptsRoot != null)
            {
                // Recursive – required for Scripts/Chess/... layout
                foreach (string f in Directory.GetFiles(_scriptsRoot, "*.cs", SearchOption.AllDirectories))
                    _scriptFiles.Add(f);
                _scriptFiles.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }

        private string DisplayName(string fullPath)
        {
            if (string.IsNullOrEmpty(_scriptsRoot) || string.IsNullOrEmpty(fullPath))
                return Path.GetFileName(fullPath) ?? fullPath;
            try
            {
                return Path.GetRelativePath(_scriptsRoot, fullPath).Replace('\\', '/');
            }
            catch
            {
                return Path.GetFileName(fullPath);
            }
        }

        private void SelectFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (_sourceDirty && !string.IsNullOrEmpty(_selectedPath))
                SaveCurrentFile();

            _selectedPath = path;
            try
            {
                _sourceBuffer = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                _sourceBuffer = "";
                _statusText = "Read failed: " + ex.Message;
                _statusColor = new Vector4(1f, 0.4f, 0.4f, 1f);
            }
            _sourceDirty = false;
            _cursorLine = 0;
            _cursorCol = 0;
            _scrollY = 0f;
            _displayDirty = true;
        }

        private void SaveCurrentFile()
        {
            if (string.IsNullOrEmpty(_selectedPath)) return;
            try
            {
                File.WriteAllText(_selectedPath, _sourceBuffer ?? "");
                _sourceDirty = false;
                _statusText = "Saved " + DisplayName(_selectedPath);
                _statusColor = new Vector4(0.4f, 0.9f, 0.5f, 1f);
            }
            catch (Exception ex)
            {
                _statusText = "Save failed: " + ex.Message;
                _statusColor = new Vector4(1f, 0.4f, 0.4f, 1f);
            }
        }

        private void RebuildScripts()
        {
            if (_sourceDirty)
                SaveCurrentFile();

            string project = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(project))
            {
                _statusText = "No active project";
                _statusColor = new Vector4(1f, 0.4f, 0.4f, 1f);
                return;
            }

            _statusText = "Building…";
            _statusColor = new Vector4(0.9f, 0.85f, 0.3f, 1f);

            string scriptsDir = Path.Combine(project, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            string csproj = Path.Combine(scriptsDir, "SiegeScripts.csproj");
            if (!File.Exists(csproj))
                ScriptLoader.BuildProjectScripts(project);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csproj}\" --configuration Release --no-incremental",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = scriptsDir
            };

            try
            {
                using (var p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    // Keep core registration path in sync
                    ScriptLoader.BuildProjectScripts(project);

                    if (p.ExitCode == 0)
                    {
                        _statusText = "Build succeeded";
                        _statusColor = new Vector4(0.4f, 0.9f, 0.5f, 1f);
                    }
                    else
                    {
                        string err = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                        if (err.Length > 160) err = err.Substring(0, 157) + "…";
                        _statusText = "Build FAILED – " + err.Replace("\r", " ").Replace("\n", " ");
                        _statusColor = new Vector4(1f, 0.4f, 0.4f, 1f);
                    }
                }
            }
            catch (Exception ex)
            {
                _statusText = "Build error: " + ex.Message;
                _statusColor = new Vector4(1f, 0.4f, 0.4f, 1f);
            }

            RefreshFileList();
        }

        public void HandleDataHook(string hook)
        {
            if (hook == "Rebuild")
                RebuildScripts();
            else if (hook == "SaveFile")
                SaveCurrentFile();
            else if (hook == "RefreshList")
                RefreshFileList();
        }

        public void HandleUIClick(HtmlElement elem) { }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);

            bool isTop = PanelManager.Current?.GetTopmostPanelAt(absMousePos) == this;
            if (!isTop)
            {
                _hasFocus = false;
                return;
            }

            float titleH = HasTitleBar ? TitleHeight : 0f;
            float listTop = titleH + ToolbarH;
            float editorLeft = FileListW;
            float editorTop = listTop;
            float editorW = Size.X - FileListW;
            float editorH = Size.Y - listTop;

            Vector2 local = absMousePos - Position;

            if (mousePressed && local.X >= 0 && local.X < FileListW && local.Y >= listTop && local.Y < Size.Y)
            {
                int row = (int)((local.Y - listTop) / FileRowH);
                if (row >= 0 && row < _scriptFiles.Count)
                    SelectFile(_scriptFiles[row]);
            }

            bool overEditor = local.X >= editorLeft && local.X <= Size.X &&
                              local.Y >= editorTop && local.Y <= Size.Y;

            if (mousePressed && overEditor)
                _hasFocus = true;
            else if (mousePressed && !overEditor)
                _hasFocus = false;

            if (scrollDelta != 0f && overEditor)
                _scrollY = Math.Max(0f, _scrollY - scrollDelta * 24f);

            if (!_hasFocus) return;

            double now = _controlContext.GetTime();
            if (now - _lastCursorBlink > 0.5)
            {
                _cursorVisible = !_cursorVisible;
                _lastCursorBlink = now;
            }

            bool shift = _controlContext.GetKey(_window, Key.LeftShift) == InputAction.Press ||
                         _controlContext.GetKey(_window, Key.RightShift) == InputAction.Press;

            ProcessKey(Key.Backspace, () => Backspace());
            ProcessKey(Key.Enter, () => InsertNewLine());
            ProcessKey(Key.Left, () => MoveCursor(-1, 0));
            ProcessKey(Key.Right, () => MoveCursor(1, 0));
            ProcessKey(Key.Up, () => MoveCursor(0, -1));
            ProcessKey(Key.Down, () => MoveCursor(0, 1));

            for (Key k = Key.A; k <= Key.Z; k++)
            {
                if (WasPressed(k))
                {
                    char? c = InputElement.GetCharFromKey(k, shift, "text");
                    if (c.HasValue) InsertChar(c.Value);
                }
            }
            for (Key k = Key.Key0; k <= Key.Key9; k++)
            {
                if (WasPressed(k))
                {
                    char? c = InputElement.GetCharFromKey(k, shift, "text");
                    if (c.HasValue) InsertChar(c.Value);
                }
            }
            TryInsert(Key.Space, ' ');
            TryInsert(Key.Minus, shift ? '_' : '-');
            TryInsert(Key.Equal, shift ? '+' : '=');
            TryInsert(Key.LeftBracket, shift ? '{' : '[');
            TryInsert(Key.RightBracket, shift ? '}' : ']');
            TryInsert(Key.Backslash, shift ? '|' : '\\');
            TryInsert(Key.Semicolon, shift ? ':' : ';');
            TryInsert(Key.Apostrophe, shift ? '"' : '\'');
            TryInsert(Key.Comma, shift ? '<' : ',');
            TryInsert(Key.Period, shift ? '>' : '.');
            TryInsert(Key.Slash, shift ? '?' : '/');
            TryInsert(Key.GraveAccent, shift ? '~' : '`');
        }

        private bool WasPressed(Key k)
        {
            bool now = _controlContext.GetKey(_window, k) == InputAction.Press;
            bool was = _prevKey.TryGetValue(k, out bool p) && p;
            _prevKey[k] = now;
            return now && !was;
        }

        private void ProcessKey(Key k, Action action)
        {
            if (WasPressed(k)) action();
        }

        private void TryInsert(Key k, char c)
        {
            if (WasPressed(k)) InsertChar(c);
        }

        private void EnsureLines()
        {
            if (!_displayDirty) return;
            _displayLines.Clear();
            if (string.IsNullOrEmpty(_sourceBuffer))
            {
                _displayLines.Add("");
            }
            else
            {
                using (var reader = new StringReader(_sourceBuffer))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        _displayLines.Add(line);
                }
                if (_sourceBuffer.EndsWith("\n") || _sourceBuffer.EndsWith("\r\n"))
                    _displayLines.Add("");
            }
            _displayDirty = false;
        }

        private void InsertChar(char c)
        {
            EnsureLines();
            if (_cursorLine < 0 || _cursorLine >= _displayLines.Count) return;
            string line = _displayLines[_cursorLine];
            _cursorCol = Math.Clamp(_cursorCol, 0, line.Length);
            _displayLines[_cursorLine] = line.Insert(_cursorCol, c.ToString());
            _cursorCol++;
            RebuildBufferFromLines();
            _sourceDirty = true;
        }

        private void InsertNewLine()
        {
            EnsureLines();
            if (_cursorLine < 0 || _cursorLine >= _displayLines.Count) return;
            string line = _displayLines[_cursorLine];
            _cursorCol = Math.Clamp(_cursorCol, 0, line.Length);
            string left = line.Substring(0, _cursorCol);
            string right = line.Substring(_cursorCol);
            _displayLines[_cursorLine] = left;
            _displayLines.Insert(_cursorLine + 1, right);
            _cursorLine++;
            _cursorCol = 0;
            RebuildBufferFromLines();
            _sourceDirty = true;
        }

        private void Backspace()
        {
            EnsureLines();
            if (_cursorLine < 0 || _cursorLine >= _displayLines.Count) return;
            string line = _displayLines[_cursorLine];
            if (_cursorCol > 0)
            {
                _displayLines[_cursorLine] = line.Remove(_cursorCol - 1, 1);
                _cursorCol--;
            }
            else if (_cursorLine > 0)
            {
                string prev = _displayLines[_cursorLine - 1];
                _cursorCol = prev.Length;
                _displayLines[_cursorLine - 1] = prev + line;
                _displayLines.RemoveAt(_cursorLine);
                _cursorLine--;
            }
            RebuildBufferFromLines();
            _sourceDirty = true;
        }

        private void MoveCursor(int dx, int dy)
        {
            EnsureLines();
            if (dy != 0)
            {
                _cursorLine = Math.Clamp(_cursorLine + dy, 0, Math.Max(0, _displayLines.Count - 1));
                string line = _displayLines[_cursorLine];
                _cursorCol = Math.Clamp(_cursorCol, 0, line.Length);
            }
            if (dx != 0)
            {
                string line = _displayLines[_cursorLine];
                _cursorCol = Math.Clamp(_cursorCol + dx, 0, line.Length);
            }
        }

        private void RebuildBufferFromLines()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _displayLines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(_displayLines[i]);
            }
            _sourceBuffer = sb.ToString();
        }

        protected override void RenderContentLayer()
        {
            float titleH = HasTitleBar ? TitleHeight : 0f;
            float listTop = titleH + ToolbarH;
            float listH = Size.Y - listTop;
            float editorLeft = FileListW + EditorPad;
            float editorTop = listTop + EditorPad;
            float editorW = Size.X - FileListW - EditorPad * 2f;
            float editorH = Size.Y - listTop - EditorPad * 2f;

            // File list background
            float[] listBg = HtmlLayoutUtils.GetNdcQuad(0, listTop, FileListW, listH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(listBg, new Vector4(0.12f, 0.12f, 0.12f, 1f));

            float rowY = listTop;
            for (int i = 0; i < _scriptFiles.Count; i++)
            {
                bool sel = string.Equals(_scriptFiles[i], _selectedPath, StringComparison.OrdinalIgnoreCase);
                if (sel)
                {
                    float[] rowBg = HtmlLayoutUtils.GetNdcQuad(0, rowY, FileListW, FileRowH, Matrix4x4.Identity, Size.X, Size.Y);
                    QuadRenderer.DrawNdcQuad(rowBg, new Vector4(0.18f, 0.28f, 0.18f, 1f));
                }
                string name = DisplayName(_scriptFiles[i]);
                _uiOverlay.TextRenderer.RenderText(
                    name,
                    8f,
                    rowY + 4f,
                    Size.X,
                    Size.Y,
                    FontSize,
                    sel ? new Vector4(0.486f, 1f, 0.796f, 1f) : new Vector4(0.8f, 0.8f, 0.8f, 1f),
                    FontFamily);
                rowY += FileRowH;
                if (rowY > Size.Y) break;
            }

            if (editorW < 40f || editorH < 20f) return;

            EnsureLines();

            float lineH = _uiOverlay.TextRenderer.GetLineHeight(FontSize, FontFamily) + LinePad;
            float totalH = _displayLines.Count * lineH;
            float maxScroll = Math.Max(0f, totalH - editorH);
            _scrollY = Math.Clamp(_scrollY, 0f, maxScroll);

            float y = editorTop - _scrollY;
            for (int i = 0; i < _displayLines.Count; i++)
            {
                if (y + lineH < editorTop) { y += lineH; continue; }
                if (y > editorTop + editorH) break;

                string text = _displayLines[i] ?? "";
                _uiOverlay.TextRenderer.RenderText(
                    text,
                    editorLeft,
                    y,
                    Size.X,
                    Size.Y,
                    FontSize,
                    new Vector4(0.85f, 0.85f, 0.85f, 1f),
                    FontFamily);

                if (_hasFocus && _cursorVisible && i == _cursorLine)
                {
                    string prefix = text.Length >= _cursorCol ? text.Substring(0, _cursorCol) : text;
                    float cx = editorLeft + _uiOverlay.TextRenderer.GetTextSize(prefix, FontSize, FontFamily).X;
                    float[] ndc = HtmlLayoutUtils.GetNdcQuad(cx, y, 2f, lineH - LinePad, Matrix4x4.Identity, Size.X, Size.Y);
                    QuadRenderer.DrawNdcQuad(ndc, new Vector4(0.486f, 1f, 0.796f, 1f));
                }
                y += lineH;
            }

            // Status
            _uiOverlay.TextRenderer.RenderText(
                _statusText,
                FileListW + 12f,
                titleH + 8f,
                Size.X,
                Size.Y,
                12f,
                _statusColor,
                FontFamily);
        }

        public override void OnPanelResize(float w, float h)
        {
            base.OnPanelResize(w, h);
            _displayDirty = true;
        }

        public override void OnLiveResize(float w, float h)
        {
            base.OnLiveResize(w, h);
            _displayDirty = true;
        }

        public override void Dispose()
        {
            if (_sourceDirty)
                SaveCurrentFile();
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new ScriptEditorPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}