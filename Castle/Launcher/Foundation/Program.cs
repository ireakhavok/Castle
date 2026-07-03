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
            bool isP2PHost = args.Contains("--host");
            bool discoverP2PHost = false;
            ulong joinLobbyId = 0;

            bool isClientRuntime = args.Contains("--client");
            string playProjectPath = null;
            string loadLevelName = "Main";
            string levelDataPayload = null;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--play-project")
                    playProjectPath = args[i + 1];
                else if (args[i] == "--load-level")
                    loadLevelName = args[i + 1];
                else if (args[i] == "--connect-to-server" && ulong.TryParse(args[i + 1], out ulong connectId))
                    connectToServerSteamId = connectId;
                else if (args[i] == "--lobby" && ulong.TryParse(args[i + 1], out ulong lobbyId))
                    specificLobbyId = lobbyId;
                else if (args[i] == "--join" && ulong.TryParse(args[i + 1], out ulong joinId))
                    joinLobbyId = joinId;
                else if (args[i] == "--level-data")
                    levelDataPayload = args[i + 1];
            }

            if (isClientRuntime || !string.IsNullOrEmpty(playProjectPath))
            {
                Console.WriteLine($"Foundation: PURE CLIENT RUNTIME MODE - project '{playProjectPath ?? "IDE"}' level '{loadLevelName}' (single process, no server spawn, no recursion)");
                var launcher = new Launcher();
                launcher.Start("OpenGL", false, 0, 0, false, 0, true, playProjectPath, loadLevelName, levelDataPayload);
                return; // No server, no editor, no recursion
            }

            if (isP2PHost)
            {
                Console.WriteLine("Foundation: P2P HOST MODE — launching self-contained Citadel.exe --p2p-host");
                string citadelExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");
                if (File.Exists(citadelExe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = citadelExe,
                        Arguments = "--p2p-host",
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(citadelExe)
                    };
                    Process.Start(psi);
                }
                discoverP2PHost = true;
            }

            if (args.Contains("--server"))
            {
                Console.WriteLine("Foundation: Launching dedicated Citadel server...");
                string citadelExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");
                if (File.Exists(citadelExe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = citadelExe,
                        Arguments = "--server",
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(citadelExe)
                    };
                    Process.Start(psi);
                }
                return;
            }

            if (args.Contains("--local"))
            {
                Console.WriteLine("Foundation: Launching local authoritative Citadel server (--local)...");
                string citadelExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");
                if (File.Exists(citadelExe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = citadelExe,
                        Arguments = "--local",
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(citadelExe)
                    };
                    Process.Start(psi);
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
                launcher.Start(settings.CurrentRenderer, discoverDedicated, specificLobbyId, connectToServerSteamId, discoverP2PHost, joinLobbyId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start Trebuchet: {ex.Message}");
            }

            steam.Dispose();
        }
    }
}