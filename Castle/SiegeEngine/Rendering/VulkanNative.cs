using System;
using System.Runtime.InteropServices;

namespace SiegeEngine.Rendering
{
    /// <summary>
    /// Provides P/Invoke wrappers for Vulkan API functions with cross-platform support.
    /// </summary>
    public static class VulkanNative
    {
        private const string VulkanDllWindows = "vulkan-1.dll";
        private const string VulkanDllLinux = "libvulkan.so";

        /// <summary>
        /// Creates a Vulkan instance.
        /// </summary>
        /// <param name="pCreateInfo">Pointer to the instance create info structure.</param>
        /// <param name="pAllocator">Pointer to the allocator (optional).</param>
        /// <param name="pInstance">Pointer to store the created instance.</param>
        /// <returns>The Vulkan result code.</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown if the operating system is not supported.</exception>
        public static nint vkCreateInstance(ref VkInstanceCreateInfo pCreateInfo, nint pAllocator, out nint pInstance)
        {
#if Windows
            return vkCreateInstanceWindows(ref pCreateInfo, pAllocator, out pInstance);
#elif Linux
            return vkCreateInstanceLinux(ref pCreateInfo, pAllocator, out pInstance);
#else
            throw new PlatformNotSupportedException("Vulkan is not supported on this operating system.");
#endif
        }

        /// <summary>
        /// Destroys a Vulkan instance.
        /// </summary>
        /// <param name="instance">The Vulkan instance to destroy.</param>
        /// <param name="pAllocator">Pointer to the allocator (optional).</param>
        /// <exception cref="PlatformNotSupportedException">Thrown if the operating system is not supported.</exception>
        public static void vkDestroyInstance(nint instance, nint pAllocator)
        {
#if Windows
            vkDestroyInstanceWindows(instance, pAllocator);
#elif Linux
            vkDestroyInstanceLinux(instance, pAllocator);
#else
            throw new PlatformNotSupportedException("Vulkan is not supported on this operating system.");
#endif
        }

        /// <summary>
        /// Enumerates physical devices available for a Vulkan instance.
        /// </summary>
        /// <param name="instance">The Vulkan instance.</param>
        /// <param name="pPhysicalDeviceCount">Pointer to store the number of physical devices.</param>
        /// <param name="pPhysicalDevices">Array to store the physical device handles.</param>
        /// <returns>The Vulkan result code.</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown if the operating system is not supported.</exception>
        public static nint vkEnumeratePhysicalDevices(nint instance, out uint pPhysicalDeviceCount, nint[] pPhysicalDevices)
        {
#if Windows
            return vkEnumeratePhysicalDevicesWindows(instance, out pPhysicalDeviceCount, pPhysicalDevices);
#elif Linux
            return vkEnumeratePhysicalDevicesLinux(instance, out pPhysicalDeviceCount, pPhysicalDevices);
#else
            throw new PlatformNotSupportedException("Vulkan is not supported on this operating system.");
#endif
        }

        /// <summary>
        /// Creates a Vulkan device.
        /// </summary>
        /// <param name="physicalDevice">The physical device to create the device from.</param>
        /// <param name="pCreateInfo">Pointer to the device create info structure.</param>
        /// <param name="pAllocator">Pointer to the allocator (optional).</param>
        /// <param name="pDevice">Pointer to store the created device.</param>
        /// <returns>The Vulkan result code.</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown if the operating system is not supported.</exception>
        public static nint vkCreateDevice(nint physicalDevice, ref VkDeviceCreateInfo pCreateInfo, nint pAllocator, out nint pDevice)
        {
#if Windows
            return vkCreateDeviceWindows(physicalDevice, ref pCreateInfo, pAllocator, out pDevice);
#elif Linux
            return vkCreateDeviceLinux(physicalDevice, ref pCreateInfo, pAllocator, out pDevice);
#else
            throw new PlatformNotSupportedException("Vulkan is not supported on this operating system.");
#endif
        }

        /// <summary>
        /// Destroys a Vulkan device.
        /// </summary>
        /// <param name="device">The Vulkan device to destroy.</param>
        /// <param name="pAllocator">Pointer to the allocator (optional).</param>
        /// <exception cref="PlatformNotSupportedException">Thrown if the operating system is not supported.</exception>
        public static void vkDestroyDevice(nint device, nint pAllocator)
        {
#if Windows
            vkDestroyDeviceWindows(device, pAllocator);
#elif Linux
            vkDestroyDeviceLinux(device, pAllocator);
#else
            throw new PlatformNotSupportedException("Vulkan is not supported on this operating system.");
#endif
        }

        // Windows-specific imports
#if Windows
        [DllImport(VulkanDllWindows, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr vkCreateInstanceWindows(ref VkInstanceCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pInstance);

        [DllImport(VulkanDllWindows, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vkDestroyInstanceWindows(IntPtr instance, IntPtr pAllocator);

        [DllImport(VulkanDllWindows, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr vkEnumeratePhysicalDevicesWindows(IntPtr instance, out uint pPhysicalDeviceCount, IntPtr[] pPhysicalDevices);

        [DllImport(VulkanDllWindows, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr vkCreateDeviceWindows(IntPtr physicalDevice, ref VkDeviceCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pDevice);

        [DllImport(VulkanDllWindows, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vkDestroyDeviceWindows(IntPtr device, IntPtr pAllocator);
#endif

        // Linux-specific imports
#if Linux
        [DllImport(VulkanDllLinux, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr vkCreateInstanceLinux(ref VkInstanceCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pInstance);

        [DllImport(VulkanDllLinux, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vkDestroyInstanceLinux(IntPtr instance, IntPtr pAllocator);

        [DllImport(VulkanDllLinux, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr vkEnumeratePhysicalDevicesLinux(IntPtr instance, out uint pPhysicalDeviceCount, IntPtr[] pPhysicalDevices);

        [DllImport(VulkanDllLinux, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr vkCreateDeviceLinux(IntPtr physicalDevice, ref VkDeviceCreateInfo pCreateInfo, IntPtr pAllocator, out IntPtr pDevice);

        [DllImport(VulkanDllLinux, CallingConvention = CallingConvention.Cdecl)]
        private static extern void vkDestroyDeviceLinux(IntPtr device, IntPtr pAllocator);
#endif

        /// <summary>
        /// Defines the Vulkan instance create info structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct VkInstanceCreateInfo
        {
            public int sType; // VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO = 1
            public nint pNext;
            public uint flags;
            public nint pApplicationInfo;
            public uint enabledLayerCount;
            public nint ppEnabledLayerNames;
            public uint enabledExtensionCount;
            public nint ppEnabledExtensionNames;
        }

        /// <summary>
        /// Defines the Vulkan device create info structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct VkDeviceCreateInfo
        {
            public int sType; // VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO = 3
            public nint pNext;
            public uint flags;
            public uint queueCreateInfoCount;
            public nint pQueueCreateInfos;
            public uint enabledLayerCount;
            public nint ppEnabledLayerNames;
            public uint enabledExtensionCount;
            public nint ppEnabledExtensionNames;
            public nint pEnabledFeatures;
        }
    }
}