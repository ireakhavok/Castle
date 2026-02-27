// Folder: SiegeEngine.Core.UI
// File: UIOverlay.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.UI.JSParser;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Rendering.Shaders;

namespace SiegeEngine.Core.UI
{
    public class UIOverlay
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly nint _window;
        protected ShaderProgram _uiShader;
        protected TextRenderer _textRenderer;
        protected UIQuadRenderer _quadRenderer;
        protected CssParser _cssParser;
        public HtmlElement _uiRoot;
        protected List<HtmlElement> _uiClickables = new List<HtmlElement>();
        protected string _currentBaseDir = "";
        private bool _justOpenedSelect = false;
        private bool _prevMouseDown = false;
        private List<SelectElement> _openSelects = new List<SelectElement>();
        private JSContext _jsContext = new JSContext();
        public JSDocument _document;
        private HtmlElement _currentFocused;
        private readonly Dictionary<Key, double> _keyDownTime = new Dictionary<Key, double>();
        private readonly Dictionary<Key, double> _lastAddTime = new Dictionary<Key, double>();
        private const double InitialRepeatDelay = 0.5;
        private const double RepeatRate = 0.05;
        public HtmlElement FocusedElement => _currentFocused;
        public float PanelWidth { get; set; }
        public float PanelHeight { get; set; }
        public float ScrollOffsetY { get; set; } = 0f;
        public float ContentFullHeight { get; private set; } = 0f;
        private bool _needsVerticalScrollbar = false;
        public bool DidHandleClick { get; private set; }

        public UIOverlay(IRenderContext renderContext, IControlContext controlContext, nint window)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
        }

        public virtual void Init()
        {
            _uiShader = new ShaderProgram(_renderContext, UiShader.VertexSource, UiShader.FragmentSource);
            _textRenderer = new TextRenderer(_renderContext, _window);
            _textRenderer.Initialize(_uiShader);
            _quadRenderer = new UIQuadRenderer(_renderContext);
            _cssParser = new CssParser();
        }

        public void LoadUI(string html, string baseDir = "")
        {
            _currentBaseDir = baseDir;
            HtmlParser parser = new HtmlParser();
            _cssParser.Clear();
            _uiRoot = parser.Parse(html);
            List<string> cssBlocks = new List<string>();
            List<string> scriptBlocks = new List<string>();
            Queue<HtmlElement> q = new Queue<HtmlElement>();
            q.Enqueue(_uiRoot);
            while (q.Count > 0)
            {
                var e = q.Dequeue();
                if (e.Tag.ToLower() == "style")
                {
                    var text = e.Children.FirstOrDefault(c => c is TextElement) as TextElement;
                    if (text != null) cssBlocks.Add(text.Content);
                    if (e.Parent != null) e.Parent.Children.Remove(e);
                }
                else if (e.Tag.ToLower() == "script")
                {
                    var text = e.Children.FirstOrDefault(c => c is TextElement) as TextElement;
                    if (text != null)
                    {
                        string scriptContent = text.Content.Trim();
                        scriptBlocks.Add(scriptContent);
                    }
                    if (e.Parent != null) e.Parent.Children.Remove(e);
                }
                foreach (var c in e.Children) q.Enqueue(c);
            }
            _cssParser.Apply(CssParser.DefaultUserAgentCss);
            foreach (var css in cssBlocks)
            {
                _cssParser.Apply(css);
            }
            _cssParser.ApplyInlineStyles(_uiRoot);
            InitializeElementProperties(_uiRoot);
            _cssParser.ApplyAll(_uiRoot);
            InheritProperties(_uiRoot, null);
            _uiRoot.PrepareResources(baseDir, _controlContext, _window, _renderContext, _uiShader);
            _uiClickables.Clear();
            CollectClickables(_uiRoot);
            _document = new JSDocument(this);
            _jsContext.Evaluator.RegisterGlobal("document", _document);
            foreach (var script in scriptBlocks)
            {
                _jsContext.Run(script);
            }
            RefreshUI();
        }

        private void InitializeElementProperties(HtmlElement root)
        {
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                if (elem.Attributes.TryGetValue("onclick", out string val)) elem.OnClickJS = val;
                if (elem.Attributes.TryGetValue("onchange", out val)) elem.OnChangeJS = val;
                if (elem.Attributes.TryGetValue("onmouseenter", out val)) elem.OnMouseEnterJS = val;
                if (elem.Attributes.TryGetValue("onmouseleave", out val)) elem.OnMouseLeaveJS = val;
                if (elem.Attributes.TryGetValue("onmouseover", out val)) elem.OnMouseOverJS = val;
                if (elem.Attributes.TryGetValue("onmouseout", out val)) elem.OnMouseOutJS = val;
                if (elem.Attributes.TryGetValue("onmousedown", out val)) elem.OnMouseDownJS = val;
                if (elem.Attributes.TryGetValue("onmouseup", out val)) elem.OnMouseUpJS = val;
                if (elem.Attributes.TryGetValue("onfocus", out val)) elem.OnFocusJS = val;
                if (elem.Attributes.TryGetValue("onblur", out val)) elem.OnBlurJS = val;
                if (elem is InputElement input)
                {
                    input.Type = elem.Attributes.GetValueOrDefault("type", "text");
                    input.Checked = elem.Attributes.ContainsKey("checked");
                    input.Value = elem.Attributes.GetValueOrDefault("value", "");
                    input.Placeholder = elem.Attributes.GetValueOrDefault("placeholder", "");
                }
                foreach (var child in elem.Children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        private void InheritProperties(HtmlElement elem, HtmlElement parent)
        {
            if (parent != null)
            {
                if (string.IsNullOrEmpty(elem.Style.Color))
                {
                    elem.Style.Color = parent.Style.Color;
                    elem.Style.TextColor = parent.Style.TextColor;
                }
                if (string.IsNullOrEmpty(elem.Style.FontSizeStr))
                    elem.Style.FontSizeStr = parent.Style.FontSizeStr;
                if (string.IsNullOrEmpty(elem.Style.TextAlign))
                    elem.Style.TextAlign = parent.Style.TextAlign;
                if (elem is InputElement inp && (inp.Type == "text" || inp.Type == "number") && elem.Style.BackgroundColor == Vector4.Zero)
                    elem.Style.BackgroundColor = parent.Style.BackgroundColor;
            }
            foreach (var child in elem.Children)
                InheritProperties(child, elem);
        }

        private void CollectClickables(HtmlElement elem)
        {
            if (elem.GetEffectiveDisplay() == "none") return;
            string classes = elem.Attributes.GetValueOrDefault("class", "");
            string tagLower = elem.Tag.ToLower();
            if (classes.Contains("button") || classes.Contains("toggle") || tagLower == "select" || tagLower == "label" || tagLower == "a" || elem.Attributes.ContainsKey("data-hook") || elem.Attributes.ContainsKey("onclick") || classes.Contains("select-option") || tagLower == "option" || elem.Attributes.ContainsKey("onchange") || elem.Attributes.ContainsKey("onmouseenter") || elem.Attributes.ContainsKey("onmouseleave") || elem.Attributes.ContainsKey("onmouseover") || elem.Attributes.ContainsKey("onmouseout") || elem.Attributes.ContainsKey("onmousedown") || elem.Attributes.ContainsKey("onmouseup") || elem.Attributes.ContainsKey("onfocus") || elem.Attributes.ContainsKey("onblur") || tagLower == "input")
            {
                _uiClickables.Add(elem);
            }
            foreach (var child in elem.Children)
                CollectClickables(child);
        }

        protected virtual void HandleDataHook(string hook)
        {
        }

        protected virtual void HandleLink(string href)
        {
            if (string.IsNullOrEmpty(href)) return;
            string resolvedPath = Path.GetFullPath(Path.Combine(_currentBaseDir, href));
            if (File.Exists(resolvedPath))
            {
                LoadUI(File.ReadAllText(resolvedPath), Path.GetDirectoryName(resolvedPath) ?? "");
            }
            else
            {
                Console.WriteLine($"UIOverlay: Failed to load relative path: {resolvedPath}");
            }
        }

        public void RefreshUI()
        {
            if (_uiRoot == null) return;
            _cssParser.ApplyAll(_uiRoot);
            InheritProperties(_uiRoot, null);
            RecomputeLayout(PanelWidth, PanelHeight);
            _uiClickables.Clear();
            CollectClickables(_uiRoot);
        }

        public HtmlElement FindElementById(string id)
        {
            return FindElementById(_uiRoot, id);
        }

        protected HtmlElement FindElementById(HtmlElement root, string id)
        {
            if (root == null) return null;
            if (root.Attributes.GetValueOrDefault("id", "") == id) return root;
            foreach (var child in root.Children)
            {
                var found = FindElementById(child, id);
                if (found != null) return found;
            }
            return null;
        }

        public List<HtmlElement> FindElementsByClass(string className)
        {
            return FindElementsByClass(_uiRoot, className);
        }

        protected List<HtmlElement> FindElementsByClass(HtmlElement root, string className)
        {
            if (root == null) return new List<HtmlElement>();
            List<HtmlElement> list = new List<HtmlElement>();
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                string classes = elem.Attributes.GetValueOrDefault("class", "");
                if (classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className)) list.Add(elem);
                foreach (var child in elem.Children) queue.Enqueue(child);
            }
            return list;
        }

        public List<HtmlElement> FindElementsByTag(string tag)
        {
            return FindElementsByTag(_uiRoot, tag);
        }

        protected List<HtmlElement> FindElementsByTag(HtmlElement root, string tag)
        {
            if (root == null) return new List<HtmlElement>();
            List<HtmlElement> list = new List<HtmlElement>();
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                if (tag == "*" || elem.Tag.ToLower() == tag.ToLower()) list.Add(elem);
                foreach (var child in elem.Children) queue.Enqueue(child);
            }
            return list;
        }

        protected virtual void HandleUIClick(HtmlElement elem)
        {
            if (elem == null) return;
            Console.WriteLine($"UIOverlay: Handling click for element Tag={elem.Tag}, Class={elem.Attributes.GetValueOrDefault("class", "")}, ID={elem.Attributes.GetValueOrDefault("id", "")}");
            bool valueChanged = false;
            if (!string.IsNullOrEmpty(elem.OnClickJS))
            {
                _jsContext.RunWithThis(elem.OnClickJS, new JSElement(elem, this));
            }
            InvokeListeners(elem, "click");
            if (elem.Tag == "a")
            {
                string href = elem.Attributes.GetValueOrDefault("href", "");
                if (string.IsNullOrEmpty(href)) return;
                if (href.StartsWith("#"))
                {
                    string targetId = href.Substring(1);
                    var target = FindElementById(targetId);
                    if (target != null)
                    {
                        var oldTargets = FindElementsByTag("*").Where(e => e.IsTarget).ToList();
                        foreach (var old in oldTargets) old.IsTarget = false;
                        target.IsTarget = true;
                        RefreshUI();
                        Console.WriteLine($"UIOverlay: Handled anchor click to #{targetId}");
                    }
                }
                else
                {
                    HandleLink(href);
                }
            }
            else if (elem.Tag == "label")
            {
                string forId = elem.Attributes.GetValueOrDefault("for", "");
                if (!string.IsNullOrEmpty(forId))
                {
                    var input = FindElementById(forId);
                    if (input != null && input.Tag == "input")
                    {
                        string type = input.Attributes.GetValueOrDefault("type", "");
                        if (type == "radio")
                        {
                            string name = input.Attributes.GetValueOrDefault("name", "");
                            var radios = FindElementsByTag("input").Where(i => i.Attributes.GetValueOrDefault("type", "") == "radio" && i.Attributes.GetValueOrDefault("name", "") == name).ToList();
                            foreach (var r in radios) r.Checked = false;
                            input.Checked = true;
                            valueChanged = true;
                            RefreshUI();
                            Console.WriteLine($"UIOverlay: Handled radio label click for {forId}");
                        }
                        else if (type == "checkbox")
                        {
                            input.Checked = !input.Checked;
                            valueChanged = true;
                            RefreshUI();
                            Console.WriteLine($"UIOverlay: Handled checkbox label click for {forId}");
                        }
                        else if (type == "text" || type == "number")
                        {
                            if (!input.IsFocused)
                            {
                                if (!string.IsNullOrEmpty(input.OnFocusJS))
                                {
                                    _jsContext.RunWithThis(input.OnFocusJS, new JSElement(input, this));
                                }
                                InvokeListeners(input, "focus");
                                input.IsFocused = true;
                                _currentFocused = input;
                                Console.WriteLine($"UIOverlay: Focused {(type == "number" ? "number" : "text")} input via label {forId}");
                            }
                        }
                    }
                }
            }
            else if (elem.Attributes.GetValueOrDefault("class", "").Contains("toggle"))
            {
                var input = elem.Children.FirstOrDefault(c => c.Tag == "input" && c.Attributes.GetValueOrDefault("type", "") == "checkbox");
                if (input != null)
                {
                    input.Checked = !input.Checked;
                    valueChanged = true;
                    RefreshUI();
                    Console.WriteLine($"UIOverlay: Handled toggle click");
                }
            }
            else if (elem.Attributes.ContainsKey("data-hook"))
            {
                string hook = elem.Attributes["data-hook"];
                Console.WriteLine($"UIOverlay: Processing data-hook: {hook}");
                HandleDataHook(hook);
            }
            else if (elem.Tag == "input")
            {
                var input = elem as InputElement;
                if (input != null)
                {
                    if (input.Type == "checkbox" || input.Type == "radio")
                    {
                        input.Checked = !input.Checked;
                        valueChanged = true;
                        RefreshUI();
                    }
                    else if (input.Type == "text" || input.Type == "number")
                    {
                        if (!input.IsFocused)
                        {
                            if (!string.IsNullOrEmpty(input.OnFocusJS))
                            {
                                _jsContext.RunWithThis(input.OnFocusJS, new JSElement(input, this));
                            }
                            InvokeListeners(input, "focus");
                            input.IsFocused = true;
                            _currentFocused = input;
                            Console.WriteLine($"UIOverlay: Focused {(input.Type == "number" ? "number" : "text")} input {input.Attributes.GetValueOrDefault("id", "")}");
                        }
                    }
                }
            }
            else if (elem.Tag == "select")
            {
                var select = elem as SelectElement;
                if (select != null)
                {
                    CloseAllOpenSelects();
                    select.IsOpen = !select.IsOpen;
                    _justOpenedSelect = select.IsOpen;
                    RefreshUI();
                }
            }
            else if (elem.Tag == "option")
            {
                var select = elem.Parent as SelectElement;
                if (select != null)
                {
                    if (select.IsOpen)
                    {
                        foreach (var opt in select.Children.Where(c => c.Tag.ToLower() == "option"))
                        {
                            opt.Attributes.Remove("selected");
                        }
                        elem.Attributes["selected"] = "";
                        select.IsOpen = false;
                        valueChanged = true;
                    }
                    else
                    {
                        CloseAllOpenSelects();
                        select.IsOpen = true;
                        _justOpenedSelect = true;
                    }
                    RefreshUI();
                }
            }
            if (valueChanged)
            {
                TriggerChange(elem);
            }
            RefreshUI();
        }

        private void CloseAllOpenSelects()
        {
            var selects = FindElementsByTag("select");
            foreach (var s in selects)
            {
                if (s is SelectElement sel)
                {
                    sel.IsOpen = false;
                }
            }
        }

        private char? GetCharFromKey(Key key, bool shiftPressed, string inputType)
        {
            if (inputType == "number")
            {
                if (key >= Key.Key0 && key <= Key.Key9)
                {
                    return (char)((int)key - (int)Key.Key0 + '0');
                }
                if (key == Key.Period)
                {
                    return '.';
                }
                if (key == Key.Minus)
                {
                    return '-';
                }
                return null;
            }
            if (key >= Key.A && key <= Key.Z)
            {
                return (char)((int)key - (int)Key.A + (shiftPressed ? 'A' : 'a'));
            }
            else if (key >= Key.Key0 && key <= Key.Key9)
            {
                char noShift = (char)((int)key - (int)Key.Key0 + '0');
                char withShift = key switch
                {
                    Key.Key0 => ')',
                    Key.Key1 => '!',
                    Key.Key2 => '@',
                    Key.Key3 => '#',
                    Key.Key4 => '$',
                    Key.Key5 => '%',
                    Key.Key6 => '^',
                    Key.Key7 => '&',
                    Key.Key8 => '*',
                    Key.Key9 => '(',
                    _ => noShift
                };
                return shiftPressed ? withShift : noShift;
            }
            else if (key == Key.Space)
            {
                return ' ';
            }
            else if (key == Key.Minus)
            {
                return shiftPressed ? '_' : '-';
            }
            else if (key == Key.Equal)
            {
                return shiftPressed ? '+' : '=';
            }
            else if (key == Key.LeftBracket)
            {
                return shiftPressed ? '{' : '[';
            }
            else if (key == Key.RightBracket)
            {
                return shiftPressed ? '}' : ']';
            }
            else if (key == Key.Backslash)
            {
                return shiftPressed ? '|' : '\\';
            }
            else if (key == Key.Semicolon)
            {
                return shiftPressed ? ':' : ';';
            }
            else if (key == Key.Apostrophe)
            {
                return shiftPressed ? '"' : '\'';
            }
            else if (key == Key.Comma)
            {
                return shiftPressed ? '<' : ',';
            }
            else if (key == Key.Period)
            {
                return shiftPressed ? '>' : '.';
            }
            else if (key == Key.Slash)
            {
                return shiftPressed ? '?' : '/';
            }
            else if (key == Key.GraveAccent)
            {
                return shiftPressed ? '~' : '`';
            }
            return null;
        }

        public virtual void Update(float deltaTime, Vector2 relMousePos, bool currentMouseDown, float panelW, float panelH)
        {
            if (_uiRoot == null) return;
            DidHandleClick = false;
            PanelWidth = panelW;
            PanelHeight = panelH;
            Vector2 scrolledMousePos = new Vector2(relMousePos.X, relMousePos.Y + ScrollOffsetY);
            bool mousePress = !_prevMouseDown && currentMouseDown;
            bool mouseRelease = _prevMouseDown && !currentMouseDown;
            float vw = PanelWidth;
            float vh = PanelHeight;
            HtmlElement clickedElem = null;
            bool isClickOnOpenSelect = false;
            _openSelects = FindElementsByTag("select").Where(s => (s as SelectElement)?.IsOpen ?? false).Cast<SelectElement>().ToList();
            SelectElement openSelect = _openSelects.FirstOrDefault();
            if (openSelect != null)
            {
                if (openSelect.HandleClick(scrolledMousePos, vw, vh))
                {
                    isClickOnOpenSelect = true;
                }
            }

            // Full snapshot to prevent "Collection was modified" when RefreshUI() is called from JS callbacks
            var clickablesSnapshot = _uiClickables.ToList();

            // Collect ranges that need 'input' event (for real-time slider → number field)
            var rangesNeedingInput = new List<HtmlElement>();

            foreach (var clickable in clickablesSnapshot)
            {
                if (openSelect != null && !clickable.IsDescendantOf(openSelect) && !(clickable == openSelect))
                {
                    continue;
                }
                bool wasHover = clickable.IsHover;
                bool wasActive = clickable.IsActive;
                bool over = clickable.HandleClick(scrolledMousePos, vw, vh);

                if (over && mousePress)
                {
                    if (!string.IsNullOrEmpty(clickable.OnMouseDownJS))
                    {
                        _jsContext.RunWithThis(clickable.OnMouseDownJS, new JSElement(clickable, this));
                    }
                    InvokeListeners(clickable, "mousedown");
                    clickable.IsActive = true;
                }
                if (over && mouseRelease)
                {
                    if (!string.IsNullOrEmpty(clickable.OnMouseUpJS))
                    {
                        _jsContext.RunWithThis(clickable.OnMouseUpJS, new JSElement(clickable, this));
                    }
                    InvokeListeners(clickable, "mouseup");
                }
                if (over && mouseRelease && wasActive)
                {
                    clickedElem = clickable;
                }
                if (!wasHover && over)
                {
                    if (!string.IsNullOrEmpty(clickable.OnMouseEnterJS))
                    {
                        _jsContext.RunWithThis(clickable.OnMouseEnterJS, new JSElement(clickable, this));
                    }
                    InvokeListeners(clickable, "mouseenter");
                    if (!string.IsNullOrEmpty(clickable.OnMouseOverJS))
                    {
                        _jsContext.RunWithThis(clickable.OnMouseOverJS, new JSElement(clickable, this));
                    }
                    InvokeListeners(clickable, "mouseover");
                }
                if (wasHover && !over)
                {
                    if (!string.IsNullOrEmpty(clickable.OnMouseLeaveJS))
                    {
                        _jsContext.RunWithThis(clickable.OnMouseLeaveJS, new JSElement(clickable, this));
                    }
                    InvokeListeners(clickable, "mouseleave");
                    if (!string.IsNullOrEmpty(clickable.OnMouseOutJS))
                    {
                        _jsContext.RunWithThis(clickable.OnMouseOutJS, new JSElement(clickable, this));
                    }
                    InvokeListeners(clickable, "mouseout");
                }
                clickable.IsHover = over;
                if (mouseRelease)
                {
                    clickable.IsActive = false;
                }

                // Collect for real-time 'input' event (slider drag)
                if (over && clickable.IsActive && clickable is InputElement inp && inp.Type == "range")
                {
                    rangesNeedingInput.Add(clickable);
                }
            }

            if (clickedElem != null)
            {
                DidHandleClick = true;
                bool focusable = clickedElem.Tag.ToLower() == "input" || clickedElem.Tag.ToLower() == "select" || clickedElem.Tag.ToLower() == "button" || clickedElem.Attributes.ContainsKey("tabindex") || !string.IsNullOrEmpty(clickedElem.OnFocusJS) || !string.IsNullOrEmpty(clickedElem.OnBlurJS);
                if (focusable)
                {
                    if (_currentFocused != null && _currentFocused != clickedElem)
                    {
                        if (!string.IsNullOrEmpty(_currentFocused.OnBlurJS))
                        {
                            _jsContext.RunWithThis(_currentFocused.OnBlurJS, new JSElement(_currentFocused, this));
                        }
                        InvokeListeners(_currentFocused, "blur");
                        _currentFocused.IsFocused = false;
                    }
                    if (!clickedElem.IsFocused)
                    {
                        if (!string.IsNullOrEmpty(clickedElem.OnFocusJS))
                        {
                            _jsContext.RunWithThis(clickedElem.OnFocusJS, new JSElement(clickedElem, this));
                        }
                        InvokeListeners(clickedElem, "focus");
                        clickedElem.IsFocused = true;
                        _currentFocused = clickedElem;
                    }
                }
                HandleUIClick(clickedElem);
            }
            else if (mouseRelease && openSelect != null && !isClickOnOpenSelect && !_justOpenedSelect)
            {
                CloseAllOpenSelects();
                RefreshUI();
            }
            _justOpenedSelect = false;
            _prevMouseDown = currentMouseDown;

            // Fire real-time 'input' for sliders AFTER enumeration - prevents Collection modified
            // This is what makes the arrow function update the paired number field instantly
            foreach (var rangeElem in rangesNeedingInput)
            {
                InvokeListeners(rangeElem, "input");
                TriggerChange(rangeElem);
            }

            bool needsRefresh = false;
            bool changed = false;
            if (_currentFocused is InputElement input && (input.Type == "text" || input.Type == "number"))
            {
                bool shiftPressed = _controlContext.GetKey(_window, Key.LeftShift) == InputAction.Press ||
                                    _controlContext.GetKey(_window, Key.RightShift) == InputAction.Press;
                double currentTime = _controlContext.GetTime();
                changed = false;
                foreach (Key key in Enum.GetValues(typeof(Key)))
                {
                    InputAction state = _controlContext.GetKey(_window, key);
                    if (state == InputAction.Press)
                    {
                        if (!_keyDownTime.ContainsKey(key))
                        {
                            _keyDownTime[key] = currentTime;
                            _lastAddTime[key] = currentTime;
                            if (key == Key.Backspace)
                            {
                                if (input.Value.Length > 0)
                                {
                                    input.Value = input.Value.Substring(0, input.Value.Length - 1);
                                    changed = true;
                                }
                            }
                            else
                            {
                                char? ch = GetCharFromKey(key, shiftPressed, input.Type);
                                if (ch.HasValue)
                                {
                                    if (input.Type == "number")
                                    {
                                        if (ch == '.' && input.Value.Contains('.')) continue;
                                        if (ch == '-' && input.Value.Length > 0 && !input.Value.StartsWith("-")) continue;
                                        if (ch == '-' && input.Value.StartsWith("-")) continue;
                                    }
                                    input.Value += ch.Value;
                                    changed = true;
                                }
                            }
                        }
                        else if (currentTime - _keyDownTime[key] > InitialRepeatDelay && currentTime - _lastAddTime[key] > RepeatRate)
                        {
                            _lastAddTime[key] = currentTime;
                            if (key == Key.Backspace)
                            {
                                if (input.Value.Length > 0)
                                {
                                    input.Value = input.Value.Substring(0, input.Value.Length - 1);
                                    changed = true;
                                }
                            }
                            else
                            {
                                char? ch = GetCharFromKey(key, shiftPressed, input.Type);
                                if (ch.HasValue)
                                {
                                    if (input.Type == "number")
                                    {
                                        if (ch == '.' && input.Value.Contains('.')) continue;
                                        if (ch == '-' && input.Value.Length > 0 && !input.Value.StartsWith("-")) continue;
                                        if (ch == '-' && input.Value.StartsWith("-")) continue;
                                    }
                                    input.Value += ch.Value;
                                    changed = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        _keyDownTime.Remove(key);
                        _lastAddTime.Remove(key);
                    }
                }
                if (changed)
                {
                    RefreshUI();
                    InvokeListeners(input, "input");
                    TriggerChange(input);
                }
                needsRefresh = input.Update(deltaTime, _controlContext, _window);
            }
            if (needsRefresh)
            {
                RefreshUI();
            }
        }

        protected virtual void RenderUI(float w, float h)
        {
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            Matrix4x4 scrollMatrix = Matrix4x4.CreateTranslation(0, -ScrollOffsetY, 0);
            _uiRoot.Render(_renderContext, _textRenderer, _quadRenderer, w, h, scrollMatrix);
            foreach (var sel in _openSelects)
            {
                sel.RenderDropdown(_renderContext, _textRenderer, _quadRenderer, w, h);
            }
            if (_needsVerticalScrollbar)
            {
                float trackX = w - 12f;
                float trackY = 0f;
                float trackW = 12f;
                float trackH = h;
                float[] trackNdc = HtmlElement.GetNdcQuad(trackX, trackY, trackW, trackH, Matrix4x4.Identity, w, h);
                _quadRenderer.DrawNdcQuad(trackNdc, new Vector4(0.15f, 0.15f, 0.15f, 0.95f));
                float thumbRatio = h / ContentFullHeight;
                float thumbH = Math.Max(30f, trackH * thumbRatio);
                float thumbY = (ScrollOffsetY / (ContentFullHeight - h)) * (trackH - thumbH);
                float[] thumbNdc = HtmlElement.GetNdcQuad(trackX + 1f, thumbY, trackW - 2f, thumbH, Matrix4x4.Identity, w, h);
                _quadRenderer.DrawNdcQuad(thumbNdc, new Vector4(0.55f, 0.55f, 0.55f, 1f));
            }
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public virtual void Render()
        {
            if (_uiRoot != null)
            {
                RenderUI(PanelWidth, PanelHeight);
            }
        }

        public void RecomputeLayout(float w, float h)
        {
            if (_uiRoot == null) return;
            _uiRoot.ComputeLayout(0, 0, w, h, w, h, _textRenderer, 16f);
            _uiRoot.UpdateFullTransforms(Matrix4x4.Identity);
            UpdateContentHeight();
        }

        private void UpdateContentHeight()
        {
            if (_uiRoot == null) return;
            ContentFullHeight = 0f;
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(_uiRoot);
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                if (elem.GetEffectiveDisplay() != "none")
                {
                    float elemBottom = elem.ComputedPosition.Y + elem.ComputedHeight;
                    ContentFullHeight = Math.Max(ContentFullHeight, elemBottom);
                }
                foreach (var child in elem.Children)
                {
                    queue.Enqueue(child);
                }
            }
            _needsVerticalScrollbar = ContentFullHeight > PanelHeight + 0.1f;
            if (_needsVerticalScrollbar)
            {
                ScrollOffsetY = Math.Clamp(ScrollOffsetY, 0f, ContentFullHeight - PanelHeight);
            }
            else
            {
                ScrollOffsetY = 0f;
            }
        }

        public void Scroll(float deltaY)
        {
            if (!_needsVerticalScrollbar) return;
            ScrollOffsetY -= deltaY * 30f;
            ScrollOffsetY = Math.Clamp(ScrollOffsetY, 0f, ContentFullHeight - PanelHeight);
        }

        public virtual void Dispose()
        {
            _uiShader.Dispose();
            _textRenderer.Dispose();
            _uiRoot = null;
            _uiClickables.Clear();
        }

        public virtual void TriggerChange(HtmlElement elem)
        {
            var current = elem;
            while (current != null)
            {
                if (!string.IsNullOrEmpty(current.OnChangeJS))
                {
                    _jsContext.RunWithThis(current.OnChangeJS, new JSElement(current, this));
                }
                InvokeListeners(current, "change");
                current = current.Parent;
            }
        }

        public void InvokeListeners(HtmlElement elem, string eventName)
        {
            if (elem.EventListeners.ContainsKey(eventName))
            {
                var jsElem = new JSElement(elem, this);
                foreach (var cb in elem.EventListeners[eventName])
                {
                    _jsContext.Evaluator.CallFunction(cb, new List<object>());
                }
            }
        }
    }
}