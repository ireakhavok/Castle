// Folder: Foundation
// File: Program.cs
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
            bool discoverDedicated = args.Contains("--discover-dedicated");
            ulong connectToServerSteamId = 0;
            ulong specificLobbyId = 0;

            // Parse --connect-to-server <steamid>
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--connect-to-server" && ulong.TryParse(args[i + 1], out ulong id))
                {
                    connectToServerSteamId = id;
                    Console.WriteLine($"Foundation: Connecting directly to dedicated server {connectToServerSteamId}");
                    break;
                }
            }

            // Parse optional --lobby <ID> (still supported)
            if (discoverDedicated && connectToServerSteamId == 0)
            {
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "--lobby" && ulong.TryParse(args[i + 1], out ulong id))
                    {
                        specificLobbyId = id;
                        Console.WriteLine($"Foundation: Using specific lobby ID {specificLobbyId}");
                        break;
                    }
                }

                if (specificLobbyId == 0)
                {
                    Console.WriteLine("Foundation: --discover-dedicated mode — will auto-discover or create dedicated lobbies");
                }
            }

            if (args.Contains("--server"))
            {
                Console.WriteLine("Foundation: Launching dedicated Citadel server...");

                string citadelExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");

                if (!File.Exists(citadelExe))
                {
                    Console.WriteLine($"ERROR: Citadel dedicated server not found at: {citadelExe}");
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

            if (args.Contains("--local"))
            {
                Console.WriteLine("Foundation: Launching local authoritative Citadel server (--local)...");

                string citadelExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");

                if (!File.Exists(citadelExe))
                {
                    Console.WriteLine($"ERROR: Citadel.exe not found at: {citadelExe}");
                    return;
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = citadelExe,
                        Arguments = "--local",
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(citadelExe)
                    };
                    Process.Start(psi);
                    Console.WriteLine("Foundation: Local authoritative server launched successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to launch local Citadel server: {ex.Message}");
                }
                return;
            }

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
                launcher.Start(settings.CurrentRenderer, discoverDedicated, specificLobbyId, connectToServerSteamId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start Trebuchet: {ex.Message}");
            }

            steam.Dispose();
        }
    }
}