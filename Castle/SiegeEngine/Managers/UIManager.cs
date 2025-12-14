// Folder: SiegeEngine.Managers
// File: UIManager.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Shaders;
using SiegeEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace SiegeEngine.Managers
{
    public class UIManager
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
        private CssParser _cssParser;
        private float _vw = 0f;
        private float _vh = 0f;
        private bool _initialized;
        private readonly List<IPanel> _panels = new List<IPanel>();
        private readonly string _configPath;
        private bool _menuVisible = true;
        private bool _prevMouseDown;

        public bool MenuVisible
        {
            get => _menuVisible;
            set => _menuVisible = value;
        }

        public UIManager(UISettingsManager settingsManager, ModManager modManager, EventBus eventBus, IControlContext controlContext, IntPtr window, IRenderContext renderContext, string configPath)
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
        }

        private void OnResize(IntPtr win, int w, int h)
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
            e.Panel.Init();
            AddPanel(e.Panel);
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
            foreach (var panel in _panels.ToArray())
            {
                panel.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased);
            }
        }

        public void Render()
        {
            if (!_initialized) return;
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            foreach (var panel in _panels)
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