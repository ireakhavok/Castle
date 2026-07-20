// Folder: SiegeEngine.Core.UI
// File: UIOverlay.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Core.UI.JSParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace SiegeEngine.Core.UI
{
    public readonly struct ContextMenuItem
    {
        public string Label { get; }
        public string DataHook { get; }
        public ContextMenuItem(string label, string dataHook)
        {
            Label = label ?? "";
            DataHook = dataHook ?? "";
        }
    }

    public class UIOverlay
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly nint _window;
        protected readonly EventBus _eventBus;
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
        public float ReservedHeaderHeight { get; set; } = 0f;
        public float ScrollOffsetY { get; set; } = 0f;
        public float ContentFullHeight { get; private set; } = 0f;
        private bool _needsVerticalScrollbar = false;
        public bool DidHandleClick { get; set; }
        private UIInteractionLayer _interactionLayer;
        public UIQuadRenderer QuadRenderer => _quadRenderer;
        private HtmlElement _currentContextMenu = null;
        public HtmlElement CurrentContextMenu => _currentContextMenu;
        public UIOverlay(IRenderContext renderContext, IControlContext controlContext, nint window)
            : this(renderContext, controlContext, window, null)
        {
        }
        public UIOverlay(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
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
            bool isOptionInsideClosedSelect = false;
            if (elem.Tag.ToLower() == "option")
            {
                if (elem.Parent is SelectElement parentSelect && !parentSelect.IsOpen)
                    isOptionInsideClosedSelect = true;
            }
            string classes = elem.Attributes.GetValueOrDefault("class", "");
            string tagLower = elem.Tag.ToLower();
            bool isTreeNode = tagLower == "li" && classes.Contains("node");
            bool hasId = elem.Attributes.ContainsKey("id");
            if (!isOptionInsideClosedSelect &&
                (isTreeNode ||
                 classes.Contains("button") || classes.Contains("toggle") || tagLower == "select" || tagLower == "label" || tagLower == "a" ||
                 elem.Attributes.ContainsKey("data-hook") || elem.Attributes.ContainsKey("onclick") || classes.Contains("select-option") ||
                 tagLower == "option" || elem.Attributes.ContainsKey("onchange") || elem.Attributes.ContainsKey("onmouseenter") ||
                 elem.Attributes.ContainsKey("onmouseleave") || elem.Attributes.ContainsKey("onmouseover") || elem.Attributes.ContainsKey("onmouseout") ||
                 elem.Attributes.ContainsKey("onmousedown") || elem.Attributes.ContainsKey("onmouseup") || elem.Attributes.ContainsKey("onfocus") ||
                 elem.Attributes.ContainsKey("onblur") || tagLower == "input" ||
                 (tagLower == "li" && (classes.Contains("nav-dropdown") || elem.Children.Any(c => c.Tag.ToLower() == "ul"))) ||
                 hasId || elem.Attributes.ContainsKey("data-context") || classes.Contains("context-menu") || classes.Contains("context-item")))
            {
                _uiClickables.Add(elem);
            }
            foreach (var child in elem.Children)
                CollectClickables(child);
        }
        protected virtual void HandleDataHook(string hook)
        {
            DataHookProcessor.Process(hook, _renderContext, _controlContext, _window, _eventBus, this);
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
            _cssParser.ApplyInlineStyles(_uiRoot);
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
                if (classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className))
                    list.Add(elem);
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
                if (tag == "*" || elem.Tag.ToLower() == tag.ToLower())
                    list.Add(elem);
                foreach (var child in elem.Children) queue.Enqueue(child);
            }
            return list;
        }
        public virtual bool HandleUIClick(HtmlElement elem)
        {
            if (elem == null) return false;
            bool handled = false;
            bool valueChanged = false;
            if (!string.IsNullOrEmpty(elem.OnClickJS))
            {
                _jsContext.RunWithThis(elem.OnClickJS, new JSElement(elem, this));
                handled = true;
            }
            InvokeListeners(elem, "click");
            if (_document != null && _document.InvokeDocumentListeners("click", elem))
                handled = true;
            if (elem.Tag == "a")
            {
                string href = elem.Attributes.GetValueOrDefault("href", "");
                if (string.IsNullOrEmpty(href)) return handled;
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
                        handled = true;
                    }
                }
                else
                {
                    HandleLink(href);
                    handled = true;
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
                            handled = true;
                        }
                        else if (type == "checkbox")
                        {
                            input.Checked = !input.Checked;
                            valueChanged = true;
                            RefreshUI();
                            handled = true;
                        }
                        else if (type == "text" || type == "number")
                        {
                            if (!input.IsFocused)
                            {
                                if (!string.IsNullOrEmpty(input.OnFocusJS))
                                    _jsContext.RunWithThis(input.OnFocusJS, new JSElement(input, this));
                                InvokeListeners(input, "focus");
                                input.IsFocused = true;
                                _interactionLayer._currentFocused = input;
                                handled = true;
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
                    handled = true;
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
                        handled = true;
                    }
                    else if (input.Type == "text" || input.Type == "number")
                    {
                        if (!input.IsFocused)
                        {
                            if (!string.IsNullOrEmpty(input.OnFocusJS))
                                _jsContext.RunWithThis(input.OnFocusJS, new JSElement(input, this));
                            InvokeListeners(input, "focus");
                            input.IsFocused = true;
                            _interactionLayer._currentFocused = input;
                            handled = true;
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
                    handled = true;
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
                            opt.Attributes.Remove("selected");
                        elem.Attributes["selected"] = "";
                        select.IsOpen = false;
                        valueChanged = true;
                        if (select.Attributes.ContainsKey("data-hook"))
                        {
                            string hook = select.Attributes["data-hook"];
                            Console.WriteLine($"UIOverlay: Processing data-hook from select after option choice: {hook}");
                            HandleDataHook(hook);
                            handled = true;
                        }
                    }
                    else
                    {
                        CloseAllOpenSelects();
                        select.IsOpen = true;
                        _interactionLayer._justOpenedSelect = true;
                        handled = true;
                    }
                    RefreshUI();
                }
            }
            if (elem.Attributes.ContainsKey("data-hook"))
            {
                if (elem.Tag != "select")
                {
                    string hook = elem.Attributes["data-hook"];
                    Console.WriteLine($"UIOverlay: Processing data-hook: {hook}");
                    HandleDataHook(hook);
                    handled = true;
                }
                CloseAllOpenNavDropdowns();
                CloseContextMenu();
            }
            if (valueChanged)
            {
                TriggerChange(elem);
            }
            return handled;
        }
        public void ShowContextMenu(Vector2 mousePos, IReadOnlyList<ContextMenuItem> items)
        {
            if (_currentContextMenu != null)
            {
                _uiRoot.Children.Remove(_currentContextMenu);
                _currentContextMenu = null;
            }
            if (items == null || items.Count == 0)
            {
                RefreshUI();
                return;
            }
            var menu = new HtmlElement();
            menu.Tag = "div";
            menu.Parent = _uiRoot;
            menu.Attributes["class"] = "context-menu";
            menu.Style.Position = "absolute";
            menu.Style.LeftStr = mousePos.X.ToString("0.##") + "px";
            menu.Style.TopStr = mousePos.Y.ToString("0.##") + "px";
            menu.Style.WidthStr = "220px";
            menu.Style.Display = "block";
            menu.Style.BackgroundColor = new Vector4(0.176f, 0.176f, 0.176f, 0.98f);
            menu.Style.BorderColor = new Vector4(0.333f, 0.333f, 0.333f, 1f);
            menu.Style.BorderWidthStr = "1px";
            menu.Style.BorderStyle = "solid";
            menu.Style.BorderRadiusStr = "4px";
            menu.Style.PaddingStr = "4px 0";
            menu.Style.Color = "#ffffff";
            menu.Style.TextColor = new Vector4(1f, 1f, 1f, 1f);
            menu.Attributes["style"] = $"position:absolute;left:{mousePos.X.ToString("0.##")}px;top:{mousePos.Y.ToString("0.##")}px;width:220px;background-color:rgba(45,45,45,0.98);border:1px solid #555555;border-radius:4px;padding:4px 0;display:block;color:#ffffff;";
            foreach (var itemDef in items)
            {
                var item = new HtmlElement();
                item.Tag = "div";
                item.Parent = menu;
                item.Attributes["class"] = "context-item";
                item.Attributes["data-hook"] = itemDef.DataHook;
                item.Style.PaddingStr = "6px 20px";
                item.Style.Color = "#ffffff";
                item.Style.TextColor = new Vector4(1f, 1f, 1f, 1f);
                item.Style.Display = "block";
                item.Attributes["style"] = "padding:6px 20px;color:#ffffff;display:block;cursor:pointer;";
                var text = new TextElement { Content = itemDef.Label, Tag = "span" };
                text.Parent = item;
                text.Style.Color = "#ffffff";
                text.Style.TextColor = new Vector4(1f, 1f, 1f, 1f);
                item.Children.Add(text);
                menu.Children.Add(item);
            }
            _uiRoot.Children.Add(menu);
            _currentContextMenu = menu;
            RefreshUI();
            Console.WriteLine($"[UIOverlay] Context menu shown with {items.Count} item(s) at mouse {mousePos}");
        }
        public void CloseContextMenu()
        {
            if (_currentContextMenu != null)
            {
                _uiRoot.Children.Remove(_currentContextMenu);
                _currentContextMenu = null;
                RefreshUI();
                Console.WriteLine("[UIOverlay] Context menu closed");
            }
        }
        protected internal virtual bool OnContextMenuRequested(HtmlElement sourceElement, Vector2 mousePos)
        {
            return false;
        }
        private void CloseAllOpenNavDropdowns()
        {
            var navLis = FindElementsByTag("li")
                .Where(e => e is NavLiElement nav && nav.IsNavDropdownParent())
                .Cast<NavLiElement>()
                .ToList();
            foreach (var nav in navLis)
                nav.CloseDropdown();
        }
        public void CloseAllOpenSelects()
        {
            var selects = FindElementsByTag("select");
            foreach (var s in selects)
            {
                if (s is SelectElement sel)
                    sel.IsOpen = false;
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
                sel.RenderDropdown(_renderContext, _textRenderer, _quadRenderer, w, h);
            if (_currentContextMenu != null)
            {
                _currentContextMenu.ComputeLayout(0, 0, w, h, w, h, _textRenderer, 14f);
                _currentContextMenu.UpdateFullTransforms(Matrix4x4.Identity);
                _currentContextMenu.Render(_renderContext, _textRenderer, _quadRenderer, w, h, Matrix4x4.Identity);
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
        public virtual void RenderBackgrounds(float w, float h)
        {
            if (_uiRoot != null)
            {
                Matrix4x4 scrollMatrix = Matrix4x4.CreateTranslation(0, -ScrollOffsetY, 0);
                _uiRoot.RenderBackgroundOnly(_renderContext, _textRenderer, _quadRenderer, w, h, scrollMatrix);
            }
        }
        public void RecomputeLayout(float w, float h)
        {
            if (_uiRoot == null) return;
            float contentStartY = ReservedHeaderHeight;
            float usableHeight = h - ReservedHeaderHeight;
            if (ReservedHeaderHeight > 0)
            {
                _uiRoot.ComputeLayout(0, contentStartY, w, usableHeight, w, h, _textRenderer, 16f, w, usableHeight);
            }
            else
            {
                _uiRoot.ComputeLayout(0, 0, w, h, w, h, _textRenderer, 16f);
            }
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
                        queue.Enqueue(child);
                }
            }
            float usableHeight = PanelHeight - ReservedHeaderHeight;
            float effectiveContentHeight = Math.Max(0f, ContentFullHeight - ReservedHeaderHeight);
            _needsVerticalScrollbar = effectiveContentHeight > usableHeight + 0.1f;
            if (_needsVerticalScrollbar)
            {
                ScrollOffsetY = Math.Clamp(ScrollOffsetY, 0f, effectiveContentHeight - usableHeight);
            }
            else
            {
                ScrollOffsetY = 0f;
            }
        }
        public void Scroll(float deltaY)
        {
            if (!_needsVerticalScrollbar)
            {
                ScrollOffsetY = 0f;
                return;
            }
            ScrollOffsetY -= deltaY * 30f;
            float usableHeight = PanelHeight - ReservedHeaderHeight;
            float effectiveContentHeight = Math.Max(0f, ContentFullHeight - ReservedHeaderHeight);
            ScrollOffsetY = Math.Clamp(ScrollOffsetY, 0f, effectiveContentHeight - usableHeight);
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
            var jsElem = new JSElement(elem, this);
            while (current != null)
            {
                if (!string.IsNullOrEmpty(current.OnChangeJS))
                    _jsContext.RunWithThis(current.OnChangeJS, jsElem);
                InvokeListeners(current, "change", jsElem);
                current = current.Parent;
            }
        }
        public bool InvokeListeners(HtmlElement elem, string eventName, JSElement jsElem = null)
        {
            Console.WriteLine($"[UIOverlay] InvokeListeners ENTER - eventName={eventName}, hasListeners={elem.EventListeners.ContainsKey(eventName)}");
            if (elem.EventListeners.ContainsKey(eventName))
            {
                if (jsElem == null) jsElem = new JSElement(elem, this);
                foreach (var cb in elem.EventListeners[eventName])
                {
                    Console.WriteLine($"[UIOverlay] InvokeListeners - calling cb type={(cb?.GetType().Name ?? "null")}");
                    _jsContext.Evaluator.CallFunction(cb, new List<object> { jsElem });
                }
                return true;
            }
            return false;
        }
        public TextRenderer TextRenderer => _textRenderer;
    }
}