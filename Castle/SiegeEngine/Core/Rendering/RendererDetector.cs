// SiegeEngine/Rendering/RendererDetector.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static SiegeEngine.Core.Rendering.ContextManagement.VulkanNative;

namespace SiegeEngine.Core.Rendering
{
    public static class RendererDetector
    {
        private const string OpenGLDllWindows = "opengl32.dll";
        private const string OpenGLDllLinux = "libGL.so.1";
        private const string OpenGLDllMac = "/System/Library/Frameworks/OpenGL.framework/OpenGL";

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint LoadLibraryWindows(string lpFileName);

        [DllImport("libdl.so.2", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint LoadLibraryLinux(string lpFileName);

        [DllImport("/usr/lib/libdyld.dylib", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint LoadLibraryMac(string lpFileName);

        private static readonly Guid IID_ID3D12Device = new Guid("189819f1-1db6-4b57-be54-1821339b85f7");

        [DllImport("d3d11.dll", EntryPoint = "D3D11CreateDevice", CallingConvention = CallingConvention.StdCall)]
        private static extern int D3D11CreateDevice(
            nint pAdapter,
            int DriverType,
            nint Software,
            uint Flags,
            int[] pFeatureLevels,
            uint FeatureLevels,
            uint SDKVersion,
            out nint ppDevice,
            out int pFeatureLevel,
            out nint ppImmediateContext);

        [DllImport("d3d12.dll", EntryPoint = "D3D12CreateDevice", CallingConvention = CallingConvention.StdCall)]
        private static extern int D3D12CreateDevice(
            nint pAdapter,
            int MinimumFeatureLevel,
            ref Guid riid,
            out nint ppDevice);

        public static List<string> DetectAvailable()
        {
            List<string> available = new List<string>();

            // Detect OpenGL
            if (IsOpenGLSupported())
            {
                available.Add("OpenGL");
            }

            // Detect Vulkan
            try
            {
                VkInstanceCreateInfo createInfo = new VkInstanceCreateInfo
                {
                    sType = 1 // VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO
                };
                nint result = vkCreateInstance(ref createInfo, nint.Zero, out nint instance);
                if (result == 0 && instance != nint.Zero)
                {
                    vkDestroyInstance(instance, nint.Zero);
                    available.Add("Vulkan");
                }
            }
            catch
            {
                // Vulkan not supported
            }

            // Detect DirectX 11
            try
            {
                int[] featureLevels = { 0xB000 }; // D3D_FEATURE_LEVEL_11_0
                int res = D3D11CreateDevice(nint.Zero, 1, nint.Zero, 0, featureLevels, 1, 7, out nint device, out int level, out nint context);
                if (res == 0 && level >= 0xB000)
                {
                    if (device != nint.Zero) Marshal.Release(device);
                    if (context != nint.Zero) Marshal.Release(context);
                    available.Add("DirectX11");
                }
            }
            catch
            {
                // DirectX 11 not supported
            }

            // Detect DirectX 12
            try
            {
                Guid iid = IID_ID3D12Device;
                int res = D3D12CreateDevice(nint.Zero, 0xB000, ref iid, out nint device);
                if (res == 0 && device != nint.Zero)
                {
                    Marshal.Release(device);
                    available.Add("DirectX12");
                }
            }
            catch
            {
                // DirectX 12 not supported
            }

            if (available.Count == 0)
            {
                available.Add("OpenGL"); // Fallback to OpenGL if nothing else
            }

            return available;
        }

        private static bool IsOpenGLSupported()
        {
            nint handle = nint.Zero;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    handle = LoadLibraryWindows(OpenGLDllWindows);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    handle = LoadLibraryLinux(OpenGLDllLinux);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    handle = LoadLibraryMac(OpenGLDllMac);
                }
                return handle != nint.Zero;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (handle != nint.Zero)
                {
                    // No need to free library as it's just for detection
                }
            }
        }
    }
}