// Citadel/Network/NetworkManager.cs
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
            _isDedicatedServer = steamEngine.GetSteamPipe() != nint.Zero; // server pipe active
        }

        public void Start()
        {
            Console.WriteLine(_isDedicatedServer
                ? "NetworkManager: Dedicated server networking active (SteamGameServer)"
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
                // Dedicated server path (SteamGameServer)
                Console.WriteLine($"NetworkManager: Broadcast to all clients (dedicated, priority {priority}): {data.Length} bytes");
                // TODO: replace placeholder with actual SteamGameServer send when full SDK integration complete
            }
            else
            {
                // P2P / client-hosted path
                _steamEngine.SendP2PMessage(data);
                Console.WriteLine($"NetworkManager: Sent P2P to all (priority {priority}): {data.Length} bytes");
            }
        }

        public void Receive(byte[] data)
        {
            _eventBus.ProcessNetworkMessage(data);
        }
    }
}