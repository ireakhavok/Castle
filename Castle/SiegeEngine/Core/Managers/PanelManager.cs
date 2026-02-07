// Folder: SiegeEngine.Core.Managers
// File: PanelManager.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.Core.Managers
{
    public class PanelManager
    {
        private readonly DockManager _dockManager;
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly nint _window;
        private readonly EventBus _eventBus;
        private bool _prevMouseDown;
        private readonly List<IPanel> _panels = new List<IPanel>();
        public PanelManager(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _dockManager = new DockManager(renderContext, controlContext, eventBus);
            _eventBus.Subscribe<OpenPanelEvent>(OnOpenPanel);
            _eventBus.Subscribe<ClosePanelEvent>(OnClosePanel);
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
            _dockManager.AddPanel(panel);
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
            _dockManager.Update(deltaTime, mousePos, currentMouseDown, mousePressed, mouseReleased, _eventBus, winW, winH);
        }
        public void Render()
        {
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            _renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            _renderContext.Viewport(0, 0, (uint)winW, (uint)winH);
            _dockManager.Render(_renderContext, winW, winH);
        }
        public void RemovePanel(IPanel panel)
        {
            _dockManager.RemovePanel(panel);
            _panels.Remove(panel);
            panel.Dispose();
        }
    }
}