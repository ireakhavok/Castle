// Trebuchet/Launcher.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Numerics;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SiegeEngine.Managers;
using SiegeEngine.Networking;
using SiegeEngine.Rendering;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Scenes;
using SiegeEngine.Definitions;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;

namespace Trebuchet
{
    public unsafe class Launcher
    {
        private bool _isRunning;
        private Glfw _glfw;
        private GL _gl;
        private WindowHandle* _window;
        private UISettingsManager _settingsManager;
        private ISteamEngine _steamEngine;
        private EventBus _eventBus;
        //private MenuManager _menuManager;
        private IRenderContext _renderContext;
        //private CustomUIController _uiController;
        private InputHandler _inputHandler;
        private ModManager _modManager;

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

                    _glfw = Glfw.GetApi();
                    if (!_glfw.Init())
                    {
                        Console.WriteLine("Failed to initialize GLFW");
                        return;
                    }

                    _settingsManager = new UISettingsManager();
                    _settingsManager.LoadSettings();
                    if (_settingsManager.WindowWidth == 0 || _settingsManager.WindowHeight == 0)
                        _settingsManager.UpdateWindowSize(1280, 720, false);

                    _glfw.WindowHint(WindowHintBool.Resizable, true);
                    _glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
                    _glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
                    _glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

                    _window = _glfw.CreateWindow(_settingsManager.WindowWidth, _settingsManager.WindowHeight, "Citadel Launcher", null, null);
                    if (_window == null)
                    {
                        Console.WriteLine("Failed to create GLFW window");
                        _glfw.Terminate();
                        return;
                    }

                    _glfw.MakeContextCurrent(_window);
                    _gl = GL.GetApi(_glfw.GetProcAddress);

                    _modManager = new ModManager(null, _steamEngine);
                    _renderContext = new OpenGLRenderContext(_glfw, _gl);
                    _inputHandler = new InputHandler(_glfw, _window, (SteamEngine)_steamEngine);
                    _inputHandler.SetMouseCallback("ui", (button, action) => { });
                    _inputHandler.SetKeyCallback("ui", (key, action) => { });

                    string configPath = _modManager.GetMenuConfigPath();
                    Console.WriteLine($"Launcher: Resolved MainMenu.json path: {configPath}, Exists: {File.Exists(configPath)}");

                    //_menuManager = new MenuManager(_settingsManager, _modManager, null, _glfw, _window, null, null, configPath);
                    //_uiController = new CustomUIController(_glfw, _renderContext, _window, _settingsManager, _menuManager, null, _inputHandler);
                    //_uiController.Initialize();

                    //_menuManager.SwitchMenu("MainMenu");

                    //_menuManager.OnSettingsSelected += () =>
                    //{
                    //    Console.WriteLine("Launcher: Settings selected, switching to UserSettingsMenu");
                    //    _menuManager.SwitchMenu("UserSettingsMenu");
                    //};

                    _glfw.SetWindowSizeCallback(_window, (w, width, height) =>
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
                        _glfw.PollEvents();

                        if (_glfw.WindowShouldClose(_window))
                            _isRunning = false;

                        //_uiController.Update(deltaTime);
                        //_uiController.Render();

                        _glfw.SwapBuffers(_window);
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
                _glfw?.Terminate();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}