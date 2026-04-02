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

            if (panel.DockingMode == DockingMode.Dynamic)
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
                if (_desktopStrategy.HasActiveContent())
                    _desktopStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                if (_dynamicStrategy.HasActiveContent())
                    _dynamicStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                if (_ideStrategy.HasActiveContent())
                    _ideStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
            }

            _scrollDelta = 0f;
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

            // Render high RenderOrder panels LAST (menu bar on top of everything)
            var highPriority = _panels.Where(p => (p as BasePanel)?.RenderOrder > 0).OrderByDescending(p => (p as BasePanel)?.RenderOrder);
            foreach (var panel in highPriority)
            {
                if (panel.Visible)
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

            _desktopStrategy.RemovePanel(panel);
            _dynamicStrategy.RemovePanel(panel);
            _ideStrategy.RemovePanel(panel);

            _panels.Remove(panel);
            panel.Dispose();
        }
    }
}