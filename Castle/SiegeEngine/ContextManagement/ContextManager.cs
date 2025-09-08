using System;

namespace SiegeEngine.ContextManagement
{
    public abstract class ContextManager
    {
        protected IntPtr _window;
        protected IRenderContext _renderContext;
        protected IControlContext _controlContext;

        public IntPtr Window => _window;
        public IRenderContext RenderContext => _renderContext;
        public IControlContext ControlContext => _controlContext;

        public abstract void Initialize(int width, int height, string title);
        public abstract void Terminate();
    }
}