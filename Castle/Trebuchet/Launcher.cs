using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Managers;
using SiegeEngine.Networking;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Rendering;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Reflection;
namespace Trebuchet
{
    public class Launcher
    {
        private bool _isRunning;
        private IntPtr _window;
        private UISettingsManager _settingsManager;
        private ISteamEngine _steamEngine;
        private EventBus _eventBus;
        private MenuSystem _menuSystem;
        private Scene _scene;
        private IRenderContext _renderContext;
        private IControlContext _controlContext;
        private InputHandler _inputHandler;
        private ModManager _modManager;
        private ContextManager _contextManager;
        private NetworkManager _networkManager;
        private GameServer _server;
        private ModelManager _modelManager;
        private Player _player;
        private PlayerMovement _playerMovement;
        private enum Mode { Menu, Scene }
        private Mode _mode = Mode.Menu;
        public void Start(string context)
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
                using (_steamEngine = new SteamEngine())
                {
                    _eventBus = new EventBus((SteamEngine)_steamEngine);
                    if (!_steamEngine.Initialize())
                    {
                        Console.WriteLine("Launcher: SteamEngine initialization failed.");
                        return;
                    }
                    _eventBus.Subscribe<SwitchSceneEvent>(OnSwitchScene);
                    _settingsManager = new UISettingsManager();
                    _settingsManager.LoadSettings();
                    if (_settingsManager.WindowWidth == 0 || _settingsManager.WindowHeight == 0)
                        _settingsManager.UpdateWindowSize(1920, 1080, false);
                    if (context == "OpenGL")
                    {
                        _contextManager = new OpenGLContextManager();
                    }
                    _contextManager.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight, "Citadel Launcher");
                    _window = _contextManager.Window;
                    _renderContext = _contextManager.RenderContext;
                    _controlContext = _contextManager.ControlContext;
                    _modManager = new ModManager(null, _steamEngine);
                    _inputHandler = new InputHandler(_controlContext, _window, (SteamEngine)_steamEngine);
                    _inputHandler.SetMouseCallback("ui", (button, action) => { });
                    _inputHandler.SetKeyCallback("ui", (key, action) => { });
                    string configPath = _modManager.GetMenuConfigPath();
                    Console.WriteLine($"Launcher: Resolved MainMenu.json path: {configPath}, Exists: {File.Exists(configPath)}");
                    _menuSystem = new MenuSystem(_settingsManager, _modManager, _eventBus, _controlContext, _window, _renderContext, configPath);
                    _menuSystem.Initialize();
                    _controlContext.SetWindowSizeCallback(_window, (w, width, height) =>
                    {
                        if (_settingsManager.AllowResize)
                        {
                            _settingsManager.UpdateWindowSize(width, height);
                            Console.WriteLine($"Launcher: Window resized to: {width}x{height}");
                        }
                        else
                        {
                            Console.WriteLine($"Launcher: Window resize to {width}x{height} blocked, allowResize is false");
                        }
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
                        if (_mode == Mode.Menu)
                        {
                            _menuSystem.Update(deltaTime);
                            _menuSystem.Render();
                        }
                        else if (_mode == Mode.Scene)
                        {
                            _scene.Update(deltaTime);
                            _scene.Render(_server.GetEntities());
                        }
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
                _settingsManager?.SaveSettings();
                _contextManager?.Terminate();
            }
        }
        private void OnSwitchScene(SwitchSceneEvent e)
        {
            _menuSystem = null;
            var parts = e.Hook.Split('.');
            if (parts.Length < 3)
            {
                Console.WriteLine($"Launcher: Invalid hook format: {e.Hook}");
                return;
            }
            string dllName = parts[0];
            string ns = string.Join(".", parts.Take(parts.Length - 2));
            string className = parts[parts.Length - 2];
            string methodName = parts[parts.Length - 1].TrimEnd('(', ')');
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{dllName}.dll");
            if (!File.Exists(dllPath))
            {
                dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", $"{dllName}.dll");
            }
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFile(dllPath);
            }
            catch
            {
                assembly = Assembly.GetExecutingAssembly();
                ns = "SiegeEngine.Scenes";
                className = "SandboxScene";
                methodName = "SandboxScene";
            }
            string fullTypeName = $"{ns}.{className}";
            Type sceneType = assembly.GetType(fullTypeName);
            if (sceneType == null || !typeof(Scene).IsAssignableFrom(sceneType))
            {
                Console.WriteLine($"Launcher: Invalid scene type: {fullTypeName}");
                return;
            }
            _modelManager = new ModelManager(null, _renderContext);
            _modelManager.LoadCharacters();
            _networkManager = new NetworkManager((SteamEngine)_steamEngine, _eventBus);
            _networkManager.Start();
            _server = new GameServer(_eventBus, _networkManager);
            _player = new Player(1, new Vector3(0, 0, 0), _steamEngine.GetSteamId(), _modelManager);
            _player.InitializeCamera(_controlContext, _window);
            _playerMovement = new PlayerMovement();
            object[] args = new object[] { _renderContext, _controlContext, _window, _player, _server, _playerMovement, _eventBus, _modelManager };
            if (methodName == className || methodName == "")
            {
                _scene = (Scene)Activator.CreateInstance(sceneType, args);
            }
            else
            {
                MethodInfo method = sceneType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
                if (method != null && typeof(Scene).IsAssignableFrom(method.ReturnType))
                {
                    _scene = (Scene)method.Invoke(null, args);
                }
                else
                {
                    Console.WriteLine($"Launcher: Invalid factory method: {methodName}");
                    return;
                }
            }
            _scene.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight);
            _mode = Mode.Scene;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}