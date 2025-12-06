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
using SiegeEngine.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Reflection;
using CastleBuilder;

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
        private BlueprintManager _blueprintManager;

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
                _serverProcess = Process.Start("Citadel.exe", "--local");
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
                    {
                        _settingsManager.UpdateWindowSize(1920, 1080, false);
                    }
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
                    string initialHtmlPath = _modManager.GetMenuConfigPath();
                    Console.WriteLine($"Launcher: Resolved MainMenu.html path: {initialHtmlPath}, Exists: {File.Exists(initialHtmlPath)}");
                    _menuPanel = new MenuPanel(_renderContext, _controlContext, _window, _eventBus, _modManager, initialHtmlPath, _inputHandler);
                    _menuPanel.Init();
                    _eventBus.RegisterNamespace("CastleBuilder.Events");
                    _blueprintManager = new BlueprintManager(_eventBus);
                    _sceneManager = new SceneManager(_eventBus, _renderContext, _controlContext, _window, _modManager, _settingsManager, _steamEngine, _inputHandler, _menuPanel);
                    _panelManager = new PanelManager(_renderContext, _controlContext, _window, _eventBus);
                    _panelManager.AddPanel(_menuPanel);
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
                        _panelManager.Update(deltaTime);
                        _sceneManager.Render();
                        _panelManager.Render();
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