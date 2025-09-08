// Trebuchet/Launcher.cs
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
using Silk.NET.GLFW;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
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
        //private MenuSystem _menuSystem;
        private IRenderContext _renderContext;
        private IControlContext _controlContext;
        //private CustomUIController _uiController;
        private InputHandler _inputHandler;
        private ModManager _modManager;
        private ContextManager _contextManager;

        public void Start()
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
                    _settingsManager = new UISettingsManager();
                    _settingsManager.LoadSettings();
                    if (_settingsManager.WindowWidth == 0 || _settingsManager.WindowHeight == 0)
                        _settingsManager.UpdateWindowSize(1280, 720, false);
                    _contextManager = new ContextManager();
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
                    //_menuSystem = new MenuSystem(_settingsManager, _modManager, null, _glfw, _window, null, null, configPath);
                    //_uiController = new CustomUIController(_glfw, _renderContext, _window, _settingsManager, _menuSystem, null, _inputHandler);
                    //_uiController.Initialize();
                    //_menuSystem.SwitchMenu("MainMenu");
                    //_menuSystem.OnSettingsSelected += () =>
                    //{
                    // Console.WriteLine("Launcher: Settings selected, switching to UserSettingsMenu");
                    // _menuSystem.SwitchMenu("UserSettingsMenu");
                    //};
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
                        float currentTime = (float)_glfw.GetTime();
                        float deltaTime = currentTime - lastFrameTime;
                        lastFrameTime = currentTime;
                        _steamEngine.RunCallbacks();
                        _controlContext.PollEvents();
                        if (_controlContext.WindowShouldClose(_window))
                            _isRunning = false;
                        //_uiController.Update(deltaTime);
                        //_uiController.Render();
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
                //_uiController?.Dispose();
                _contextManager?.Terminate();
            }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}