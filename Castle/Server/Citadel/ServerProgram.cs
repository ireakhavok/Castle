// Folder: Citadel
// File: ServerProgram.cs
using Citadel.Network;
using Citadel.Server;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Trebuchet;

namespace Citadel
{
    class ServerProgram
    {
        private static SteamEngine _steamEngine;
        private static GameServer _gameServer;
        private static EventBus _eventBus;
        private static NetworkManager _networkManager;

        static void Main(string[] args)
        {
            // ULTRA-EARLY CLIENT BYPASS - first executable statement (fixes stale binary / ignored flag)
            bool isClientRuntime = args != null && args.Any(a => a.Trim() == "--client" || a.Contains("--client"));
            string loadLevelName = "Main";
            string playProjectPath = null;

            Console.WriteLine($"[ServerProgram.Entry] Args received: {string.Join(" ", args ?? new string[0])} | isClientRuntime={isClientRuntime}");

            if (args != null)
            {
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "--load-level" && i + 1 < args.Length)
                        loadLevelName = args[i + 1];
                    else if (args[i] == "--play-project" && i + 1 < args.Length)
                        playProjectPath = args[i + 1];
                }
            }

            if (isClientRuntime)
            {
                Console.WriteLine($"[ServerProgram] PURE CLIENT RUNTIME ACTIVATED - level '{loadLevelName}' (bypass complete - no server init, no NRE, isolated window guaranteed)");
                var launcher = new Launcher();
                launcher.Start("OpenGL", false, 0, 0, false, 0, true, playProjectPath, loadLevelName);
                Console.WriteLine("[ServerProgram] Client runtime exited cleanly");
                return; // ABSOLUTE short-circuit - nothing below executes in client mode
            }

            // All server-only code below
            Console.Title = "Citadel Dedicated Server";
            Console.WriteLine("Citadel Server starting...");

            bool isServerMode = args != null && (args.Contains("--server") || args.Length == 0);
            bool isLocal = args != null && args.Contains("--local");
            bool isP2PHost = args != null && args.Contains("--p2p-host");

            if (isP2PHost) Console.WriteLine("ServerProgram: Running in P2P HOST mode...");
            else if (isLocal) Console.WriteLine("ServerProgram: Running in local authoritative mode...");
            else if (isServerMode) Console.WriteLine("ServerProgram: Running in dedicated server mode...");

            try
            {
                _eventBus = new EventBus();
                _steamEngine = new SteamEngine(_eventBus);
                if (!_steamEngine.Initialize()) { Console.WriteLine("Citadel: Steam client init failed."); return; }

                if (isP2PHost)
                {
                    ((SteamEngine)_steamEngine).SetP2PHostMode(true);
                    _networkManager = new NetworkManager((SteamEngine)_steamEngine, _eventBus);
                    _gameServer = new GameServer(_eventBus, _networkManager, isEditor: false);
                    ((SteamEngine)_steamEngine).CreateLobby(64);
                    _networkManager.Start();
                }
                else if (isServerMode || isLocal)
                {
                    if (isServerMode && !_steamEngine.InitializeServer(0, 27015, "Citadel Server"))
                    {
                        Console.WriteLine("Citadel: Steam GameServer init failed.");
                        return;
                    }
                    _gameServer = new GameServer(_eventBus, null, isEditor: false);
                    _networkManager = new NetworkManager(_steamEngine, _eventBus);
                    _networkManager.Start();
                }

                // server loop...
                bool running = true;
                var sw = Stopwatch.StartNew();
                Console.CancelKeyPress += (s, e) => { running = false; e.Cancel = true; };
                while (running)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) running = false;
                    _steamEngine.RunCallbacks();
                    float deltaTime = (float)sw.Elapsed.TotalSeconds;
                    sw.Restart();
                    _gameServer?.Update(deltaTime);
                    Thread.Sleep(16);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Citadel: Fatal error - {ex}");
            }
            finally
            {
                Console.WriteLine("Citadel: Shutting down server...");
                _gameServer = null;
                _steamEngine?.Dispose();
                Console.WriteLine("Citadel: Shutdown complete.");
            }
        }
    }
}