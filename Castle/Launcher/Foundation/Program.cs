// Folder: Foundation
// File: Program.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU;
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
            string sceneDataPayload = null;
            string playPayloadFile = null;

            int i = 0;
            while (i < args.Length)
            {
                if (i + 1 < args.Length && args[i] == "--play-project")
                    playProjectPath = args[++i];
                else if (i + 1 < args.Length && args[i] == "--load-level")
                    loadLevelName = args[++i];
                else if (i + 1 < args.Length && args[i] == "--connect-to-server" && ulong.TryParse(args[i + 1], out ulong connectId))
                {
                    connectToServerSteamId = connectId;
                    i++;
                }
                else if (i + 1 < args.Length && args[i] == "--lobby" && ulong.TryParse(args[i + 1], out ulong lobbyId))
                {
                    specificLobbyId = lobbyId;
                    i++;
                }
                else if (i + 1 < args.Length && args[i] == "--join" && ulong.TryParse(args[i + 1], out ulong joinId))
                {
                    joinLobbyId = joinId;
                    i++;
                }
                else if (i + 1 < args.Length && args[i] == "--level-data")
                    levelDataPayload = args[++i];
                else if (i + 1 < args.Length && args[i] == "--scene-data")
                    sceneDataPayload = args[++i];
                else if (i + 1 < args.Length && args[i] == "--play-payload-file")
                    playPayloadFile = args[++i];
                i++;
            }

            // Prefer temp payload file when present (pure in-memory Play transfer vehicle).
            // Property names match PascalCase written by BlueprintManager.PlayPayloadTransfer.
            if (!string.IsNullOrEmpty(playPayloadFile) && File.Exists(playPayloadFile))
            {
                try
                {
                    string json = File.ReadAllText(playPayloadFile);
                    Console.WriteLine($"[Program] Loading play payload file ({json.Length} chars): {playPayloadFile}");
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("LevelName", out JsonElement nameElem))
                        {
                            string name = nameElem.GetString();
                            if (!string.IsNullOrEmpty(name)) loadLevelName = name;
                        }
                        if (root.TryGetProperty("LevelDataBase64", out JsonElement levelElem))
                        {
                            string b64 = levelElem.GetString();
                            if (!string.IsNullOrEmpty(b64)) levelDataPayload = b64;
                        }
                        if (root.TryGetProperty("SceneData", out JsonElement sceneElem))
                        {
                            // Use raw JSON text of the SceneData object, then base64 it for the existing pipeline.
                            string sceneJson = sceneElem.GetRawText();
                            sceneDataPayload = Convert.ToBase64String(
                                System.Text.Encoding.UTF8.GetBytes(sceneJson));
                        }
                    }
                    Console.WriteLine($"[Program] Play payload loaded - levelData={(levelDataPayload != null)}, sceneData={(sceneDataPayload != null)}, levelName={loadLevelName}");
                    // Best-effort cleanup of the transfer file.
                    try { File.Delete(playPayloadFile); } catch { }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Program] Failed to load play payload file '{playPayloadFile}': {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(playPayloadFile))
            {
                Console.WriteLine($"[Program] Play payload file not found: {playPayloadFile}");
            }

            if (isClientRuntime || !string.IsNullOrEmpty(playProjectPath))
            {
                Console.WriteLine($"Foundation: PURE CLIENT RUNTIME MODE - project '{playProjectPath ?? "IDE"}' level '{loadLevelName}' (single process, no server spawn, no recursion) - payloads present: levelData={levelDataPayload != null}, sceneData={sceneDataPayload != null}");
                var launcher = new Launcher();
                launcher.Start("OpenGL", false, 0, 0, false, 0, true, playProjectPath, loadLevelName, levelDataPayload, sceneDataPayload);
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