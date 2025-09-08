// SiegeEngine.ContextManagement/ContextManager.cs
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using System;

namespace SiegeEngine.ContextManagement
{
    public unsafe class ContextManager
    {
        private Glfw _glfw;
        private WindowHandle* _internalWindow;
        private IntPtr _window;
        private IRenderContext _renderContext;
        private IControlContext _controlContext;

        public IntPtr Window => _window;
        public IRenderContext RenderContext => _renderContext;
        public IControlContext ControlContext => _controlContext;

        public void Initialize(int width, int height, string title)
        {
            _glfw = Glfw.GetApi();
            if (!_glfw.Init())
            {
                throw new Exception("Failed to initialize GLFW");
            }

            _glfw.WindowHint(WindowHintBool.Resizable, true);
            _glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
            _glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
            _glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

            _internalWindow = _glfw.CreateWindow(width, height, title, null, null);
            if (_internalWindow == null)
            {
                _glfw.Terminate();
                throw new Exception("Failed to create GLFW window");
            }

            _window = (IntPtr)_internalWindow;

            _glfw.MakeContextCurrent(_internalWindow);

            GL gl = GL.GetApi(_glfw.GetProcAddress);

            _renderContext = new OpenGLRenderContext(_glfw, gl);
            _controlContext = new GlfwControlContext(_glfw);
        }

        public void Terminate()
        {
            _glfw.Terminate();
        }
    }
}