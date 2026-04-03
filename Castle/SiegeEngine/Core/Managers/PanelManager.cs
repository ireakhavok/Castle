// Folder: SiegeEngine.Core.Managers
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

        public static PanelManager Current { get; private set; }

        public PanelManager(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _captureManager = new CaptureManager(controlContext);

            _desktopStrategy = new DesktopDockingStrategy(renderContext, controlContext, eventBus);
            _dynamicStrategy = new DynamicDockingStrategy(renderContext, controlContext, eventBus);
            _ideStrategy = new IDEDockingStrategy(renderContext, controlContext, eventBus);

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

            _captureManager.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta);

            if (!_captureManager.IsCapturing)
            {
                bool modalHandled = false;

                for (int i = _modalPanels.Count - 1; i >= 0; i--)
                {
                    var panel = _modalPanels[i];
                    if (panel.Visible)
                    {
                        panel.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta);
                        modalHandled = true;
                        break;
                    }
                }

                if (modalHandled && mouseReleased)
                {
                    bool clickedOnModal = false;
                    for (int i = _modalPanels.Count - 1; i >= 0; i--)
                    {
                        var m = _modalPanels[i];
                        if (m.Visible)
                        {
                            bool over = mousePos.X >= m.Position.X && mousePos.X <= m.Position.X + m.Size.X &&
                                        mousePos.Y >= m.Position.Y && mousePos.Y <= m.Position.Y + m.Size.Y;
                            if (over)
                            {
                                clickedOnModal = true;
                                break;
                            }
                        }
                    }
                    if (!clickedOnModal && _modalPanels.Count > 0)
                    {
                        _eventBus.Publish(new ClosePanelEvent(_modalPanels.Last()));
                    }
                }

                if (!modalHandled)
                {
                    if (_desktopStrategy.HasActiveContent())
                        _desktopStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                    if (_dynamicStrategy.HasActiveContent())
                        _dynamicStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                    if (_ideStrategy.HasActiveContent())
                        _ideStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                }
            }

            // === SINGLE TOPMOST OWNER – this is the only place content events are processed ===
            IPanel topOwner = null;
            if (!_captureManager.IsCapturing)
            {
                topOwner = GetTopmostPanelAt(mousePos);
            }

            if (topOwner != null)
            {
                topOwner.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta);
            }

            _scrollDelta = 0f;
        }

        // NEW: Centralized topmost hit test – used by BasePanel to swallow clicks on lower panels
        public IPanel GetTopmostPanelAt(Vector2 mousePos)
        {
            // Modals always win
            for (int i = _modalPanels.Count - 1; i >= 0; i--)
            {
                var m = _modalPanels[i];
                if (m.Visible)
                {
                    bool over = mousePos.X >= m.Position.X && mousePos.X <= m.Position.X + m.Size.X &&
                                mousePos.Y >= m.Position.Y && mousePos.Y <= m.Position.Y + m.Size.Y;
                    if (over) return m;
                }
            }

            IPanel p = _ideStrategy.GetTopmostPanelAt(mousePos);
            if (p != null) return p;

            p = _dynamicStrategy.GetTopmostPanelAt(mousePos);
            if (p != null) return p;

            return _desktopStrategy.GetTopmostPanelAt(mousePos);
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