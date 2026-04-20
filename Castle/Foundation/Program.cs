// Foundation/Program.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using Trebuchet;

namespace Foundation
{
    class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int MessageBox(nint hWnd, string text, string caption, uint type);

        static void Main(string[] args)
        {
            if (args.Contains("--server"))
            {
                Console.WriteLine("Foundation: Launching dedicated Citadel server...");

                // Robust path to the published Citadel.exe (copied by Citadel post-build to Libraries)
                string citadelExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");

                if (!File.Exists(citadelExe))
                {
                    Console.WriteLine($"ERROR: Citadel dedicated server not found at: {citadelExe}");
                    Console.WriteLine("Please build the Citadel project first (dotnet build Citadel/Citadel.csproj).");
                    return;
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = citadelExe,
                        Arguments = "--server",
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(citadelExe)
                    };
                    Process.Start(psi);
                    Console.WriteLine("Foundation: Dedicated server launched successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to launch Citadel server: {ex.Message}");
                }
                return;
            }

            // Normal client launcher path (unchanged)
            var steam = new SteamEngine();
            if (!steam.Initialize())
            {
                MessageBox(nint.Zero, "Steam is not running or no valid Steam account detected. Please launch Steam and log in.", "Authentication Error", 0);
                return;
            }

            var availableRenderers = RendererDetector.DetectAvailable();

            var settings = new UISettingsManager();
            settings.LoadSettings();

            if (!availableRenderers.Contains(settings.CurrentRenderer))
            {
                settings.CurrentRenderer = "OpenGL";
            }
            settings.AvailableRenderers = availableRenderers;
            settings.SaveSettings();

            try
            {
                var launcher = new Launcher();
                launcher.Start(settings.CurrentRenderer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start Trebuchet: {ex.Message}");
            }

            steam.Dispose();
        }
    }
}