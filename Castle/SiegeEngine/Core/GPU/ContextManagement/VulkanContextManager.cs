using System;
using System.Runtime.InteropServices;
using static SiegeEngine.Core.GPU.ContextManagement.VulkanNative;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    /// <summary>
    /// Vulkan instance + device on a GLFW NoApi Win32 window.
    /// Slice 1 creates the device and presents by keeping the HWND alive.
    /// Full swapchain/clear command buffer is the next increment on this same manager.
    /// </summary>
    public sealed class VulkanContextManager : ContextManager
    {
        GlfwNoApiHost _host;
        nint _instance, _device;
        BackendRenderContext _backend;

        public override string BackendName => "Vulkan";
        public override bool DrawsUi => true;

        public override void Initialize(int width, int height, string title)
        {
            _host = new GlfwNoApiHost(width, height, title);
            _window = _host.WindowPtr;

            var createInfo = new VkInstanceCreateInfo { sType = 1 };
            nint rc = vkCreateInstance(ref createInfo, nint.Zero, out _instance);
            if (rc != 0 || _instance == nint.Zero)
                throw new InvalidOperationException($"vkCreateInstance failed ({rc})");

            nint phys = nint.Zero;
            rc = vkEnumeratePhysicalDevices(_instance, out uint count, null);
            if (count == 0)
                throw new InvalidOperationException("vkEnumeratePhysicalDevices returned 0 devices");
            var devices = new nint[count];
            rc = vkEnumeratePhysicalDevices(_instance, out count, devices);
            phys = devices[0];

            var devInfo = new VkDeviceCreateInfo { sType = 3 };
            rc = vkCreateDevice(phys, ref devInfo, nint.Zero, out _device);
            if (rc != 0 || _device == nint.Zero)
                throw new InvalidOperationException($"vkCreateDevice failed ({rc})");

            Console.WriteLine($"[Vulkan] Instance+device ready hwnd=0x{_host.Hwnd:X} devices={count}");

            _backend = new BackendRenderContext("Vulkan", width, height);
            _renderContext = _backend;
            var control = new GlfwControlContext(_host.Glfw);
            control.SetPresentOverride(Present);
            _controlContext = control;
        }

        public override void Present()
        {
            // Swapchain acquire/clear/present lands next. Window stays responsive.
        }

        public override void Terminate()
        {
            if (_device != nint.Zero)
            {
                vkDestroyDevice(_device, nint.Zero);
                _device = nint.Zero;
            }
            if (_instance != nint.Zero)
            {
                vkDestroyInstance(_instance, nint.Zero);
                _instance = nint.Zero;
            }
            _host?.Dispose();
        }
    }
}
