using Citadel.Network;
using Citadel.Server;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using System;
using System.Diagnostics;
using System.Linq;

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
                Console.WriteLine("Citadel: Server running...");
                bool isLocal = args.Contains("--local");
                if (isLocal)
                {
                    Console.WriteLine("ServerProgram: Running in local mode");
                }
                bool running = true;
                while (running)
                {
                    _steamEngine.RunCallbacks();
                    _gameServer.Update(1f / 60f); // 60 FPS tick
                    System.Threading.Thread.Sleep(16); // ~60 FPS
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Citadel: Error - {ex}");
            }
            finally
            {
                _steamEngine?.Dispose();
            }
        }
    }
}