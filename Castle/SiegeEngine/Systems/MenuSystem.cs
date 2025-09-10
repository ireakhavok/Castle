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
        private List<ButtonElement> _buttons = new List<ButtonElement>();
        private bool _initialized;

        public MenuSystem(UISettingsManager settingsManager, ModManager modManager, EventBus eventBus, IControlContext controlContext, IntPtr window, IRenderContext renderContext, string configPath) : base(null)
        {
            _settingsManager = settingsManager;
            _modManager = modManager;
            _eventBus = eventBus;
            _controlContext = controlContext;
            _window = window;
            _renderContext = renderContext;
            _configPath = configPath;
        }

        public void SwitchMenu(string menuName)
        {
            string configDir = Path.GetDirectoryName(_configPath);
            string htmlPath = Path.Combine(configDir, $"{menuName}.html");
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
                CssParser cssParser = new CssParser();
                foreach (var css in cssBlocks)
                {
                    cssParser.Apply(css, _currentMenu);
                }
                // Manually set displays since no pseudo-selector support
                var settings = FindElementById(_currentMenu, "settings");
                if (settings != null) settings.Style.Display = "none";
                var contents = FindElementsByClass(_currentMenu, "content");
                if (contents.Count > 0) contents[0].Style.Display = "block";
                // Collect buttons
                _buttons.Clear();
                CollectButtons(_currentMenu);
            }
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

        private void CollectButtons(HtmlElement elem)
        {
            if (elem is ButtonElement btn)
                _buttons.Add(btn);
            foreach (var child in elem.Children)
                CollectButtons(child);
        }

        public void Initialize()
        {
            _textRenderer = new TextRenderer(_renderContext, _window);
            _textRenderer.Initialize(new ShaderProgram(_renderContext, UiShader.VertexSource, UiShader.FragmentSource));
            _quadRenderer = new UIQuadRenderer(_renderContext);
            SwitchMenu("MainMenu");
            _initialized = true;
        }

        public override void Update(float deltaTime)
        {
            if (!_initialized) return;
            // Handle inputs
            Vector2 mousePos = new Vector2();
            _controlContext.GetCursorPos(_window, out double x, out double y);
            mousePos = new Vector2((float)x, (float)y);
            if (_controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Press)
            {
                foreach (var btn in _buttons)
                {
                    if (btn.HandleClick(mousePos))
                        break;
                }
            }
        }

        public void Render()
        {
            if (!_initialized || _currentMenu == null) return;
            float vw = _settingsManager.WindowWidth;
            float vh = _settingsManager.WindowHeight;
            _currentMenu.ComputeLayout(0, 0, vw, vh, vw, vh, _textRenderer);
            _currentMenu.Render(_renderContext, _textRenderer, _quadRenderer, vw, vh);
        }
    }
}