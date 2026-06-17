// Folder: Trebuchet
// File: Launcher.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.UI;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Trebuchet
{
    public class Launcher
    {
        private bool _isRunning;
        private IntPtr _window;
        private UISettingsManager _settingsManager;
        private ISteamEngine _steamEngine;
        private EventBus _eventBus;
        private MenuPanel _menuPanel;
        private IRenderContext _renderContext;
        private IControlContext _controlContext;
        private InputHandler _inputHandler;
        private ModManager _modManager;
        private ContextManager _contextManager;
        private Process _serverProcess;
        private SceneManager _sceneManager;
        private PanelManager _panelManager;

        public void Start(string context, bool discoverDedicated = false, ulong specificLobbyId = 0, ulong connectToServerSteamId = 0, bool discoverP2PHost = false, ulong joinLobbyId = 0, bool isClientRuntime = false, string playProjectPath = null, string loadLevelName = "Main")
        {
            try
            {
                string dllPath = Path.Combine(Directory.GetCurrentDirectory(), "steam_api64.dll");
                IntPtr dllHandle = LoadLibrary(dllPath);
                if (dllHandle == IntPtr.Zero)
                {
                    Console.WriteLine($"Failed to load steam_api64.dll. Error code: {Marshal.GetLastWin32Error()}");
                    return;
                }

                if (isClientRuntime || !string.IsNullOrEmpty(playProjectPath))
                {
                    Console.WriteLine($"[Launcher.PureClient] ACTIVATED - level '{loadLevelName}' - NO server spawn, NO IDE panels, NO Steam server, robust 1280x720 window");
                    _settingsManager = new UISettingsManager();
                    _settingsManager.UpdateWindowSize(1280, 720);
                    _settingsManager.LoadSettings(); // safe call (defaults applied)
                    _panelManager = null; // strict isolation - no panels
                    _menuPanel = null;
                }
                else if (!discoverDedicated && connectToServerSteamId == 0 && !discoverP2PHost)
                {
                    _serverProcess = Process.Start("Citadel.exe", "--local");
                    Console.WriteLine("Launcher: Started local authoritative Citadel server (--local) for validation layer.");
                }
                else if (discoverDedicated)
                {
                    Console.WriteLine("Launcher: DISCOVER MODE — searching for or creating dedicated lobbies...");
                }
                else if (connectToServerSteamId != 0)
                {
                    Console.WriteLine($"Launcher: DIRECT CONNECT MODE — connecting to dedicated server {connectToServerSteamId}");
                }
                else if (discoverP2PHost)
                {
                    Console.WriteLine("Launcher: P2P HOST DISCOVER MODE — searching for P2P authoritative host lobbies...");
                }

                using (_steamEngine = new SteamEngine())
                {
                    _eventBus = new EventBus((SteamEngine)_steamEngine);
                    if (!_steamEngine.Initialize())
                    {
                        Console.WriteLine("Launcher: SteamEngine initialization failed.");
                        return;
                    }

                    if (joinLobbyId != 0)
                    {
                        ((SteamEngine)_steamEngine).JoinSpecificLobby(joinLobbyId);
                    }
                    else if (connectToServerSteamId != 0)
                    {
                        ((SteamEngine)_steamEngine).ConnectToDedicatedServer(connectToServerSteamId);
                    }
                    else if (discoverP2PHost)
                    {
                        ((SteamEngine)_steamEngine).RequestP2PHostLobbies();
                    }
                    else if (discoverDedicated)
                    {
                        if (specificLobbyId != 0)
                        {
                            ((SteamEngine)_steamEngine).JoinSpecificLobby(specificLobbyId);
                        }
                        else
                        {
                            ((SteamEngine)_steamEngine).CreateLobby(64);
                        }
                    }

                    _settingsManager = _settingsManager ?? new UISettingsManager();
                    _settingsManager.LoadSettings();
                    if (_settingsManager.WindowWidth == 0 || _settingsManager.WindowHeight == 0)
                    {
                        _settingsManager.UpdateWindowSize(1280, 720);
                    }
                    if (context == "OpenGL")
                    {
                        _contextManager = new OpenGLContextManager();
                    }
                    _contextManager.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight, isClientRuntime ? "SiegeEngine Runtime - Main" : "Citadel Launcher");
                    _window = _contextManager.Window;
                    _renderContext = _contextManager.RenderContext;
                    _controlContext = _contextManager.ControlContext;
                    _modManager = new ModManager(null, _steamEngine);
                    _inputHandler = new InputHandler(_controlContext, _window, (SteamEngine)_steamEngine);
                    _inputHandler.SetMouseCallback("ui", (button, action) => { });
                    _inputHandler.SetKeyCallback("ui", (key, action) => { });
                    string initialHtmlPath = _modManager.GetMenuConfigPath();
                    Console.WriteLine($"Launcher: Resolved MainMenu.html path: {initialHtmlPath}, Exists: {File.Exists(initialHtmlPath)}");

                    if (isClientRuntime || !string.IsNullOrEmpty(playProjectPath))
                    {
                        _sceneManager = new SceneManager(_eventBus, _renderContext, _controlContext, _window, _modManager, _settingsManager, _steamEngine, _inputHandler, null);
                        // panelManager skipped entirely in pure runtime
                        _sceneManager.SwitchToRuntimeGameplay(playProjectPath, loadLevelName);
                        Console.WriteLine("[Launcher] Pure client runtime - IDE panels skipped, Gameplay scene loaded from passed Level name");
                    }
                    else
                    {
                        _menuPanel = new MenuPanel(_renderContext, _controlContext, _window, _eventBus, _modManager, initialHtmlPath);
                        _menuPanel.DockState = DockState.Tabbed;
                        _menuPanel.Init();
                        _eventBus.RegisterNamespace("CastleBuilder.Events");

                        _sceneManager = new SceneManager(_eventBus, _renderContext, _controlContext, _window, _modManager, _settingsManager, _steamEngine, _inputHandler, _menuPanel);
                        _panelManager = new PanelManager(_renderContext, _controlContext, _window, _eventBus);
                        _panelManager.AddPanel(_menuPanel);
                    }

                    _controlContext.SetWindowSizeCallback(_window, (w, width, height) =>
                    {
                        if (!isClientRuntime) // respect core separation - no spam in pure client
                        {
                            _settingsManager.UpdateWindowSize(width, height);
                            Console.WriteLine($"Launcher: Window resized to: {width}x{height}");
                        }
                        _sceneManager.Resize(width, height);
                    });
                    _isRunning = true;
                    float lastFrameTime = 0f;
                    while (_isRunning)
                    {
                        float currentTime = (float)_controlContext.GetTime();
                        float deltaTime = currentTime - lastFrameTime;
                        lastFrameTime = currentTime;
                        _steamEngine.RunCallbacks();
                        _controlContext.PollEvents();
                        if (_controlContext.WindowShouldClose(_window))
                            _isRunning = false;
                        _renderContext.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
                        _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
                        _renderContext.Disable(_renderContext.Enums.DepthTest);
                        _sceneManager.Update(deltaTime);
                        if (_panelManager != null) _panelManager?.Update(deltaTime);
                        _sceneManager.Render();
                        if (_panelManager != null) _panelManager?.Render();
                        _renderContext.Enable(_renderContext.Enums.DepthTest);
                        _controlContext.SwapBuffers(_window);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex}");
            }
            finally
            {
                _sceneManager?.Dispose();
                _serverProcess?.Kill();
                _settingsManager?.SaveSettings();
                _contextManager?.Terminate();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}