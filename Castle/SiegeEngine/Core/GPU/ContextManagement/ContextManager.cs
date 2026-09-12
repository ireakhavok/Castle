using System;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public abstract class ContextManager
    {
        protected nint _window;
        protected IRenderContext _renderContext;
        protected IControlContext _controlContext;

        public nint Window => _window;
        public IRenderContext RenderContext => _renderContext;
        public IControlContext ControlContext => _controlContext;
        public virtual string BackendName => "OpenGL";
        public virtual bool DrawsUi => true;

        public abstract void Initialize(int width, int height, string title);
        public abstract void Terminate();

        /// <summary>
        /// Present the back buffer. OpenGL uses glfwSwapBuffers via IControlContext.
        /// DX/VK override this; GlfwControlContext.SwapBuffers forwards here when set.
        /// </summary>
        public virtual void Present() { }

        public static ContextManager Create(string renderer)
        {
            string name = renderer ?? "OpenGL";
            Console.WriteLine($"[ContextManager] Creating backend '{name}'");
            return name switch
            {
                "DirectX11" => new D3D11ContextManager(),
                "DirectX12" => new D3D12ContextManager(),
                "Vulkan" => new VulkanContextManager(),
                _ => new OpenGLContextManager()
            };
        }
    }
}
