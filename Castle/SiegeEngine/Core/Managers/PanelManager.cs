// Folder: SiegeEngine/Core/Managers
// File: PanelManager.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace SiegeEngine.Core.Managers
{
    public class PanelManager
    {
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly nint _window;
        private readonly EventBus _eventBus;
        private bool _prevMouseDown;
        private readonly List<IPanel> _panels = new List<IPanel>();
        private readonly List<IPanel> _modalPanels = new List<IPanel>();
        private float _scrollDelta = 0f;
        private readonly CaptureManager _captureManager;
        private IDockingStrategy _desktopStrategy;
        private DynamicDockingStrategy _dynamicStrategy;
        private IDEDockingStrategy _ideStrategy;
        private DockingMode _sceneDefaultMode = DockingMode.Desktop;
        private bool _lastGlobalTabPressed = false;
        private readonly PanelInputRouter _router;
        public static PanelManager Current { get; private set; }
        public IDEDockingStrategy IDEStrategy => _ideStrategy;
        public PanelManager(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _captureManager = new CaptureManager(controlContext);
            _router = new PanelInputRouter();
            _desktopStrategy = new DesktopDockingStrategy(renderContext, controlContext, eventBus);
            _dynamicStrategy = new DynamicDockingStrategy(renderContext, controlContext, eventBus);
            _ideStrategy = new IDEDockingStrategy(renderContext, controlContext, window, eventBus);
            _eventBus.Subscribe<OpenPanelEvent>(OnOpenPanel);
            _eventBus.Subscribe<ClosePanelEvent>(OnClosePanel);
            _eventBus.Subscribe<OpenGameHudEvent>(OnOpenGameHud);
            _controlContext.SetScrollCallback(_window, (nint w, double xoffset, double yoffset) =>
            {
                _scrollDelta += (float)yoffset;
            });
            Current = this;
        }
        public void SetSceneDefaultDockingMode(DockingMode mode)
        {
            _sceneDefaultMode = mode;
        }
        private void OnOpenPanel(OpenPanelEvent e)
        {
            AddPanel(e.Panel);
        }
        private void OnClosePanel(ClosePanelEvent e)
        {
            RemovePanel(e.Panel);
        }
        public void AddPanel(IPanel panel)
        {
            if (panel == null) return;
            if (_panels.Contains(panel)) return;
            _panels.Add(panel);
            _router.AddPanel(panel);
            panel.Init();
            if (panel is BasePanel bp && bp.IsModal)
            {
                _modalPanels.Add(panel);
                AutoCenterModal(panel);
            }
            else if (panel.DockingMode == DockingMode.Dynamic)
            {
                _dynamicStrategy.AddPanel(panel);
            }
            else if (panel.DockingMode == DockingMode.IDE)
            {
                _ideStrategy.AddPanel(panel);
            }
            else
            {
                if (panel.DockingMode == DockingMode.Desktop)
                {
                    panel.DockingMode = _sceneDefaultMode;
                }
                _desktopStrategy.AddPanel(panel);
            }
            if (!panel.IsModal && panel.DockState == DockState.Floating)
            {
                AutoCenterFloating(panel);
            }
        }
        private void AutoCenterModal(IPanel panel)
        {
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            float x = (winW - panel.Size.X) * 0.5f;
            float y = (winH - panel.Size.Y) * 0.5f;
            panel.Position = new Vector2(Math.Max(40f, x), Math.Max(40f, y));
            panel.OnPanelResize(panel.Size.X, panel.Size.Y);
        }
        private void AutoCenterFloating(IPanel panel)
        {
            if (panel.Size.Y <= 30f) return;
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            const float menuBar = 28f;
            float x = (winW - panel.Size.X) * 0.5f;
            float y = (winH - panel.Size.Y) * 0.5f;
            y = Math.Max(menuBar + 20f, y);
            x = Math.Max(20f, Math.Min(x, winW - panel.Size.X - 20f));
            panel.Position = new Vector2(x, y);
            panel.OnPanelResize(panel.Size.X, panel.Size.Y);
        }
        public void BringToFront(IPanel panel)
        {
            if (panel == null || !_panels.Contains(panel)) return;
            _panels.Remove(panel);
            _panels.Add(panel);
            _router.RemovePanel(panel);
            _router.AddPanel(panel);
            if (panel is BasePanel bp && bp.DockState == DockState.Floating)
            {
                switch (bp.DockingMode)
                {
                    case DockingMode.Desktop:
                        if (_desktopStrategy is DesktopDockingStrategy desktop)
                            desktop.BringFloatingPanelToFront(bp);
                        break;
                    case DockingMode.Dynamic:
                        if (_dynamicStrategy is DynamicDockingStrategy dynamic)
                            dynamic.BringFloatingPanelToFront(bp);
                        break;
                    case DockingMode.IDE:
                        if (_ideStrategy is IDEDockingStrategy ide)
                            ide.BringFloatingPanelToFront(bp);
                        break;
                }
            }
        }
        public void Update(float deltaTime)
        {
            _controlContext.GetCursorPos(_window, out double mx, out double my);
            Vector2 mousePos = new Vector2((float)mx, (float)my);
            bool currentMouseDown = _controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Press;
            bool mousePressed = !_prevMouseDown && currentMouseDown;
            bool mouseReleased = _prevMouseDown && !currentMouseDown;
            _prevMouseDown = currentMouseDown;
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            bool tabPressed = _controlContext.GetKey(_window, Key.Tab) == InputAction.Press;
            if (tabPressed && !_lastGlobalTabPressed)
            {
                _lastGlobalTabPressed = true;
                IPanel target = _captureManager.CurrentOwner ?? GetTopmostPanelAt(mousePos);
                target?.ToggleCameraMode();
            }
            else if (!tabPressed)
            {
                _lastGlobalTabPressed = false;
            }
            _captureManager.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta);
            if (!_captureManager.IsCapturing)
            {
                IPanel topmost = GetTopmostPanelAt(mousePos);
                bool handledByIDEStrategy = false;
                if (_ideStrategy != null && topmost != null)
                {
                    if (_ideStrategy.ContainsFloatingPanel(topmost))
                    {
                        handledByIDEStrategy = true;
                    }
                }
                if (!handledByIDEStrategy && topmost != null)
                {
                    topmost.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta);
                    if (BasePanel.MouseReleasedConsumedThisFrame && mouseReleased)
                    {
                        mouseReleased = false;
                        BasePanel.MouseReleasedConsumedThisFrame = false;
                    }
                }
                foreach (var panel in _panels)
                {
                    if (panel is BasePanel bp && bp.WantsContinuousUpdate)
                    {
                        bool isTopmost = (topmost == panel);
                        if (isTopmost) continue;
                        bool passMouseDown = false;
                        bool passMousePressed = false;
                        bool passMouseReleased = false;
                        panel.Update(deltaTime, mousePos, passMouseDown, passMousePressed, passMouseReleased, _scrollDelta);
                    }
                }
                if (_desktopStrategy.HasActiveContent())
                    _desktopStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                if (_dynamicStrategy.HasActiveContent())
                    _dynamicStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                if (_ideStrategy.HasActiveContent())
                    _ideStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
            }
            _router.ClearForcedOverdraw();
            _scrollDelta = 0f;
            BasePanel.MouseReleasedConsumedThisFrame = false;
        }
        public IPanel GetTopmostPanelAt(Vector2 mousePos)
        {
            return _router.GetTopmostPanelAt(mousePos);
        }
        public void ForceDrawOverThisFrame(IPanel panel)
        {
            _router.ForceDrawOverThisFrame(panel);
        }
        public IEnumerable<IPanel> GetAllPanels()
        {
            return _panels;
        }
        public void Render()
        {
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            _renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            _renderContext.Viewport(0, 0, (uint)winW, (uint)winH);
            if (_desktopStrategy.HasActiveContent())
                _desktopStrategy.Render(_renderContext, winW, winH);
            if (_dynamicStrategy.HasActiveContent())
                _dynamicStrategy.Render(_renderContext, winW, winH);
            if (_ideStrategy.HasActiveContent())
                _ideStrategy.Render(_renderContext, winW, winH);
            foreach (var panel in _modalPanels)
            {
                if (panel.Visible)
                {
                    _renderContext.Disable(_renderContext.Enums.DepthTest);
                    panel.Render();
                    _renderContext.Enable(_renderContext.Enums.DepthTest);
                }
            }
            var highPriority = _panels.Where(p => (p as BasePanel)?.RenderOrder > 0).OrderByDescending(p => (p as BasePanel)?.RenderOrder);
            foreach (var panel in highPriority)
            {
                if (panel.Visible && !_modalPanels.Contains(panel))
                {
                    _renderContext.Disable(_renderContext.Enums.DepthTest);
                    panel.Render();
                    _renderContext.Enable(_renderContext.Enums.DepthTest);
                }
            }
        }
        private readonly System.Collections.Generic.Dictionary<string, SiegeEngine.Core.UI.GameHudPanel> _gameHuds = new System.Collections.Generic.Dictionary<string, SiegeEngine.Core.UI.GameHudPanel>();

        private void OnOpenGameHud(OpenGameHudEvent e)
        {
            if (e == null || string.IsNullOrEmpty(e.HtmlRelativePath)) return;
            string key = e.HtmlRelativePath;
            if (!e.Open)
            {
                if (_gameHuds.TryGetValue(key, out var existing))
                {
                    RemovePanel(existing);
                    _gameHuds.Remove(key);
                }
                return;
            }
            if (_gameHuds.ContainsKey(key)) return;
            var hud = new SiegeEngine.Core.UI.GameHudPanel(_renderContext, _controlContext, _window, _eventBus, e);
            _gameHuds[key] = hud;
            AddPanel(hud);
        }

        public void RemovePanel(IPanel panel)
        {
            if (_captureManager.CurrentOwner == panel)
                _captureManager.ReleaseCapture();
            _router.RemovePanel(panel);
            panel.Detach();
            _modalPanels.Remove(panel);
            _desktopStrategy.RemovePanel(panel);
            _dynamicStrategy.RemovePanel(panel);
            _ideStrategy.RemovePanel(panel);
            _panels.Remove(panel);
            panel.Dispose();
        }
        public void CapturePanel(IPanel panel)
        {
            _captureManager.RequestCapture(panel);
        }
        public void ReleasePanelCapture()
        {
            _captureManager.ReleaseCapture();
        }
    }
}