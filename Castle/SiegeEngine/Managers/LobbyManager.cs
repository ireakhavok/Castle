using System;
using SiegeEngine.Interfaces;

namespace SiegeEngine.Managers
{
    public class LobbyManager
    {
        private readonly ISteamEngine _steamEngine;

        public LobbyManager(ISteamEngine steamEngine)
        {
            _steamEngine = steamEngine ?? throw new ArgumentNullException(nameof(steamEngine));
        }

        public void CreateLobby()
        {
            _steamEngine.CreateLobby(4);
        }

        public void Update()
        {
            _steamEngine.RunCallbacks();
        }

        public bool IsLobbyCreated()
        {
            return _steamEngine.IsLobbyCreated();
        }

        public bool IsLobbyJoined()
        {
            return _steamEngine.IsLobbyJoined();
        }
    }
}