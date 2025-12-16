using System;

namespace SiegeEngine.Core.Interfaces
{
    public interface ISteamEngine : IDisposable
    {
        bool Initialize();
        void RunCallbacks();
        nint GetSteamPipe();
        void CreateLobby(int maxPlayers);
        void JoinLobby(ulong lobbyId);
        bool IsLobbyCreated();
        bool IsLobbyJoined();
        ulong GetLobbyId();
        bool InitializeServer(uint ip, ushort port, string serverName);
        void ShutdownServer();
        bool StartVoiceRecording();
        bool StopVoiceRecording();
        byte[] GetVoiceData();
        void ConnectP2P(ulong steamId);
        void SendP2PMessage(byte[] data);
        void CreateWorkshopItem(string title, string description, string contentPath);
        ulong[] GetSubscribedWorkshopItems();
        string GetWorkshopItemInstallInfo(ulong itemId); // Added this
    }
}