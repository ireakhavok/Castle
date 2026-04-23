using Citadel.Network;
using Citadel.Server;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

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
            Console.Title = "Citadel Dedicated Server";
            Console.WriteLine("Citadel Server starting...");

            bool isServerMode = args.Contains("--server") || args.Length == 0;
            bool isLocal = args.Contains("--local");
            bool isP2PHost = args.Contains("--p2p-host");

            if (isP2PHost)
            {
                Console.WriteLine("ServerProgram: Running in P2P HOST mode (authoritative GameServer + P2P lobby via client Steam — self-contained)");
            }
            else if (isLocal)
            {
                Console.WriteLine("ServerProgram: Running in local authoritative mode (single-player testing)");
            }
            else if (isServerMode)
            {
                Console.WriteLine("ServerProgram: Running in dedicated server mode (port 27015)");
            }

            try
            {
                _eventBus = new EventBus();
                _steamEngine = new SteamEngine(_eventBus);
                if (!_steamEngine.Initialize())
                {
                    Console.WriteLine("Citadel: Steam client init failed. Dedicated server requires Steam running.");
                    return;
                }

                if (isP2PHost)
                {
                    // P2P host path: client Steam + authoritative GameServer (no dedicated GameServer init on port)
                    ((SteamEngine)_steamEngine).SetP2PHostMode(true);
                    _networkManager = new NetworkManager((SteamEngine)_steamEngine, _eventBus);
                    _gameServer = new GameServer(_eventBus, _networkManager, isEditor: false);
                    ((SteamEngine)_steamEngine).CreateLobby(64);
                    _networkManager.Start();
                    Console.WriteLine("Citadel P2P Host: Lobby created with p2p-host=true metadata. Waiting for client P2P connections...");
                }
                else if (isServerMode || isLocal)
                {
                    if (isServerMode)
                    {
                        if (!_steamEngine.InitializeServer(0, 27015, "Citadel Server"))
                        {
                            Console.WriteLine("Citadel: Steam GameServer init failed on port 27015.");
                            return;
                        }
                    }

                    _gameServer = new GameServer(_eventBus, null, isEditor: false);
                    _networkManager = new NetworkManager(_steamEngine, _eventBus);
                    _networkManager.Start();

                    if (isServerMode)
                    {
                        Console.WriteLine("Citadel: Dedicated server running on port 27015. Waiting for client connections... Press ESC to stop.");
                    }
                }

                bool running = true;
                var sw = Stopwatch.StartNew();
                Console.CancelKeyPress += (s, e) => { running = false; e.Cancel = true; };

                while (running)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.Escape)
                            running = false;
                    }

                    _steamEngine.RunCallbacks();
                    float deltaTime = (float)sw.Elapsed.TotalSeconds;
                    sw.Restart();
                    _gameServer.Update(deltaTime);
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