using System;
using System.Diagnostics;
using System.IO;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    /// <summary>
    /// Renderer is chosen at process start. GLFW cannot hot-swap GL ↔ DX ↔ Vulkan
    /// on the same window. Save settings.json, spawn this exe again, then exit.
    /// </summary>
    public static class RendererSwitch
    {
        public static void Relaunch()
        {
            string exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe))
            {
                Console.WriteLine("[RendererSwitch] Cannot resolve process path; exit without relaunch");
                Environment.Exit(0);
                return;
            }

            string args = Environment.CommandLine;
            int firstSpace = args.IndexOf(' ');
            string passArgs = firstSpace >= 0 ? args.Substring(firstSpace + 1) : "";

            Console.WriteLine($"[RendererSwitch] Relaunching '{exe}' {passArgs}");
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = passArgs,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            };
            Process.Start(psi);
            Environment.Exit(0);
        }
    }
}
