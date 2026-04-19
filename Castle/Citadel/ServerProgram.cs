// Citadel/ServerProgram.cs
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

            if (isLocal)
            {
                Console.WriteLine("ServerProgram: Running in local mode (authoritative server for single-player testing)");
            }
            else if (isServerMode)
            {
                Console.WriteLine("ServerProgram: Running in dedicated server mode");
            }

            try
            {
                _eventBus = new EventBus();
                _steamEngine = new SteamEngine(_eventBus);
                if (!_steamEngine.Initialize())
                {
                    Console.WriteLine("Citadel: SteamEngine init failed.");
                    return;
                }
                if (!_steamEngine.InitializeServer(0, 27015, "Citadel Server"))
                {
                    Console.WriteLine("Citadel: Server init failed.");
                    return;
                }
                _gameServer = new GameServer(_eventBus);
                _networkManager = new NetworkManager(_steamEngine, _eventBus);
                _networkManager.Start();
                Console.WriteLine("Citadel: Server running on port 27015. Press ESC to stop.");

                bool running = true;
                var sw = Stopwatch.StartNew();
                while (running)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.Escape)
                        {
                            running = false;
                        }
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
                Console.WriteLine($"Citadel: Error - {ex}");
            }
            finally
            {
                Console.WriteLine("Citadel: Shutting down...");
                _steamEngine?.Dispose();
            }
        }
    }
}