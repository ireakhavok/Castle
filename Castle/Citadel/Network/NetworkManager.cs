// Folder: Citadel/Network
// File: NetworkManager.cs
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
            _isDedicatedServer = steamEngine.GetSteamPipe() != nint.Zero;
        }

        public void Start()
        {
            Console.WriteLine(_isDedicatedServer
                ? "NetworkManager: Dedicated server networking active (SteamGameServer + lobby) — waiting for client connections..."
                : "NetworkManager: P2P networking active");
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
                Console.WriteLine($"NetworkManager: Sent P2P to all (priority {priority}): {data.Length} bytes");
            }
        }

        public void Receive(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                Console.WriteLine($"NetworkManager [DEDICATED]: Received {data.Length} bytes from client — processing...");
            }
            _eventBus.ProcessNetworkMessage(data);
        }
    }
}