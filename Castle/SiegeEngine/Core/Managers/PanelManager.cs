// Folder: SiegeEngine.Core.Managers
// File: PanelManager.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
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

        // === Desktop strategy (original behavior - untouched) ===
        private IDockingStrategy _desktopStrategy;

        // === NEW: Isolated Dynamic strategy (no interaction with Desktop) ===
        private DynamicDockingStrategy _dynamicStrategy;

        private DockingMode _sceneDefaultMode = DockingMode.Desktop;

        public PanelManager(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _captureManager = new CaptureManager(controlContext);

            _desktopStrategy = new DesktopDockingStrategy(renderContext, controlContext, eventBus);
            _dynamicStrategy = new DynamicDockingStrategy(renderContext, controlContext, eventBus);

            _eventBus.Subscribe<OpenPanelEvent>(OnOpenPanel);
            _eventBus.Subscribe<ClosePanelEvent>(OnClosePanel);
            _controlContext.SetScrollCallback(_window, (nint w, double xoffset, double yoffset) =>
            {
                _scrollDelta += (float)yoffset;
            });
        }

        public void SetSceneDefaultDockingMode(DockingMode mode)
        {
            _sceneDefaultMode = mode;
            // No strategy recreation - we now support both modes simultaneously
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

            // === ROUTING: Completely isolated by mode (no cross-talk) ===
            if (panel.DockingMode == DockingMode.Dynamic)
            {
                _dynamicStrategy.AddPanel(panel);
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
                // === Call BOTH strategies independently (zero interaction) ===
                _desktopStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
                _dynamicStrategy.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _scrollDelta, _eventBus, winW, winH);
            }

            _scrollDelta = 0f;
        }

        public void Render()
        {
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            _renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            _renderContext.Viewport(0, 0, (uint)winW, (uint)winH);

            // === Render BOTH strategies independently ===
            _desktopStrategy.Render(_renderContext, winW, winH);
            _dynamicStrategy.Render(_renderContext, winW, winH);
        }

        public void RemovePanel(IPanel panel)
        {
            if (_captureManager.CurrentOwner == panel)
                _captureManager.ReleaseCapture();

            panel.Detach();

            // === Safe remove from both (one will ignore) ===
            _desktopStrategy.RemovePanel(panel);
            _dynamicStrategy.RemovePanel(panel);

            _panels.Remove(panel);
            panel.Dispose();
        }
    }
}