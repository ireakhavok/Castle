using Silk.NET.GLFW;
using System;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    internal unsafe sealed class GlfwNoApiHost : IDisposable
    {
        public Glfw Glfw { get; }
        public WindowHandle* Window { get; private set; }
        public nint WindowPtr => (nint)Window;
        public nint Hwnd { get; private set; }

        [DllImport("glfw3", EntryPoint = "glfwGetWin32Window", CallingConvention = CallingConvention.Cdecl)]
        static extern nint glfwGetWin32Window(nint window);

        public GlfwNoApiHost(int width, int height, string title)
        {
            Glfw = Silk.NET.GLFW.Glfw.GetApi();
            if (!Glfw.Init())
                throw new Exception("Failed to initialize GLFW");

            Glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
            Glfw.WindowHint(WindowHintBool.Resizable, true);
            Window = Glfw.CreateWindow(width, height, title ?? "SiegeEngine", null, null);
            if (Window == null)
            {
                Glfw.Terminate();
                throw new Exception("Failed to create GLFW window (NoApi)");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Hwnd = glfwGetWin32Window(WindowPtr);
                if (Hwnd == nint.Zero)
                    throw new Exception("glfwGetWin32Window returned 0");
            }
        }

        public void Dispose()
        {
            if (Window != null)
            {
                Glfw.DestroyWindow(Window);
                Window = null;
            }
            Glfw.Terminate();
        }
    }
}
