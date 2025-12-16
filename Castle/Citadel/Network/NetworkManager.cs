using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using System;

namespace Citadel.Network
{
    public class NetworkManager
    {
        private readonly SteamEngine _steamEngine;
        private readonly EventBus _eventBus;

        public NetworkManager(SteamEngine steamEngine, EventBus eventBus)
        {
            _steamEngine = steamEngine;
            _eventBus = eventBus;
        }

        public void Start()
        {
            Console.WriteLine("NetworkManager: Started (placeholder for SteamGameServer comms)");
        }

        public void SendToAll(byte[] data)
        {
            SendToAll(data, 0); // Default to low priority
        }

        public void SendToAll(byte[] data, int priority) // New overload
        {
            if (_steamEngine.GetSteamPipe() != nint.Zero) // P2P mode
            {
                _steamEngine.SendP2PMessage(data);
                Console.WriteLine($"NetworkManager: Sent to all (P2P, priority {priority}): {data.Length} bytes");
            }
            else if (_steamEngine.GetSteamPipe() != nint.Zero) // Server mode placeholder
            {
                Console.WriteLine($"NetworkManager: Sent to all (SteamGameServer, priority {priority}): {data.Length} bytes (placeholder)");
            }
        }

        public void Receive(byte[] data)
        {
            _eventBus.ProcessNetworkMessage(data);
        }
    }
}