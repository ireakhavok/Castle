// Folder: SiegeEngine.UI
// File: BasePanel.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using System;

namespace SiegeEngine.UI
{
    public abstract class BasePanel : IPanel
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly IntPtr _window;
        protected readonly EventBus _eventBus;
        protected UIOverlay _uiOverlay;
        protected int _lastW;
        protected int _lastH;

        public DockState DockState { get; set; } = DockState.Floating;

        protected BasePanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _uiOverlay = CreateUIOverlay();
        }

        protected virtual UIOverlay CreateUIOverlay()
        {
            return new UIOverlay(_renderContext, _controlContext, _window);
        }

        public virtual void Init()
        {
            _uiOverlay.Init();
        }

        public virtual void Update(float deltaTime)
        {
            _uiOverlay.Update(deltaTime);
        }

        public virtual void Render()
        {
            _controlContext.GetWindowSize(_window, out int w, out int h);
            if (w != _lastW || h != _lastH)
            {
                _lastW = w;
                _lastH = h;
                _uiOverlay.RecomputeLayout(w, h);
            }
            _renderContext.Viewport(0, 0, (uint)w, (uint)h);
            _renderContext.ClearColor(0.118f, 0.118f, 0.118f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _uiOverlay.Render();
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public virtual void Dispose()
        {
            _uiOverlay.Dispose();
        }

        public virtual void Detach()
        {
        }
    }
}