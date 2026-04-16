// Folder: SiegeEngine/Core/Managers
// File: PanelManager.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
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
        }

        private void AutoCenterModal(IPanel panel)
        {
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            float x = (winW - panel.Size.X) * 0.5f;
            float y = (winH - panel.Size.Y) * 0.5f;
            panel.Position = new Vector2(Math.Max(40f, x), Math.Max(40f, y));
            panel.OnPanelResize(panel.Size.X, panel.Size.Y);
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
                if (topmost != null)
                {
                    topmost.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta);

                    // === QUICK CONSUME FIX ===
                    // If BasePanel just handled a close (or any other action) on mouseReleased,
                    // we null the mouseReleased flag so no lower overlapping panel can ever see it.
                    if (BasePanel.MouseReleasedConsumedThisFrame && mouseReleased)
                    {
                        mouseReleased = false;
                        BasePanel.MouseReleasedConsumedThisFrame = false;
                    }
                }

                // Non-modal panels go through strategies — but mouseReleased is now safely nulled if a close happened
                if (_desktopStrategy.HasActiveContent())
                    _desktopStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                if (_dynamicStrategy.HasActiveContent())
                    _dynamicStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                if (_ideStrategy.HasActiveContent())
                    _ideStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
            }

            _router.ClearForcedOverdraw();
            _scrollDelta = 0f;
            BasePanel.MouseReleasedConsumedThisFrame = false;   // reset for next frame
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