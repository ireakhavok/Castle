// Folder: SiegeEngine.Systems
// File: MenuSystem.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Managers;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Shaders;
using SiegeEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Systems
{
    public class MenuSystem : GameSystem
    {
        private readonly UISettingsManager _settingsManager;
        private readonly ModManager _modManager;
        private readonly EventBus _eventBus;
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private readonly IRenderContext _renderContext;
        private TextRenderer _textRenderer;
        private UIQuadRenderer _quadRenderer;
        private ShaderProgram _uiShader;
        private readonly string _configPath;
        private HtmlElement _currentMenu;
        private List<HtmlElement> _clickables = new List<HtmlElement>();
        private bool _initialized;
        private float _vw = 0f;
        private float _vh = 0f;
        private bool _layoutDirty = true;
        private CssParser _cssParser;
        private string _currentTargetId;
        private string _baseDir;

        public MenuSystem(UISettingsManager settingsManager, ModManager modManager, EventBus eventBus, IControlContext controlContext, IntPtr window, IRenderContext renderContext, string configPath) : base(null)
        {
            _settingsManager = settingsManager;
            _modManager = modManager;
            _eventBus = eventBus;
            _controlContext = controlContext;
            _window = window;
            _renderContext = renderContext;
            _configPath = configPath;
            _controlContext.SetWindowSizeCallback(_window, OnResize);
        }

        private void OnResize(IntPtr win, int w, int h)
        {
            _renderContext.Viewport(0, 0, (uint)w, (uint)h);
            _vw = w;
            _vh = h;
            if (_currentMenu != null)
            {
                _currentMenu.ComputeLayout(0, 0, _vw, _vh, _vw, _vh, _textRenderer, 16f);
            }
            _layoutDirty = false;
        }

        private void LoadHtml(string htmlPath)
        {
            if (File.Exists(htmlPath))
            {
                string html = File.ReadAllText(htmlPath);
                HtmlParser parser = new HtmlParser();
                _currentMenu = parser.Parse(html);
                List<string> cssBlocks = new List<string>();
                Queue<HtmlElement> q = new Queue<HtmlElement>();
                q.Enqueue(_currentMenu);
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
                _cssParser = new CssParser();
                foreach (var css in cssBlocks)
                {
                    _cssParser.Apply(css);
                }
                ApplyUserAgentDefaults(_cssParser);
                _cssParser.ApplyAll(_currentMenu);
                InheritProperties(_currentMenu, null);
                ProcessSelects(_currentMenu);
                _clickables.Clear();
                CollectClickables(_currentMenu);
                string htmlDir = Path.GetDirectoryName(htmlPath);
                _currentMenu.PrepareResources(htmlDir, _controlContext, _window, _renderContext, _uiShader);
                _layoutDirty = true;
                _baseDir = htmlDir;
            }
        }

        public void SwitchMenu(string menuName)
        {
            string configDir = Path.GetDirectoryName(_configPath);
            string htmlPath = Path.Combine(configDir, $"{menuName}.html");
            LoadHtml(htmlPath);
        }

        private void ApplyUserAgentDefaults(CssParser cssParser)
        {
            string defaultCss = @"
select {
    padding: 2px 10px;
    min-height: 30px;
    border: 1px solid rgba(128, 128, 128, 1);
    border-radius: 5px;
}
input[type=""checkbox""] {
    width: 16px;
    height: 16px;
    margin: 0 5px 0 0;
}
";
            cssParser.Apply(defaultCss);
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

        private void ProcessSelects(HtmlElement elem)
        {
            if (elem is SelectElement select)
            {
                foreach (var child in elem.Children.ToList())
                {
                    if (child.Tag.ToLower() == "option")
                    {
                        string value = child.Attributes.GetValueOrDefault("value", "");
                        string text = string.Join("", child.Children.OfType<TextElement>().Select(t => t.Content));
                        select.Options.Add(text);
                        if (child.Attributes.ContainsKey("selected"))
                            select.Selected = text;
                        elem.Children.Remove(child);
                    }
                }
                if (string.IsNullOrEmpty(select.Selected) && select.Options.Count > 0)
                    select.Selected = select.Options[0];
            }
            foreach (var child in elem.Children.ToList())
                ProcessSelects(child);
        }

        private HtmlElement FindElementById(HtmlElement root, string id)
        {
            if (root.Attributes.GetValueOrDefault("id", "") == id) return root;
            foreach (var child in root.Children)
            {
                var found = FindElementById(child, id);
                if (found != null) return found;
            }
            return null;
        }

        private List<HtmlElement> FindElementsByClass(HtmlElement root, string className)
        {
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

        private void CollectClickables(HtmlElement elem)
        {
            string classes = elem.Attributes.GetValueOrDefault("class", "");
            if (classes.Contains("button") || classes.Contains("toggle") || elem.Tag == "select" || elem.Tag == "label" || elem.Tag == "a" || elem.Attributes.ContainsKey("data-hook") || elem.Attributes.ContainsKey("onclick"))
            {
                _clickables.Add(elem);
            }
            foreach (var child in elem.Children)
                CollectClickables(child);
        }

        public void Initialize()
        {
            _uiShader = new ShaderProgram(_renderContext, UiShader.VertexSource, UiShader.FragmentSource);
            _textRenderer = new TextRenderer(_renderContext, _window);
            _textRenderer.Initialize(_uiShader);
            _quadRenderer = new UIQuadRenderer(_renderContext);
            SwitchMenu("MainMenu");
            _controlContext.GetWindowSize(_window, out int w, out int h);
            OnResize(_window, w, h);
            _layoutDirty = true;
            _initialized = true;
        }

        public override void Update(float deltaTime)
        {
            if (!_initialized) return;
            Vector2 mousePos = new Vector2();
            _controlContext.GetCursorPos(_window, out double x, out double y);
            mousePos = new Vector2((float)x, (float)y);
            bool mouseDown = _controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Press;
            bool mouseUp = _controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Release;
            foreach (var clickable in _clickables)
            {
                bool over = mousePos.X >= clickable.ComputedPosition.X && mousePos.X <= clickable.ComputedPosition.X + clickable.ComputedWidth &&
                            mousePos.Y >= clickable.ComputedPosition.Y && mousePos.Y <= clickable.ComputedPosition.Y + clickable.ComputedHeight;
                clickable.IsHover = over;
                if (over && mouseDown)
                {
                    clickable.IsActive = true;
                }
                if (mouseUp)
                {
                    clickable.IsActive = false;
                }
                if (over && mouseDown)
                {
                    Console.WriteLine($"MenuSystem: Detected click on element at Pos=({clickable.ComputedPosition.X}, {clickable.ComputedPosition.Y}), Size=({clickable.ComputedWidth}, {clickable.ComputedHeight}), Tag={clickable.Tag}, Class={clickable.Attributes.GetValueOrDefault("class", "")}");
                    HandleClickableClick(clickable);
                }
            }
        }

        private void HandleClickableClick(HtmlElement elem)
        {
            Console.WriteLine($"MenuSystem: Handling click for element Tag={elem.Tag}, Class={elem.Attributes.GetValueOrDefault("class", "")}, ID={elem.Attributes.GetValueOrDefault("id", "")}");
            if (elem.Tag == "a")
            {
                string href = elem.Attributes.GetValueOrDefault("href", "");
                if (href.StartsWith("#"))
                {
                    string targetId = href.Substring(1);
                    var target = FindElementById(_currentMenu, targetId);
                    if (target != null)
                    {
                        if (!string.IsNullOrEmpty(_currentTargetId))
                        {
                            var oldTarget = FindElementById(_currentMenu, _currentTargetId);
                            if (oldTarget != null) oldTarget.IsTarget = false;
                        }
                        target.IsTarget = true;
                        _currentTargetId = targetId;
                        _cssParser.ApplyAll(_currentMenu);
                        InheritProperties(_currentMenu, null);
                        _layoutDirty = true;
                        Console.WriteLine($"MenuSystem: Handled anchor click to #{targetId}");
                    }
                }
                else
                {
                    string newPath = href;
                    if (!Path.IsPathRooted(href))
                    {
                        newPath = Path.Combine(_baseDir, href);
                        newPath = Path.GetFullPath(newPath);
                    }
                    if (File.Exists(newPath))
                    {
                        LoadHtml(newPath);
                    }
                }
            }
            else if (elem.Tag == "label")
            {
                string forId = elem.Attributes.GetValueOrDefault("for", "");
                if (!string.IsNullOrEmpty(forId))
                {
                    var input = FindElementById(_currentMenu, forId);
                    if (input != null && input.Tag == "input")
                    {
                        string type = input.Attributes.GetValueOrDefault("type", "");
                        if (type == "radio")
                        {
                            string name = input.Attributes.GetValueOrDefault("name", "");
                            var radios = FindElementsByTag(_currentMenu, "input").Where(i => i.Attributes.GetValueOrDefault("type", "") == "radio" && i.Attributes.GetValueOrDefault("name", "") == name).ToList();
                            foreach (var r in radios) r.Checked = false;
                            input.Checked = true;
                            _cssParser.ApplyAll(_currentMenu);
                            InheritProperties(_currentMenu, null);
                            _layoutDirty = true;
                            Console.WriteLine($"MenuSystem: Handled radio label click for {forId}");
                        }
                        else if (type == "checkbox")
                        {
                            input.Checked = !input.Checked;
                            _cssParser.ApplyAll(_currentMenu);
                            InheritProperties(_currentMenu, null);
                            _layoutDirty = true;
                            Console.WriteLine($"MenuSystem: Handled checkbox label click for {forId}");
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
                    _cssParser.ApplyAll(_currentMenu);
                    InheritProperties(_currentMenu, null);
                    _layoutDirty = true;
                    Console.WriteLine($"MenuSystem: Handled toggle click");
                }
            }
            else if (elem.Attributes.ContainsKey("data-hook"))
            {
                string hook = elem.Attributes["data-hook"];
                Console.WriteLine($"MenuSystem: Processing data-hook: {hook}");
                //if (hook.StartsWith("SiegeEngine.Scenes.") || _modManager.IsWhitelistedHook(hook))
                //{
                if (hook.Contains("Scene"))
                {
                    //_eventBus.Publish(new SwitchSceneEvent { Hook = hook });
                    Console.WriteLine($"MenuSystem: Published SwitchSceneEvent with hook {hook}");
                }
                else
                {
                    //_eventBus.Publish(new GenericEvent { Hook = hook });
                    Console.WriteLine($"MenuSystem: Published GenericEvent with hook {hook}");
                }
                //}
                //else
                //{
                // Console.WriteLine($"MenuSystem: Rejected unsafe hook: {hook}");
                //}
            }
        }

        private List<HtmlElement> FindElementsByTag(HtmlElement root, string tag)
        {
            List<HtmlElement> list = new List<HtmlElement>();
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                if (elem.Tag.ToLower() == tag.ToLower()) list.Add(elem);
                foreach (var child in elem.Children) queue.Enqueue(child);
            }
            return list;
        }

        public void Render()
        {
            if (!_initialized || _currentMenu == null) return;
            _controlContext.GetWindowSize(_window, out int w, out int h);
            _renderContext.Viewport(0, 0, (uint)w, (uint)h);
            if (_layoutDirty || w != _vw || h != _vh)
            {
                OnResize(_window, w, h);
            }
            _currentMenu.Render(_renderContext, _textRenderer, _quadRenderer, _vw, _vh);
        }
    }
}