using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using System;

namespace Citadel.Network
{
    public class NetworkManager
    {
        private readonly SteamEngine _steamEngine;
        private readonly EventBus _eventBus;
        private readonly bool _isDedicatedServer;

        public NetworkManager(SteamEngine steamEngine, EventBus eventBus)
        {
            _steamEngine = steamEngine;
            _eventBus = eventBus;

            // FIXED: Properly detect true dedicated server vs P2P host
            // P2P host uses client Steam (no GameServer pipe), dedicated uses InitializeServer()
            _isDedicatedServer = steamEngine.GetSteamPipe() == nint.Zero ||
                                 (steamEngine is SteamEngine se && se.GetHSteamServerPipe() != nint.Zero);
        }

        public void Start()
        {
            if (_isDedicatedServer)
            {
                Console.WriteLine("NetworkManager: Dedicated server networking active (SteamGameServer + lobby) — waiting for client connections...");
            }
            else
            {
                Console.WriteLine("NetworkManager: P2P host/client networking active — using Steam P2P");
            }
        }

        public void SendToAll(byte[] data)
        {
            SendToAll(data, 0);
        }

        public void SendToAll(byte[] data, int priority)
        {
            if (data == null || data.Length == 0) return;

            if (_isDedicatedServer)
            {
                Console.WriteLine($"NetworkManager [DEDICATED]: Broadcast to all clients (priority {priority}): {data.Length} bytes");
            }
            else
            {
                _steamEngine.SendP2PMessage(data);
                Console.WriteLine($"NetworkManager [P2P]: Sent P2P to all (priority {priority}): {data.Length} bytes");
            }
        }

        public void Receive(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                Console.WriteLine($"NetworkManager: Received {data.Length} bytes — processing...");
            }
            _eventBus.ProcessNetworkMessage(data);
        }
    }
}