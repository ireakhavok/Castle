// Folder: ToolChest
// File: ScriptEditorPanel.cs
using CastleBuilder;
using Keystone;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace ToolChest
{
    public struct HighlightedSpan
    {
        public int Start;
        public int Length;
        public Vector4 Color;
        public HighlightedSpan(int start, int length, Vector4 color)
        {
            Start = start; Length = length; Color = color;
        }
    }

    public interface ISyntaxHighlighter
    {
        string Language { get; }
        IReadOnlyList<HighlightedSpan> HighlightLine(string line);
    }

    public sealed class CSharpSyntaxHighlighter : ISyntaxHighlighter
    {
        public string Language => "C#";
        static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
            "continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
            "false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
            "internal","is","lock","long","namespace","new","null","object","operator","out","override",
            "params","private","protected","public","readonly","ref","return","sbyte","sealed","short",
            "sizeof","stackalloc","static","string","struct","switch","this","throw","true","try","typeof",
            "uint","ulong","unchecked","unsafe","ushort","using","virtual","void","volatile","while",
            "add","alias","ascending","async","await","by","descending","dynamic","equals","from","get",
            "global","group","into","join","let","nameof","on","orderby","partial","remove","select","set",
            "value","var","when","where","yield","record","init","with","noint","required","file","scoped"
        };
        static readonly Vector4 ColKeyword = new Vector4(0.35f, 0.75f, 0.95f, 1f);
        static readonly Vector4 ColType = new Vector4(0.40f, 0.85f, 0.70f, 1f);
        static readonly Vector4 ColString = new Vector4(0.90f, 0.65f, 0.35f, 1f);
        static readonly Vector4 ColComment = new Vector4(0.45f, 0.55f, 0.40f, 1f);
        static readonly Vector4 ColNumber = new Vector4(0.75f, 0.85f, 0.55f, 1f);
        static readonly Vector4 ColPreproc = new Vector4(0.70f, 0.55f, 0.85f, 1f);
        static readonly Vector4 ColDefault = new Vector4(0.85f, 0.85f, 0.85f, 1f);

        public IReadOnlyList<HighlightedSpan> HighlightLine(string line)
        {
            var spans = new List<HighlightedSpan>();
            if (string.IsNullOrEmpty(line)) return spans;
            int i = 0;
            int n = line.Length;
            while (i < n)
            {
                if (char.IsWhiteSpace(line[i])) { i++; continue; }
                if (i + 1 < n && line[i] == '/' && line[i + 1] == '/')
                {
                    spans.Add(new HighlightedSpan(i, n - i, ColComment));
                    break;
                }
                if (line[i] == '#')
                {
                    spans.Add(new HighlightedSpan(i, n - i, ColPreproc));
                    break;
                }
                if (line[i] == '"' || line[i] == '\'')
                {
                    char q = line[i];
                    int start = i++;
                    while (i < n)
                    {
                        if (line[i] == '\\' && i + 1 < n) { i += 2; continue; }
                        if (line[i] == q) { i++; break; }
                        i++;
                    }
                    spans.Add(new HighlightedSpan(start, i - start, ColString));
                    continue;
                }
                if (i + 1 < n && line[i] == '@' && line[i + 1] == '"')
                {
                    int start = i; i += 2;
                    while (i < n)
                    {
                        if (line[i] == '"' && i + 1 < n && line[i + 1] == '"') { i += 2; continue; }
                        if (line[i] == '"') { i++; break; }
                        i++;
                    }
                    spans.Add(new HighlightedSpan(start, i - start, ColString));
                    continue;
                }
                if (char.IsDigit(line[i]) || (line[i] == '.' && i + 1 < n && char.IsDigit(line[i + 1])))
                {
                    int start = i++;
                    while (i < n && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == 'f' || line[i] == 'F' ||
                                     line[i] == 'd' || line[i] == 'D' || line[i] == 'm' || line[i] == 'M' ||
                                     line[i] == 'x' || line[i] == 'X' || line[i] == 'b' || line[i] == 'B' ||
                                     (line[i] >= 'a' && line[i] <= 'f') || (line[i] >= 'A' && line[i] <= 'F')))
                        i++;
                    spans.Add(new HighlightedSpan(start, i - start, ColNumber));
                    continue;
                }
                if (char.IsLetter(line[i]) || line[i] == '_')
                {
                    int start = i++;
                    while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                    string word = line.Substring(start, i - start);
                    Vector4 col = Keywords.Contains(word) ? ColKeyword :
                                  (char.IsUpper(word[0]) ? ColType : ColDefault);
                    spans.Add(new HighlightedSpan(start, i - start, col));
                    continue;
                }
                spans.Add(new HighlightedSpan(i, 1, ColDefault));
                i++;
            }
            return spans;
        }
    }

    public sealed class HtmlSyntaxHighlighter : ISyntaxHighlighter
    {
        public string Language => "HTML";
        static readonly Vector4 ColTag = new Vector4(0.35f, 0.75f, 0.95f, 1f);
        static readonly Vector4 ColAttr = new Vector4(0.55f, 0.85f, 0.55f, 1f);
        static readonly Vector4 ColValue = new Vector4(0.90f, 0.65f, 0.35f, 1f);
        static readonly Vector4 ColComment = new Vector4(0.45f, 0.55f, 0.40f, 1f);
        static readonly Vector4 ColDefault = new Vector4(0.85f, 0.85f, 0.85f, 1f);

        public IReadOnlyList<HighlightedSpan> HighlightLine(string line)
        {
            var spans = new List<HighlightedSpan>();
            if (string.IsNullOrEmpty(line)) return spans;
            int i = 0;
            int n = line.Length;
            while (i < n)
            {
                if (i + 3 < n && line[i] == '<' && line[i + 1] == '!' && line[i + 2] == '-' && line[i + 3] == '-')
                {
                    int start = i; i += 4;
                    while (i + 2 < n && !(line[i] == '-' && line[i + 1] == '-' && line[i + 2] == '>')) i++;
                    if (i + 2 < n) i += 3;
                    spans.Add(new HighlightedSpan(start, i - start, ColComment));
                    continue;
                }
                if (line[i] == '<')
                {
                    int start = i++;
                    if (i < n && line[i] == '/') i++;
                    while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == ':' || line[i] == '-')) i++;
                    spans.Add(new HighlightedSpan(start, i - start, ColTag));
                    while (i < n && line[i] != '>')
                    {
                        if (char.IsWhiteSpace(line[i])) { i++; continue; }
                        if (char.IsLetter(line[i]))
                        {
                            int a0 = i;
                            while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == '-' || line[i] == ':')) i++;
                            spans.Add(new HighlightedSpan(a0, i - a0, ColAttr));
                            continue;
                        }
                        if (line[i] == '=') { spans.Add(new HighlightedSpan(i, 1, ColDefault)); i++; continue; }
                        if (line[i] == '"' || line[i] == '\'')
                        {
                            char q = line[i];
                            int v0 = i++;
                            while (i < n && line[i] != q) i++;
                            if (i < n) i++;
                            spans.Add(new HighlightedSpan(v0, i - v0, ColValue));
                            continue;
                        }
                        spans.Add(new HighlightedSpan(i, 1, ColDefault));
                        i++;
                    }
                    if (i < n && line[i] == '>')
                    {
                        spans.Add(new HighlightedSpan(i, 1, ColTag));
                        i++;
                    }
                    continue;
                }
                int t0 = i;
                while (i < n && line[i] != '<') i++;
                if (i > t0) spans.Add(new HighlightedSpan(t0, i - t0, ColDefault));
            }
            return spans;
        }
    }

    public class ScriptEditorPanel : BasePanel
    {
        private class ScriptEditorUIOverlay : UIOverlay
        {
            private readonly ScriptEditorPanel _parent;
            public ScriptEditorUIOverlay(ScriptEditorPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window) { _parent = parent; }
            protected override void HandleDataHook(string hook) { }
            public override bool HandleUIClick(HtmlElement elem) => true;
        }

        private readonly List<string> _scriptFiles = new List<string>();
        private string _scriptsRoot;
        private string _selectedPath;
        private string _sourceBuffer = "";
        private readonly List<string> _displayLines = new List<string>();
        private bool _displayDirty = true;
        private bool _sourceDirty;
        private int _cursorLine;
        private int _cursorCol;
        private float _scrollY;
        private string _statusText = "Ready";
        private Vector4 _statusColor = new Vector4(0.55f, 0.55f, 0.55f, 1f);
        private bool _hasFocus;
        private double _lastCursorBlink;
        private bool _cursorVisible = true;
        private readonly Dictionary<Key, bool> _prevKey = new Dictionary<Key, bool>();
        private ISyntaxHighlighter _highlighter = new CSharpSyntaxHighlighter();

        // Width cache – invalidated whenever _displayDirty becomes true
        private readonly List<float> _lineWidthCache = new List<float>();
        private bool _widthsDirty = true;

        private const float ToolbarH = 34f;
        private const float FileListW = 210f;
        private const float GutterW = 48f;
        private const float FontSize = 13f;
        private const string FontFamily = "Consolas";
        private const float LinePad = 3f;
        private const float EditorPad = 6f;
        private const float FileRowH = 22f;
        private const float BtnH = 24f;
        private const float BtnPad = 6f;

        private float _btnRebuildX, _btnSaveX, _btnRefreshX, _btnW;

        public ScriptEditorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
            DockingMode = DockingMode.IDE;
            BaseWidth = 1024f;
            BaseHeight = 680f;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new ScriptEditorUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            _uiOverlay.LoadUI("<html><body style='margin:0;background:transparent'></body></html>");
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            RefreshFileList();
        }

        private string GetScriptsDir()
        {
            string project = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(project) || !Directory.Exists(project)) return null;
            string dir = Path.Combine(project, "Scripts");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void RefreshFileList()
        {
            _scriptFiles.Clear();
            _scriptsRoot = GetScriptsDir();
            if (_scriptsRoot == null) return;
            foreach (string f in Directory.GetFiles(_scriptsRoot, "*.cs", SearchOption.AllDirectories)
                                          .Concat(Directory.GetFiles(_scriptsRoot, "*.html", SearchOption.AllDirectories))
                                          .Concat(Directory.GetFiles(_scriptsRoot, "*.htm", SearchOption.AllDirectories)))
                _scriptFiles.Add(f);
            _scriptFiles.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private string DisplayName(string fullPath)
        {
            if (string.IsNullOrEmpty(_scriptsRoot) || string.IsNullOrEmpty(fullPath))
                return Path.GetFileName(fullPath) ?? fullPath;
            try { return Path.GetRelativePath(_scriptsRoot, fullPath).Replace('\\', '/'); }
            catch { return Path.GetFileName(fullPath); }
        }

        private void SelectFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (_sourceDirty && !string.IsNullOrEmpty(_selectedPath))
                SaveCurrentFile();
            _selectedPath = path;
            try { _sourceBuffer = File.ReadAllText(path); }
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
            _widthsDirty = true;
            UpdateHighlighter();
        }

        private void UpdateHighlighter()
        {
            string ext = Path.GetExtension(_selectedPath ?? "").ToLowerInvariant();
            if (ext == ".html" || ext == ".htm")
                _highlighter = new HtmlSyntaxHighlighter();
            else
                _highlighter = new CSharpSyntaxHighlighter();
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
            if (_sourceDirty) SaveCurrentFile();
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
                    ScriptLoader.BuildProjectScripts(project);
                    if (p.ExitCode == 0)
                    {
                        _statusText = "Build succeeded";
                        _statusColor = new Vector4(0.4f, 0.9f, 0.5f, 1f);
                    }
                    else
                    {
                        string err = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                        if (err.Length > 180) err = err.Substring(0, 177) + "…";
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

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            bool isTop = PanelManager.Current?.GetTopmostPanelAt(absMousePos) == this;
            if (!isTop) { _hasFocus = false; return; }
            float titleH = HasTitleBar ? TitleHeight : 0f;
            float listTop = titleH + ToolbarH;
            Vector2 local = absMousePos - Position;

            if (mousePressed && local.Y >= titleH && local.Y < titleH + ToolbarH)
            {
                if (HitBtn(local.X, _btnRebuildX)) { RebuildScripts(); return; }
                if (HitBtn(local.X, _btnSaveX)) { SaveCurrentFile(); return; }
                if (HitBtn(local.X, _btnRefreshX)) { RefreshFileList(); return; }
            }

            if (mousePressed && local.X >= 0 && local.X < FileListW && local.Y >= listTop && local.Y < Size.Y)
            {
                int row = (int)((local.Y - listTop) / FileRowH);
                if (row >= 0 && row < _scriptFiles.Count)
                    SelectFile(_scriptFiles[row]);
                return;
            }

            float editorLeft = FileListW + GutterW;
            float editorTop = listTop;
            float editorW = Size.X - editorLeft;
            float editorH = Size.Y - listTop;
            bool overEditor = local.X >= editorLeft && local.X <= Size.X &&
                               local.Y >= editorTop && local.Y <= Size.Y;
            if (mousePressed && overEditor)
            {
                _hasFocus = true;
                EnsureLines();
                float lineH = _uiOverlay.TextRenderer.GetLineHeight(FontSize, FontFamily) + LinePad;
                float relY = local.Y - editorTop + _scrollY;
                int lineIdx = Math.Clamp((int)(relY / lineH), 0, Math.Max(0, _displayLines.Count - 1));
                _cursorLine = lineIdx;
                string line = _displayLines[lineIdx];
                float relX = local.X - editorLeft - EditorPad;
                _cursorCol = MeasureColumn(line, relX);
            }
            else if (mousePressed && !overEditor)
            {
                _hasFocus = false;
            }

            if (scrollDelta != 0f && overEditor)
                _scrollY = Math.Max(0f, _scrollY - scrollDelta * 28f);

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
            ProcessKey(Key.Home, () => { _cursorCol = 0; });
            ProcessKey(Key.End, () => {
                EnsureLines();
                if (_cursorLine < _displayLines.Count)
                    _cursorCol = _displayLines[_cursorLine].Length;
            });
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

        private bool HitBtn(float mouseX, float btnX) =>
            mouseX >= btnX && mouseX <= btnX + _btnW;

        private int MeasureColumn(string line, float pixelX)
        {
            if (string.IsNullOrEmpty(line) || pixelX <= 0) return 0;
            float acc = 0f;
            for (int i = 0; i < line.Length; i++)
            {
                float w = GetCachedCharWidth(line[i]);
                if (acc + w * 0.5f >= pixelX) return i;
                acc += w;
            }
            return line.Length;
        }

        private float GetCachedCharWidth(char c)
        {
            // Single-character width via the now-cheap GetTextSize path
            return _uiOverlay.TextRenderer.GetTextSize(c.ToString(), FontSize, FontFamily).X;
        }

        private bool WasPressed(Key k)
        {
            bool now = _controlContext.GetKey(_window, k) == InputAction.Press;
            bool was = _prevKey.TryGetValue(k, out bool p) && p;
            _prevKey[k] = now;
            return now && !was;
        }

        private void ProcessKey(Key k, Action a) { if (WasPressed(k)) a(); }
        private void TryInsert(Key k, char c) { if (WasPressed(k)) InsertChar(c); }

        private void EnsureLines()
        {
            if (!_displayDirty) return;
            _displayLines.Clear();
            if (string.IsNullOrEmpty(_sourceBuffer))
                _displayLines.Add("");
            else
            {
                using (var r = new StringReader(_sourceBuffer))
                {
                    string line;
                    while ((line = r.ReadLine()) != null) _displayLines.Add(line);
                }
                if (_sourceBuffer.EndsWith("\n") || _sourceBuffer.EndsWith("\r\n"))
                    _displayLines.Add("");
            }
            _displayDirty = false;
            _widthsDirty = true;
        }

        private void EnsureLineWidths()
        {
            if (!_widthsDirty) return;
            _lineWidthCache.Clear();
            for (int i = 0; i < _displayLines.Count; i++)
            {
                string line = _displayLines[i] ?? "";
                _lineWidthCache.Add(_uiOverlay.TextRenderer.GetTextSize(line, FontSize, FontFamily).X);
            }
            _widthsDirty = false;
        }

        private float GetLineWidth(int lineIndex)
        {
            EnsureLineWidths();
            if (lineIndex < 0 || lineIndex >= _lineWidthCache.Count) return 0f;
            return _lineWidthCache[lineIndex];
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
            _widthsDirty = true;
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
            _widthsDirty = true;
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
            _widthsDirty = true;
        }

        private void MoveCursor(int dx, int dy)
        {
            EnsureLines();
            if (dy != 0)
            {
                _cursorLine = Math.Clamp(_cursorLine + dy, 0, Math.Max(0, _displayLines.Count - 1));
                _cursorCol = Math.Clamp(_cursorCol, 0, _displayLines[_cursorLine].Length);
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

            float[] tbBg = HtmlLayoutUtils.GetNdcQuad(0, titleH, Size.X, ToolbarH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(tbBg, new Vector4(0.16f, 0.16f, 0.17f, 1f));

            _btnW = 78f;
            float bx = 10f;
            _btnRebuildX = bx; DrawButton(bx, titleH + 5f, "Rebuild"); bx += _btnW + BtnPad;
            _btnSaveX = bx; DrawButton(bx, titleH + 5f, "Save"); bx += _btnW + BtnPad;
            _btnRefreshX = bx; DrawButton(bx, titleH + 5f, "Refresh");

            _uiOverlay.TextRenderer.RenderText(
                _statusText,
                bx + _btnW + 16f,
                titleH + 9f,
                Size.X, Size.Y, 12f, _statusColor, FontFamily);

            float[] listBg = HtmlLayoutUtils.GetNdcQuad(0, listTop, FileListW, listH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(listBg, new Vector4(0.11f, 0.11f, 0.12f, 1f));

            float[] div = HtmlLayoutUtils.GetNdcQuad(FileListW, listTop, 1f, listH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(div, new Vector4(0.25f, 0.25f, 0.27f, 1f));

            float rowY = listTop;
            for (int i = 0; i < _scriptFiles.Count; i++)
            {
                bool sel = string.Equals(_scriptFiles[i], _selectedPath, StringComparison.OrdinalIgnoreCase);
                if (sel)
                {
                    float[] rowBg = HtmlLayoutUtils.GetNdcQuad(0, rowY, FileListW, FileRowH, Matrix4x4.Identity, Size.X, Size.Y);
                    QuadRenderer.DrawNdcQuad(rowBg, new Vector4(0.18f, 0.28f, 0.20f, 1f));
                }
                string name = DisplayName(_scriptFiles[i]);
                name = ClipText(name, FileListW - 16f);
                _uiOverlay.TextRenderer.RenderText(
                    name, 8f, rowY + 4f, Size.X, Size.Y, FontSize,
                    sel ? new Vector4(0.486f, 1f, 0.796f, 1f) : new Vector4(0.78f, 0.78f, 0.78f, 1f),
                    FontFamily);
                rowY += FileRowH;
                if (rowY > Size.Y) break;
            }

            float editorLeft = FileListW + GutterW;
            float editorTop = listTop;
            float editorW = Size.X - editorLeft;
            float editorH = listH;
            if (editorW < 40f || editorH < 20f) return;

            float[] gutBg = HtmlLayoutUtils.GetNdcQuad(FileListW, listTop, GutterW, listH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(gutBg, new Vector4(0.13f, 0.13f, 0.14f, 1f));

            EnsureLines();
            EnsureLineWidths();

            float lineH = _uiOverlay.TextRenderer.GetLineHeight(FontSize, FontFamily) + LinePad;
            float totalH = _displayLines.Count * lineH;
            float maxScroll = Math.Max(0f, totalH - editorH + 20f);
            _scrollY = Math.Clamp(_scrollY, 0f, maxScroll);

            float y = editorTop - _scrollY;
            for (int i = 0; i < _displayLines.Count; i++)
            {
                if (y + lineH < editorTop) { y += lineH; continue; }
                if (y > editorTop + editorH) break;

                string num = (i + 1).ToString();
                float numW = _uiOverlay.TextRenderer.GetTextSize(num, FontSize, FontFamily).X;
                _uiOverlay.TextRenderer.RenderText(
                    num,
                    FileListW + GutterW - numW - 8f,
                    y,
                    Size.X, Size.Y, FontSize,
                    new Vector4(0.45f, 0.45f, 0.48f, 1f),
                    FontFamily);

                string text = _displayLines[i] ?? "";
                DrawHighlightedLine(text, editorLeft + EditorPad, y);

                if (_hasFocus && _cursorVisible && i == _cursorLine)
                {
                    string prefix = text.Length >= _cursorCol ? text.Substring(0, _cursorCol) : text;
                    float cx = editorLeft + EditorPad + _uiOverlay.TextRenderer.GetTextSize(prefix, FontSize, FontFamily).X;
                    float[] ndc = HtmlLayoutUtils.GetNdcQuad(cx, y, 2f, lineH - LinePad, Matrix4x4.Identity, Size.X, Size.Y);
                    QuadRenderer.DrawNdcQuad(ndc, new Vector4(0.486f, 1f, 0.796f, 1f));
                }
                y += lineH;
            }
        }

        private void DrawButton(float x, float y, string label)
        {
            float[] bg = HtmlLayoutUtils.GetNdcQuad(x, y, _btnW, BtnH, Matrix4x4.Identity, Size.X, Size.Y);
            QuadRenderer.DrawNdcQuad(bg, new Vector4(0.22f, 0.22f, 0.24f, 1f));
            float tw = _uiOverlay.TextRenderer.GetTextSize(label, 12f, FontFamily).X;
            _uiOverlay.TextRenderer.RenderText(
                label, x + (_btnW - tw) * 0.5f, y + 5f, Size.X, Size.Y, 12f,
                new Vector4(0.486f, 1f, 0.796f, 1f), FontFamily);
        }

        private string ClipText(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return text;
            float w = _uiOverlay.TextRenderer.GetTextSize(text, FontSize, FontFamily).X;
            if (w <= maxWidth) return text;
            const string ell = "…";
            float ew = _uiOverlay.TextRenderer.GetTextSize(ell, FontSize, FontFamily).X;
            for (int len = text.Length - 1; len > 0; len--)
            {
                string sub = text.Substring(0, len);
                if (_uiOverlay.TextRenderer.GetTextSize(sub, FontSize, FontFamily).X + ew <= maxWidth)
                    return sub + ell;
            }
            return ell;
        }

        private void DrawHighlightedLine(string line, float x, float y)
        {
            if (string.IsNullOrEmpty(line)) return;
            var spans = _highlighter.HighlightLine(line);
            float cx = x;
            int cursor = 0;
            foreach (var sp in spans)
            {
                if (sp.Start > cursor)
                {
                    string gap = line.Substring(cursor, sp.Start - cursor);
                    _uiOverlay.TextRenderer.RenderText(gap, cx, y, Size.X, Size.Y, FontSize,
                        new Vector4(0.85f, 0.85f, 0.85f, 1f), FontFamily);
                    cx += _uiOverlay.TextRenderer.GetTextSize(gap, FontSize, FontFamily).X;
                }
                string tok = line.Substring(sp.Start, sp.Length);
                _uiOverlay.TextRenderer.RenderText(tok, cx, y, Size.X, Size.Y, FontSize, sp.Color, FontFamily);
                cx += _uiOverlay.TextRenderer.GetTextSize(tok, FontSize, FontFamily).X;
                cursor = sp.Start + sp.Length;
            }
            if (cursor < line.Length)
            {
                string rest = line.Substring(cursor);
                _uiOverlay.TextRenderer.RenderText(rest, cx, y, Size.X, Size.Y, FontSize,
                    new Vector4(0.85f, 0.85f, 0.85f, 1f), FontFamily);
            }
        }

        public override void OnPanelResize(float w, float h)
        {
            base.OnPanelResize(w, h);
            _displayDirty = true;
            _widthsDirty = true;
        }

        public override void OnLiveResize(float w, float h)
        {
            base.OnLiveResize(w, h);
            _displayDirty = true;
            _widthsDirty = true;
        }

        public override void Dispose()
        {
            if (_sourceDirty) SaveCurrentFile();
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new ScriptEditorPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}