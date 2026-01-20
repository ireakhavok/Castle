// Folder: SiegeEngine.Managers
// File: UIManager.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
namespace SiegeEngine.Core.Managers
{
    public class UIManager
    {
        private readonly UISettingsManager _settingsManager;
        private readonly ModManager _modManager;
        private readonly EventBus _eventBus;
        private readonly IControlContext _controlContext;
        private readonly nint _window;
        private readonly IRenderContext _renderContext;
        private TextRenderer _textRenderer;
        private UIQuadRenderer _quadRenderer;
        private ShaderProgram _uiShader;
        private CssParser _cssParser;
        private float _vw = 0f;
        private float _vh = 0f;
        private bool _initialized;
        private readonly List<IPanel> _panels = new List<IPanel>();
        private readonly string _configPath;
        private bool _menuVisible = true;
        private bool _prevMouseDown;
        private Dictionary<IPanel, bool> _previousVisibility = new Dictionary<IPanel, bool>();
        public bool MenuVisible
        {
            get => _menuVisible;
            set => _menuVisible = value;
        }
        public UIManager(UISettingsManager settingsManager, ModManager modManager, EventBus eventBus, IControlContext controlContext, nint window, IRenderContext renderContext, string configPath)
        {
            _settingsManager = settingsManager;
            _modManager = modManager;
            _eventBus = eventBus;
            _controlContext = controlContext;
            _window = window;
            _renderContext = renderContext;
            _configPath = configPath;
            _controlContext.SetWindowSizeCallback(_window, OnResize);
            _eventBus.Subscribe<OpenPanelEvent>(OnOpenPanel);
            _eventBus.Subscribe<ClosePanelEvent>(OnClosePanel);
        }
        private void OnResize(nint win, int w, int h)
        {
            _renderContext.Viewport(0, 0, (uint)w, (uint)h);
            _vw = w;
            _vh = h;
            foreach (var panel in _panels)
            {
                if (panel is BasePanel bp)
                {
                    bp.OnPanelResize(w, h);
                }
            }
        }
        public void Initialize(string initialMenuHtmlPath)
        {
            _uiShader = new ShaderProgram(_renderContext, UiShader.VertexSource, UiShader.FragmentSource);
            _textRenderer = new TextRenderer(_renderContext, _window);
            _textRenderer.Initialize(_uiShader);
            _quadRenderer = new UIQuadRenderer(_renderContext);
            _cssParser = new CssParser();
            LoadMenu(initialMenuHtmlPath);
            _controlContext.GetWindowSize(_window, out int w, out int h);
            OnResize(_window, w, h);
            _initialized = true;
        }
        private void LoadMenu(string initialMenuHtmlPath)
        {
            string resolvedPath = _modManager.ResolvePath(initialMenuHtmlPath);
            if (resolvedPath == null || !File.Exists(resolvedPath))
            {
                Console.WriteLine($"UIManager: Failed to load menu HTML: {initialMenuHtmlPath}");
                return;
            }
            string html = File.ReadAllText(resolvedPath);
            // Load into menu overlay or main UI root
            // For simplicity, assume menu is loaded as a special panel
            var menuPanel = new MenuPanel(_renderContext, _controlContext, _window, _eventBus, _modManager, initialMenuHtmlPath);
            AddPanel(menuPanel);
        }
        private void OnOpenPanel(OpenPanelEvent e)
        {
            if (e.Mode == OpenMode.Replace)
            {
                foreach (var p in _panels.ToArray())
                {
                    RemovePanel(p);
                }
                _panels.Clear();
            }
            else if (e.Mode == OpenMode.Overlay && e.Panel.IsModal)
            {
                _previousVisibility.Clear();
                foreach (var p in _panels)
                {
                    _previousVisibility[p] = p.Visible;
                    p.Visible = false;
                }
            }
            e.Panel.Init();
            AddPanel(e.Panel);
        }
        private void OnClosePanel(ClosePanelEvent e)
        {
            if (e.Panel.IsModal)
            {
                foreach (var kvp in _previousVisibility)
                {
                    kvp.Key.Visible = kvp.Value;
                }
                _previousVisibility.Clear();
            }
            RemovePanel(e.Panel);
        }
        public void AddPanel(IPanel panel)
        {
            _panels.Add(panel);
        }
        public void RemovePanel(IPanel panel)
        {
            panel.Dispose();
            _panels.Remove(panel);
        }
        public void Update(float deltaTime)
        {
            if (!_initialized) return;
            _controlContext.GetCursorPos(_window, out double mx, out double my);
            Vector2 mousePos = new Vector2((float)mx, (float)my);
            bool currentMouseDown = _controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Press;
            bool mousePressed = !_prevMouseDown && currentMouseDown;
            bool mouseReleased = _prevMouseDown && !currentMouseDown;
            _prevMouseDown = currentMouseDown;
            bool handled = false;
            bool hasModal = _panels.Any(p => p.IsModal && p.Visible);
            if (hasModal)
            {
                for (int i = _panels.Count - 1; i >= 0; i--)
                {
                    var panel = _panels[i];
                    if (panel.IsModal && panel.Visible)
                    {
                        Vector2 rel = mousePos - panel.Position;
                        bool over = rel.X >= 0 && rel.X <= panel.Size.X && rel.Y >= 0 && rel.Y <= panel.Size.Y;
                        if (over)
                        {
                            panel.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased);
                            handled = true;
                        }
                        break;
                    }
                }
                if (!handled && mouseReleased)
                {
                    var topModal = _panels.LastOrDefault(p => p.IsModal && p.Visible);
                    if (topModal != null)
                    {
                        _eventBus.Publish(new ClosePanelEvent(topModal));
                    }
                }
            }
            else
            {
                for (int i = _panels.Count - 1; i >= 0; i--)
                {
                    var panel = _panels[i];
                    if (!panel.Visible) continue;
                    Vector2 rel = mousePos - panel.Position;
                    bool over = rel.X >= 0 && rel.X <= panel.Size.X && rel.Y >= 0 && rel.Y <= panel.Size.Y;
                    if (over)
                    {
                        panel.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased);
                        handled = true;
                        break;
                    }
                }
            }
        }
        public void Render()
        {
            if (!_initialized) return;
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            foreach (var panel in _panels.Where(p => p.Visible))
            {
                panel.Render();
            }
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }
        public void Dispose()
        {
            foreach (var panel in _panels)
            {
                panel.Dispose();
            }
        }
    }
}