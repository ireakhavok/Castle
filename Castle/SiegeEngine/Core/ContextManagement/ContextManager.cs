using System;

namespace SiegeEngine.Core.ContextManagement
{
    public abstract class ContextManager
    {
        protected nint _window;
        protected IRenderContext _renderContext;
        protected IControlContext _controlContext;

        public nint Window => _window;
        public IRenderContext RenderContext => _renderContext;
        public IControlContext ControlContext => _controlContext;

        public abstract void Initialize(int width, int height, string title);
        public abstract void Terminate();
    }
}