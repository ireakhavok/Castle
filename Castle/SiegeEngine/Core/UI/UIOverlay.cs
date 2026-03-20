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
using SiegeEngine.Core.UI.Elements;

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
        public List<HtmlElement> _uiClickables = new List<HtmlElement>();
        protected string _currentBaseDir = "";
        public JSContext _jsContext = new JSContext();
        public JSDocument _document;
        public float PanelWidth { get; set; }
        public float PanelHeight { get; set; }
        public float ScrollOffsetY { get; set; } = 0f;
        public float ContentFullHeight { get; private set; } = 0f;
        private bool _needsVerticalScrollbar = false;
        public bool DidHandleClick { get; set; }
        private UIInteractionLayer _interactionLayer;

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
            _interactionLayer = new UIInteractionLayer(this, _controlContext, _window);
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
            if (classes.Contains("button") || classes.Contains("toggle") || tagLower == "select" || tagLower == "label" || tagLower == "a" || elem.Attributes.ContainsKey("data-hook") || elem.Attributes.ContainsKey("onclick") || classes.Contains("select-option") || tagLower == "option" || elem.Attributes.ContainsKey("onchange") || elem.Attributes.ContainsKey("onmouseenter") || elem.Attributes.ContainsKey("onmouseleave") || elem.Attributes.ContainsKey("onmouseover") || elem.Attributes.ContainsKey("onmouseout") || elem.Attributes.ContainsKey("onmousedown") || elem.Attributes.ContainsKey("onmouseup") || elem.Attributes.ContainsKey("onfocus") || elem.Attributes.ContainsKey("onblur") || tagLower == "input" || (tagLower == "li" && (classes.Contains("nav-dropdown") || elem.Children.Any(c => c.Tag.ToLower() == "ul"))))
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
            _uiRoot.MarkIntrinsicDirty();
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

        public virtual void HandleUIClick(HtmlElement elem)
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
                                _interactionLayer._currentFocused = input;
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
                            _interactionLayer._currentFocused = input;
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
                    _interactionLayer._justOpenedSelect = select.IsOpen;
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
                        if (select.Attributes.ContainsKey("data-hook"))
                        {
                            HandleDataHook(select.Attributes["data-hook"]);
                        }
                    }
                    else
                    {
                        CloseAllOpenSelects();
                        select.IsOpen = true;
                        _interactionLayer._justOpenedSelect = true;
                    }
                    RefreshUI();
                }
            }
            if (elem.Attributes.ContainsKey("data-hook"))
            {
                string hook = elem.Attributes["data-hook"];
                Console.WriteLine($"UIOverlay: Processing data-hook: {hook}");
                HandleDataHook(hook);
            }
            if (valueChanged)
            {
                TriggerChange(elem);
            }
            RefreshUI();
        }

        public void CloseAllOpenSelects()
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

        public virtual void Update(float deltaTime, Vector2 relMousePos, bool currentMouseDown, float panelW, float panelH)
        {
            _interactionLayer.Update(deltaTime, relMousePos, currentMouseDown, panelW, panelH);
        }

        protected virtual void RenderUI(float w, float h)
        {
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);

            Matrix4x4 rootMatrix = Matrix4x4.CreateTranslation(0, -ScrollOffsetY, 0);
            _uiRoot.Render(_renderContext, _textRenderer, _quadRenderer, w, h, rootMatrix);

            foreach (var sel in _interactionLayer._openSelects)
            {
                sel.RenderDropdown(_renderContext, _textRenderer, _quadRenderer, w, h);
            }

            if (_needsVerticalScrollbar)
            {
                float trackX = w - 12f;
                float trackY = 0f;
                float trackW = 12f;
                float trackH = h;
                float[] trackNdc = HtmlLayoutUtils.GetNdcQuad(trackX, trackY, trackW, trackH, Matrix4x4.Identity, w, h);
                _quadRenderer.DrawNdcQuad(trackNdc, new Vector4(0.15f, 0.15f, 0.15f, 0.95f));
                float thumbRatio = h / ContentFullHeight;
                float thumbH = Math.Max(30f, trackH * thumbRatio);
                float thumbY = (ScrollOffsetY / (ContentFullHeight - h)) * (trackH - thumbH);
                float[] thumbNdc = HtmlLayoutUtils.GetNdcQuad(trackX + 1f, thumbY, trackW - 2f, thumbH, Matrix4x4.Identity, w, h);
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
            // === FIXED: Always start layout at 0 ===
            // BasePanel scissor reserves title bar space. HTML content starts at top of panel.
            // This removes the second offset that was pushing content down too far.
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
                    foreach (var child in elem.Children)
                    {
                        queue.Enqueue(child);
                    }
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