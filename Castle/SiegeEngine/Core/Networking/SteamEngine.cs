// Folder: SiegeEngine/Core/Networking
// File: SteamEngine.cs
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Events;

namespace SiegeEngine.Core.Networking
{
    public class SteamEngine : ISteamEngine
    {
        private nint _hSteamPipe;
        private nint _matchmaking;
        private nint _hSteamServerPipe;
        private nint _gameServer;
        private nint _user;
        private nint _networking;
        private nint _ugc;
        private bool _lobbyCreated;
        private bool _lobbyJoined;
        private ulong _lobbyId;
        private bool _disposed;
        private bool _isDedicatedServer;
        private readonly EventBus _eventBus;
        private readonly List<byte[]> _receivedMessages = new List<byte[]>();
        private readonly Dictionary<ulong, long> _connectionHandles = new Dictionary<ulong, long>();
        private readonly Dictionary<long, bool> _connectionReady = new Dictionary<long, bool>();

        public SteamEngine(EventBus eventBus = null)
        {
            _eventBus = eventBus ?? new EventBus(this);
        }

        public bool Initialize()
        {
            if (!SteamAPI_InitSafe())
            {
                Console.WriteLine("SteamAPI_InitSafe failed.");
                return false;
            }
            Console.WriteLine("SteamAPI initialized successfully.");

            SteamAPI_ManualDispatch_Init();
            _hSteamPipe = SteamAPI_GetHSteamPipe();
            if (_hSteamPipe == nint.Zero)
            {
                Console.WriteLine("Failed to get HSteamPipe.");
                return false;
            }
            Console.WriteLine($"HSteamPipe acquired: {_hSteamPipe}");

            if (!SteamAPI_IsSteamRunning())
            {
                Console.WriteLine("Steam client is not running.");
                return false;
            }
            Console.WriteLine("Steam client is running.");

            _matchmaking = SteamAPI_SteamMatchmaking_v009();
            if (_matchmaking == nint.Zero)
            {
                Console.WriteLine("Failed to get ISteamMatchmaking.");
                return false;
            }
            Console.WriteLine("ISteamMatchmaking interface acquired.");

            _user = SteamAPI_SteamUser_v023();
            if (_user == nint.Zero)
            {
                Console.WriteLine("Failed to get ISteamUser.");
                return false;
            }
            ulong steamId = SteamAPI_ISteamUser_GetSteamID(_user);
            Console.WriteLine($"SteamID: {steamId}");

            nint friends = SteamAPI_SteamFriends_v018();
            if (friends == nint.Zero)
            {
                Console.WriteLine("Failed to get ISteamFriends.");
            }
            else
            {
                StringBuilder username = new StringBuilder(256);
                if (SteamAPI_ISteamFriends_GetPersonaName(friends, username, 256))
                {
                    Console.WriteLine($"Connected as: {username} (SteamID: {steamId})");
                }
                else
                {
                    Console.WriteLine("Failed to get Steam username.");
                }
            }

            _networking = SteamAPI_SteamNetworkingSockets_SteamAPI_v012();
            if (_networking == nint.Zero)
            {
                Console.WriteLine("Failed to get ISteamNetworkingSockets.");
                return false;
            }
            Console.WriteLine("ISteamNetworkingSockets interface acquired.");

            _ugc = SteamAPI_SteamUGC_v021();
            if (_ugc == nint.Zero)
            {
                Console.WriteLine("Failed to get ISteamUGC.");
                return false;
            }
            Console.WriteLine("ISteamUGC interface acquired.");

            return true;
        }

        public void RunCallbacks()
        {
            // CLIENT PIPE
            SteamAPI_ManualDispatch_RunFrame(_hSteamPipe);
            nint callbackMsg = Marshal.AllocHGlobal(Marshal.SizeOf<CallbackMsg_t>());
            try
            {
                while (SteamAPI_ManualDispatch_GetNextCallback(_hSteamPipe, callbackMsg))
                {
                    CallbackMsg_t msg = Marshal.PtrToStructure<CallbackMsg_t>(callbackMsg);

                    if (msg.m_iCallback == 510) OnLobbyCreated(Marshal.PtrToStructure<LobbyCreated_t>(msg.m_pubParam));
                    else if (msg.m_iCallback == 504) OnLobbyEnter(Marshal.PtrToStructure<LobbyEnter_t>(msg.m_pubParam));
                    else if (msg.m_iCallback == 512) OnLobbyMatchList(Marshal.PtrToStructure<LobbyMatchList_t>(msg.m_pubParam));
                    else if (msg.m_iCallback == 1220) OnConnectionStatusChanged(Marshal.PtrToStructure<SteamNetConnectionStatusChanged_t>(msg.m_pubParam));

                    SteamAPI_ManualDispatch_FreeLastCallback(_hSteamPipe);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(callbackMsg);
            }

            // SERVER PIPE
            if (_hSteamServerPipe != nint.Zero)
            {
                SteamGameServer_RunCallbacks();

                SteamAPI_ManualDispatch_RunFrame(_hSteamServerPipe);
                nint serverCallbackMsg = Marshal.AllocHGlobal(Marshal.SizeOf<CallbackMsg_t>());
                try
                {
                    while (SteamAPI_ManualDispatch_GetNextCallback(_hSteamServerPipe, serverCallbackMsg))
                    {
                        CallbackMsg_t msg = Marshal.PtrToStructure<CallbackMsg_t>(serverCallbackMsg);

                        if (msg.m_iCallback == 510) OnLobbyCreated(Marshal.PtrToStructure<LobbyCreated_t>(msg.m_pubParam));
                        else if (msg.m_iCallback == 504) OnLobbyEnter(Marshal.PtrToStructure<LobbyEnter_t>(msg.m_pubParam));
                        else if (msg.m_iCallback == 512) OnLobbyMatchList(Marshal.PtrToStructure<LobbyMatchList_t>(msg.m_pubParam));
                        else if (msg.m_iCallback == 1220) OnConnectionStatusChanged(Marshal.PtrToStructure<SteamNetConnectionStatusChanged_t>(msg.m_pubParam));

                        SteamAPI_ManualDispatch_FreeLastCallback(_hSteamServerPipe);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(serverCallbackMsg);
                }
            }

            lock (_receivedMessages)
            {
                foreach (var msg in _receivedMessages)
                    _eventBus.ProcessNetworkMessage(msg);
                _receivedMessages.Clear();
            }
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChanged_t result)
        {
            long conn = result.m_hConn;
            int newState = result.m_info.m_eState;

            Console.WriteLine($"[SteamEngine] Connection status changed - Handle: {conn}, New State: {newState}");

            if (newState == 2)
            {
                int acceptResult = SteamAPI_ISteamNetworkingSockets_AcceptConnection(_networking, (int)conn);
                Console.WriteLine($"SteamEngine: Accepted incoming connection {conn} (result: {acceptResult})");
            }
            else if (newState == 3)
            {
                _connectionReady[conn] = true;
                Console.WriteLine($"CONNECTION {conn} is now READY");
            }
            else if (newState >= 4)
            {
                _connectionReady[conn] = false;
            }
        }

        public void RequestDedicatedLobbies()
        {
            Console.WriteLine("Client: Searching for dedicated lobbies (dedicated=true)...");
            SteamAPI_ISteamMatchmaking_AddRequestLobbyListStringFilter(_matchmaking, "dedicated", "true", 0);
            SteamAPI_ISteamMatchmaking_RequestLobbyList(_matchmaking);
        }

        public void JoinSpecificLobby(ulong lobbyId)
        {
            Console.WriteLine($"Client: Joining specific dedicated lobby ID {lobbyId}");
            JoinLobby(lobbyId);
        }

        public nint GetSteamPipe() => _hSteamPipe;

        public ulong GetSteamId()
        {
            return _user != nint.Zero ? SteamAPI_ISteamUser_GetSteamID(_user) : 0;
        }

        public ulong GetLobbyOwner(ulong lobbyId)
        {
            if (_matchmaking == nint.Zero) return 0;
            ulong owner = SteamAPI_ISteamMatchmaking_GetLobbyOwner(_matchmaking, lobbyId);
            Console.WriteLine($"SteamEngine: Lobby {lobbyId} owner SteamID: {owner}");
            return owner;
        }

        public void CreateLobby(int maxPlayers)
        {
            if (_lobbyCreated) return;
            SteamAPI_ISteamMatchmaking_CreateLobby(_matchmaking, 1, maxPlayers);
            Console.WriteLine($"Creating lobby with max players: {maxPlayers}");
        }

        public void JoinLobby(ulong lobbyId)
        {
            SteamAPI_ISteamMatchmaking_JoinLobby(_matchmaking, (long)lobbyId);
            Console.WriteLine($"Joining lobby: {lobbyId}");
        }

        public bool IsLobbyCreated() => _lobbyCreated;
        public bool IsLobbyJoined() => _lobbyJoined;
        public ulong GetLobbyId() => _lobbyId;

        public bool InitializeServer(uint ip, ushort port, string serverName)
        {
            _isDedicatedServer = true;

            if (!SteamGameServer_Init(ip, port, (ushort)(port + 1), 0, 480, "1.0.0"))
            {
                Console.WriteLine("Failed to initialize Steam Game Server.");
                return false;
            }
            _hSteamServerPipe = SteamGameServer_GetHSteamPipe();
            if (_hSteamServerPipe == nint.Zero)
            {
                Console.WriteLine("Failed to get server HSteamPipe.");
                return false;
            }
            Console.WriteLine($"Server HSteamPipe acquired: {_hSteamServerPipe}");

            _gameServer = SteamAPI_SteamGameServer_v015();
            if (_gameServer == nint.Zero)
            {
                Console.WriteLine("Failed to get ISteamGameServer.");
                return false;
            }
            Console.WriteLine("ISteamGameServer interface acquired.");

            SteamAPI_ISteamGameServer_SetServerName(_gameServer, serverName);
            SteamAPI_ISteamGameServer_LogOnAnonymous(_gameServer);
            Console.WriteLine($"Server started: {serverName} on port {port}");

            return true;
        }

        public void ShutdownServer()
        {
            if (_hSteamServerPipe != nint.Zero)
            {
                SteamGameServer_Shutdown();
                Console.WriteLine("Steam Game Server shutdown complete.");
                _hSteamServerPipe = nint.Zero;
            }
        }

        public bool StartVoiceRecording()
        {
            SteamAPI_ISteamUser_StartVoiceRecording(_user);
            Console.WriteLine("Started voice recording.");
            return true;
        }

        public bool StopVoiceRecording()
        {
            SteamAPI_ISteamUser_StopVoiceRecording(_user);
            Console.WriteLine("Stopped voice recording.");
            return true;
        }

        public byte[] GetVoiceData()
        {
            uint bytesAvailable = 0;
            uint bytesWritten = 0;
            SteamAPI_ISteamUser_GetAvailableVoice(_user, ref bytesAvailable, nint.Zero);
            if (bytesAvailable == 0) return null;

            byte[] buffer = new byte[bytesAvailable];
            nint compressed = Marshal.AllocHGlobal((int)bytesAvailable);
            try
            {
                uint result = SteamAPI_ISteamUser_GetVoice(_user, true, compressed, bytesAvailable, ref bytesWritten, false, nint.Zero, 0, nint.Zero, 0);
                if (result == 0 && bytesWritten > 0)
                {
                    Marshal.Copy(compressed, buffer, 0, (int)bytesWritten);
                    Console.WriteLine($"Voice data retrieved: {bytesWritten} bytes.");
                    return buffer;
                }
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(compressed);
            }
        }

        public void ConnectP2P(ulong steamId)
        {
            ulong localSteamId = GetSteamId();
            if (steamId == localSteamId)
            {
                Console.WriteLine("SteamEngine: Skipping P2P to self (localhost testing)");
                return;
            }

            Console.WriteLine($"Client: Connecting P2P to lobby owner SteamID {steamId}");
            SteamNetworkingIdentity identity = new SteamNetworkingIdentity
            {
                m_eType = 1,
                m_steamID64 = steamId
            };

            nint connection = SteamAPI_ISteamNetworkingSockets_ConnectP2P(_networking, ref identity, 0, nint.Zero, 0);
            long handle = (long)connection;
            _connectionHandles[steamId] = handle;
            _connectionReady[handle] = false;
            Console.WriteLine($"P2P connection initiated to SteamID: {steamId}, Handle: {handle}");
        }

        public void SendP2PMessage(byte[] data)
        {
            if (data == null || data.Length == 0 || _connectionHandles.Count == 0) return;

            nint messagePtr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, messagePtr, data.Length);
                foreach (var kvp in _connectionHandles)
                {
                    long conn = kvp.Value;
                    if (!_connectionReady.ContainsKey(conn) || !_connectionReady[conn])
                    {
                        Console.WriteLine($"SteamEngine: Skipping send to connection {conn} - not READY yet");
                        continue;
                    }

                    uint result = SteamAPI_ISteamNetworkingSockets_SendMessageToConnection(_networking, (int)conn, messagePtr, (uint)data.Length, 0, out long _);
                    if (result == 0)
                    {
                        Console.WriteLine($"SteamEngine: Sent {data.Length} bytes successfully to connection {conn}");
                    }
                    else
                    {
                        Console.WriteLine($"SteamEngine: Send failed to connection {conn} (error {result})");
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
            }
        }

        public void CreateWorkshopItem(string title, string description, string contentPath)
        {
            nint call = SteamAPI_ISteamUGC_CreateItem(_ugc, 480, 0);
            Console.WriteLine($"Creating Workshop item: {title}");
        }

        public ulong[] GetSubscribedWorkshopItems()
        {
            uint numItems = SteamAPI_ISteamUGC_GetNumSubscribedItems(_ugc);
            ulong[] items = new ulong[numItems];
            uint returned = SteamAPI_ISteamUGC_GetSubscribedItems(_ugc, items, numItems);
            Console.WriteLine($"Found {returned} subscribed Workshop items.");
            return items.Length > 0 ? items : Array.Empty<ulong>();
        }

        public string GetWorkshopItemInstallInfo(ulong itemId)
        {
            uint size = 1024;
            StringBuilder folder = new StringBuilder((int)size);
            ulong sizeOnDisk;
            uint timeStamp;
            if (SteamAPI_ISteamUGC_GetItemInstallInfo(_ugc, itemId, out sizeOnDisk, folder, size, out timeStamp))
            {
                string path = folder.ToString();
                Console.WriteLine($"Workshop item {itemId} installed at: {path}, Size: {sizeOnDisk}, Timestamp: {timeStamp}");
                return path;
            }
            Console.WriteLine($"Failed to get install info for Workshop item {itemId}");
            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ShutdownServer();
                SteamAPI_Shutdown();
                Console.WriteLine("SteamAPI shutdown complete.");
                _disposed = true;
            }
        }

        private void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != 1)
            {
                if (!_isDedicatedServer)
                {
                    Console.WriteLine($"[CLIENT] Ignored stray LobbyCreated callback (result {result.m_eResult})");
                }
                else
                {
                    Console.WriteLine($"[SERVER] Lobby creation callback received (ignored on dedicated server)");
                }
                return;
            }

            _lobbyCreated = true;
            _lobbyId = result.m_ulSteamIDLobby;

            SteamAPI_ISteamMatchmaking_SetLobbyData(_matchmaking, _lobbyId, "dedicated", "true");
            SteamAPI_ISteamMatchmaking_SetLobbyData(_matchmaking, _lobbyId, "port", "27015");
            SteamAPI_ISteamMatchmaking_SetLobbyData(_matchmaking, _lobbyId, "serverName", "Citadel Dedicated Server");
            SteamAPI_ISteamMatchmaking_SetLobbyData(_matchmaking, _lobbyId, "modVersion", "1.0.0");

            Console.WriteLine($"=== LOBBY CREATED (client-side) ===");
            Console.WriteLine($"Lobby ID: {_lobbyId}");
            Console.WriteLine($"=======================================");

            _eventBus.Publish(new LobbyCreatedEvent(_lobbyId), true);
        }

        private void OnLobbyEnter(LobbyEnter_t result)
        {
            if (_isDedicatedServer)
            {
                Console.WriteLine("[SERVER] Ignoring LobbyEnter callback - server does not join lobbies");
                return;
            }

            if (result.m_EChatRoomEnterResponse != 1)
            {
                Console.WriteLine($"Failed to join lobby: {result.m_EChatRoomEnterResponse}");
                return;
            }
            _lobbyJoined = true;
            ulong joinedLobbyId = result.m_ulSteamIDLobby;
            Console.WriteLine($"Successfully joined lobby: {joinedLobbyId}");

            ulong ownerSteamId = GetLobbyOwner(joinedLobbyId);
            if (ownerSteamId != 0)
            {
                ConnectP2P(ownerSteamId);
            }

            _eventBus.Publish(new LobbyJoinedEvent(joinedLobbyId), true);
        }

        private void OnLobbyMatchList(LobbyMatchList_t result)
        {
            Console.WriteLine($"Client: Found {result.m_nLobbiesMatching} dedicated lobbies");

            if (result.m_nLobbiesMatching > 0)
            {
                ulong firstLobby = SteamAPI_ISteamMatchmaking_GetLobbyByIndex(_matchmaking, 0);
                Console.WriteLine($"Client: Auto-joining first dedicated lobby {firstLobby}");
                JoinLobby(firstLobby);
            }
            else
            {
                Console.WriteLine("Client: No dedicated lobbies found. Creating one for testing...");
                CreateLobby(64);
            }
        }

        private void OnWorkshopItemCreated(SteamUGCRequestUGCDetailsResult_t result)
        {
            if (result.m_eResult != 1)
            {
                Console.WriteLine($"Workshop item creation failed: {result.m_eResult}");
                return;
            }
            Console.WriteLine($"Workshop item created: ID {result.m_nPublishedFileId}, Title: {result.m_pchTitle}");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CallbackMsg_t
        {
            public int m_hSteamUser;
            public int m_iCallback;
            public nint m_pubParam;
            public int m_cubParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LobbyCreated_t
        {
            public uint m_eResult;
            public ulong m_ulSteamIDLobby;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LobbyEnter_t
        {
            public ulong m_ulSteamIDLobby;
            public uint m_rgfChatPermissions;
            public bool m_bLocked;
            public uint m_EChatRoomEnterResponse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LobbyMatchList_t
        {
            public uint m_nLobbiesMatching;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SteamNetworkingIdentity
        {
            public int m_eType;
            public ulong m_steamID64;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 152)]
            public byte[] m_padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SteamNetConnectionStatusChanged_t
        {
            public long m_hConn;
            public SteamNetConnectionInfo_t m_info;
            public int m_eOldState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SteamNetConnectionInfo_t
        {
            public int m_eState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SteamUGCRequestUGCDetailsResult_t
        {
            public uint m_eResult;
            public ulong m_nPublishedFileId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string m_pchTitle;
        }

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamNetworkingSockets_AcceptConnection")]
        private static extern int SteamAPI_ISteamNetworkingSockets_AcceptConnection(nint instance, int hConn);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyOwner")]
        private static extern ulong SteamAPI_ISteamMatchmaking_GetLobbyOwner(nint instance, ulong steamIDLobby);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_AddRequestLobbyListStringFilter")]
        private static extern void SteamAPI_ISteamMatchmaking_AddRequestLobbyListStringFilter(nint instance, string pchKeyToMatch, string pchValueToMatch, int eComparisonType);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_RequestLobbyList")]
        private static extern nint SteamAPI_ISteamMatchmaking_RequestLobbyList(nint instance);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyByIndex")]
        private static extern ulong SteamAPI_ISteamMatchmaking_GetLobbyByIndex(nint instance, int iLobby);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_InitSafe")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_InitSafe();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ManualDispatch_Init")]
        private static extern void SteamAPI_ManualDispatch_Init();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_GetHSteamPipe")]
        private static extern nint SteamAPI_GetHSteamPipe();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_SteamMatchmaking_v009")]
        private static extern nint SteamAPI_SteamMatchmaking_v009();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_CreateLobby")]
        private static extern nint SteamAPI_ISteamMatchmaking_CreateLobby(nint instance, int eLobbyType, int cMaxMembers);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_JoinLobby")]
        private static extern nint SteamAPI_ISteamMatchmaking_JoinLobby(nint instance, long steamIDLobby);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyData")]
        private static extern bool SteamAPI_ISteamMatchmaking_GetLobbyData(nint instance, ulong steamIDLobby, string pchKey, StringBuilder pchValue, uint cchValueMax);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_SetLobbyData")]
        private static extern bool SteamAPI_ISteamMatchmaking_SetLobbyData(nint instance, ulong steamIDLobby, string pchKey, string pchValue);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ManualDispatch_RunFrame")]
        private static extern void SteamAPI_ManualDispatch_RunFrame(nint hSteamPipe);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ManualDispatch_GetNextCallback")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ManualDispatch_GetNextCallback(nint hSteamPipe, nint pCallbackMsg);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ManualDispatch_FreeLastCallback")]
        private static extern void SteamAPI_ManualDispatch_FreeLastCallback(nint hSteamPipe);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_Shutdown")]
        private static extern void SteamAPI_Shutdown();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_IsSteamRunning")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_IsSteamRunning();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_SteamUser_v023")]
        private static extern nint SteamAPI_SteamUser_v023();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUser_GetSteamID")]
        private static extern ulong SteamAPI_ISteamUser_GetSteamID(nint instance);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_SteamFriends_v018")]
        private static extern nint SteamAPI_SteamFriends_v018();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamFriends_GetPersonaName")]
        private static extern bool SteamAPI_ISteamFriends_GetPersonaName(nint instance, StringBuilder pchPersonaName, uint cchPersonaName);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamGameServer_InitSafe")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamGameServer_Init(uint unIP, ushort usSteamPort, ushort usGamePort, uint unFlags, int nGameAppId, string pchVersionString);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamGameServer_GetHSteamPipe")]
        private static extern nint SteamGameServer_GetHSteamPipe();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_SteamGameServer_v015")]
        private static extern nint SteamAPI_SteamGameServer_v015();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamGameServer_SetServerName")]
        private static extern void SteamAPI_ISteamGameServer_SetServerName(nint instance, string serverName);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamGameServer_LogOnAnonymous")]
        private static extern void SteamAPI_ISteamGameServer_LogOnAnonymous(nint instance);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamGameServer_Shutdown")]
        private static extern void SteamGameServer_Shutdown();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamGameServer_RunCallbacks")]
        private static extern void SteamGameServer_RunCallbacks();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUser_StartVoiceRecording")]
        private static extern void SteamAPI_ISteamUser_StartVoiceRecording(nint instance);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUser_StopVoiceRecording")]
        private static extern void SteamAPI_ISteamUser_StopVoiceRecording(nint instance);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUser_GetAvailableVoice")]
        private static extern uint SteamAPI_ISteamUser_GetAvailableVoice(nint instance, ref uint pcbCompressed, nint pcbUncompressed);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUser_GetVoice")]
        private static extern uint SteamAPI_ISteamUser_GetVoice(nint instance, bool bWantCompressed, nint pDestBuffer, uint cbDestBufferSize, ref uint nBytesWritten, bool bWantUncompressed, nint pUncompressedDestBuffer, uint cbUncompressedDestBufferSize, nint nUncompressedBytesWritten, uint nUncompressedVoiceDesiredSampleRate);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_SteamNetworkingSockets_SteamAPI_v012")]
        private static extern nint SteamAPI_SteamNetworkingSockets_SteamAPI_v012();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamNetworkingSockets_ConnectP2P")]
        private static extern nint SteamAPI_ISteamNetworkingSockets_ConnectP2P(nint instance, ref SteamNetworkingIdentity identityRemote, int nVirtualPort, nint pConnectionOptions, int nOptions);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamNetworkingSockets_SendMessageToConnection")]
        private static extern uint SteamAPI_ISteamNetworkingSockets_SendMessageToConnection(nint instance, int hConn, nint pData, uint cbData, int nSendFlags, out long pOutMessageNumber);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_SteamUGC_v021")]
        private static extern nint SteamAPI_SteamUGC_v021();

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUGC_CreateItem")]
        private static extern nint SteamAPI_ISteamUGC_CreateItem(nint instance, int nConsumerAppId, int eFileType);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUGC_GetNumSubscribedItems")]
        private static extern uint SteamAPI_ISteamUGC_GetNumSubscribedItems(nint instance);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUGC_GetSubscribedItems")]
        private static extern uint SteamAPI_ISteamUGC_GetSubscribedItems(nint instance, ulong[] pvecPublishedFileID, uint cMaxEntries);

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamUGC_GetItemInstallInfo")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ISteamUGC_GetItemInstallInfo(nint instance, ulong nPublishedFileID, out ulong punSizeOnDisk, StringBuilder pchFolder, uint cchFolderSize, out uint punTimeStamp);
    }
}