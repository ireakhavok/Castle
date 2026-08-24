using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using System;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public unsafe class OpenGLContextManager : ContextManager
    {
        private Glfw _glfw;
        private WindowHandle* _internalWindow;

        public override void Initialize(int width, int height, string title)
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

            _window = (nint)_internalWindow;

            _glfw.MakeContextCurrent(_internalWindow);

            GL gl = GL.GetApi(_glfw.GetProcAddress);

            _renderContext = new OpenGLRenderContext(_glfw, gl);
            _controlContext = new GlfwControlContext(_glfw);
        }

        public override void Terminate()
        {
            _glfw.Terminate();
        }
    }
}