// Folder: SiegeEngine.UI
// File: UIOverlay.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Shaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class UIOverlay
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly IntPtr _window;
        protected ShaderProgram _uiShader;
        protected TextRenderer _textRenderer;
        protected UIQuadRenderer _quadRenderer;
        protected CssParser _cssParser;
        protected HtmlElement _uiRoot;
        protected List<HtmlElement> _uiClickables = new List<HtmlElement>();
        protected string _currentBaseDir = "";
        private bool _justOpenedSelect = false;
        private bool _prevMouseDown = false;

        public UIOverlay(IRenderContext renderContext, IControlContext controlContext, IntPtr window)
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
            _uiRoot = parser.Parse(html);
            List<string> cssBlocks = new List<string>();
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
            // Initial layout
            _controlContext.GetWindowSize(_window, out int w, out int h);
            RecomputeLayout(w, h);
        }

        private void InitializeElementProperties(HtmlElement root)
        {
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                if (elem is InputElement input)
                {
                    input.Type = elem.Attributes.GetValueOrDefault("type", "text");
                    input.Checked = elem.Attributes.ContainsKey("checked");
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
            }
            foreach (var child in elem.Children)
                InheritProperties(child, elem);
        }

        private void CollectClickables(HtmlElement elem)
        {
            if (elem.GetEffectiveDisplay() == "none") return;
            string classes = elem.Attributes.GetValueOrDefault("class", "");
            if (classes.Contains("button") || classes.Contains("toggle") || elem.Tag == "select" || elem.Tag == "label" || elem.Tag == "a" || elem.Attributes.ContainsKey("data-hook") || elem.Attributes.ContainsKey("onclick") || classes.Contains("select-option") || elem.Tag == "option")
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
            _controlContext.GetWindowSize(_window, out int w, out int h);
            RecomputeLayout(w, h);
            _uiClickables.Clear();
            CollectClickables(_uiRoot);
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

        public HtmlElement FindElementById(string id)
        {
            return FindElementById(_uiRoot, id);
        }

        private List<HtmlElement> FindElementsByClass(HtmlElement root, string className)
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

        private List<HtmlElement> FindElementsByTag(HtmlElement root, string tag)
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

        protected List<HtmlElement> FindElementsByTag(string tag)
        {
            return FindElementsByTag(_uiRoot, tag);
        }

        protected virtual void HandleUIClick(HtmlElement elem)
        {
            if (elem == null) return;
            Console.WriteLine($"UIOverlay: Handling click for element Tag={elem.Tag}, Class={elem.Attributes.GetValueOrDefault("class", "")}, ID={elem.Attributes.GetValueOrDefault("id", "")}");
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
                            RefreshUI();
                            Console.WriteLine($"UIOverlay: Handled radio label click for {forId}");
                        }
                        else if (type == "checkbox")
                        {
                            input.Checked = !input.Checked;
                            RefreshUI();
                            Console.WriteLine($"UIOverlay: Handled checkbox label click for {forId}");
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
                        RefreshUI();
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
                        // select this option
                        foreach (var opt in select.Children.Where(c => c.Tag.ToLower() == "option"))
                        {
                            opt.Attributes.Remove("selected");
                        }
                        elem.Attributes["selected"] = "";
                        select.IsOpen = false;
                    }
                    else
                    {
                        // open the select
                        CloseAllOpenSelects();
                        select.IsOpen = true;
                        _justOpenedSelect = true;
                    }
                    RefreshUI();
                }
            }
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

        public virtual void Update(float deltaTime)
        {
            // UI input handling
            Vector2 mousePos = new Vector2();
            _controlContext.GetCursorPos(_window, out double x, out double y);
            mousePos = new Vector2((float)x, (float)y);
            bool currentMouseDown = _controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Press;
            bool mouseRelease = _prevMouseDown && !currentMouseDown;
            _controlContext.GetWindowSize(_window, out int vw_int, out int vh_int);
            float vw = vw_int;
            float vh = vh_int;
            HtmlElement clickedElem = null;
            bool isClickOnOpenSelect = false;
            SelectElement openSelect = FindElementsByTag("select").FirstOrDefault(s => (s as SelectElement)?.IsOpen ?? false) as SelectElement;
            if (openSelect != null)
            {
                // Check if click is on open select or descendants
                if (openSelect.HandleClick(mousePos, vw, vh))
                {
                    isClickOnOpenSelect = true;
                }
            }
            foreach (var clickable in _uiClickables)
            {
                if (openSelect != null && !clickable.IsDescendantOf(openSelect) && !(clickable == openSelect))
                {
                    continue; // Skip non-descendants when select open
                }
                bool over = clickable.HandleClick(mousePos, vw, vh);
                clickable.IsHover = over;
                if (over && currentMouseDown)
                {
                    clickable.IsActive = true;
                }
                if (over && mouseRelease && clickable.IsActive)
                {
                    clickedElem = clickable;
                }
                if (mouseRelease)
                {
                    clickable.IsActive = false;
                }
            }
            if (clickedElem != null)
            {
                HandleUIClick(clickedElem);
            }
            else if (mouseRelease && openSelect != null && !isClickOnOpenSelect && !_justOpenedSelect)
            {
                // Click outside open select, close it
                CloseAllOpenSelects();
                RefreshUI();
            }
            _justOpenedSelect = false;
            _prevMouseDown = currentMouseDown;
        }

        protected void RenderUI(int w, int h)
        {
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            _uiRoot.Render(_renderContext, _textRenderer, _quadRenderer, w, h, Matrix4x4.Identity);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public virtual void Render()
        {
            _controlContext.GetWindowSize(_window, out int w, out int h);
            if (_uiRoot != null)
            {
                RenderUI(w, h);
            }
        }

        public void RecomputeLayout(int w, int h)
        {
            if (_uiRoot != null)
            {
                _uiRoot.ComputeLayout(0, 0, w, h, w, h, _textRenderer, 16f);
                _uiRoot.UpdateFullTransforms(Matrix4x4.Identity);
            }
        }

        public virtual void Dispose()
        {
            _uiShader.Dispose();
            _textRenderer.Dispose();
        }
    }
}