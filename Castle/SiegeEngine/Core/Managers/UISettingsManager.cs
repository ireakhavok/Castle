// Folder: SiegeEngine/Core/Managers
// File: UISettingsManager.cs
using SiegeEngine.Core.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SiegeEngine.Core.Managers
{
    public class UISettingsManager
    {
        private readonly string _settingsPath;
        private int _windowWidth;
        private int _windowHeight;
        private bool _isFullscreen;
        private Dictionary<string, int> _iconIndices;
        private bool _allowResize;
        private string _currentRenderer;
        private List<string> _availableRenderers;
        private AntiAliasingMode _antiAliasingMode = AntiAliasingMode.SMAA;
        private bool _hasAntiAliasingOverride;

        public int WindowWidth => _windowWidth;
        public int WindowHeight => _windowHeight;
        public bool IsFullscreen => _isFullscreen;
        public Dictionary<string, int> IconIndices => _iconIndices;
        public bool AllowResize
        {
            get => _allowResize;
            set => _allowResize = value;
        }
        public string CurrentRenderer
        {
            get => _currentRenderer ?? "OpenGL";
            set => _currentRenderer = value;
        }
        public List<string> AvailableRenderers
        {
            get => _availableRenderers ?? new List<string> { "OpenGL" };
            set => _availableRenderers = value ?? new List<string> { "OpenGL" };
        }

        public AntiAliasingMode AntiAliasingMode => _antiAliasingMode;
        public bool HasAntiAliasingOverride => _hasAntiAliasingOverride;

        public UISettingsManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "GrokAIGame");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "settings.json");
            _windowWidth = 1280;
            _windowHeight = 720;
            _isFullscreen = false;
            _allowResize = true;
            _iconIndices = new Dictionary<string, int>
            {
                { "MMO Mode", 0 },
                { "Standalone Mode", 1 },
                { "Steam Matchmaking", 3 },
                { "Settings", 13 },
                { "Invite to Lobby", 15 },
                { "Exit", 23 }
            };
        }

        public void SetAntiAliasingMode(AntiAliasingMode mode, bool save = true)
        {
            _antiAliasingMode = mode;
            _hasAntiAliasingOverride = true;
            if (save)
                SaveSettings();
        }

        public void UpdateWindowSize(int width, int height, bool saveSettings = true)
        {
            if (width <= 0 || height <= 0)
            {
                Console.WriteLine($"UISettingsManager: Invalid resolution {width}x{height}, ignoring");
                return;
            }
            if (!_allowResize)
            {
                Console.WriteLine($"UISettingsManager: Resize blocked for {width}x{height}, allowResize is false");
                return;
            }
            _windowWidth = width;
            _windowHeight = height;
            string aspectRatio = GetAspectRatio(width, height);
            Console.WriteLine($"UISettingsManager: Updated window size to {_windowWidth}x{_windowHeight}, Aspect: {aspectRatio}");
            if (saveSettings)
            {
                SaveSettings();
            }
        }

        private string GetAspectRatio(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return "16:9";
            return (width, height) switch
            {
                (800, 600) or (1024, 768) or (1280, 960) or (1400, 1050) or (1600, 1200) => "4:3",
                (1280, 720) or (1366, 768) or (1600, 900) or (1920, 1080) or (2560, 1440) or (3840, 2160) => "16:9",
                (1280, 800) or (1440, 900) or (1680, 1050) or (1920, 1200) or (2560, 1600) => "16:10",
                (2560, 1080) or (3440, 1440) or (5120, 2160) => "21:9",
                (3840, 1080) or (5120, 1440) => "32:9",
                _ => "16:9"
            };
        }

        private int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        public void UpdateFullscreen(bool isFullscreen, bool saveSettings = true)
        {
            _isFullscreen = isFullscreen;
            Console.WriteLine($"UISettingsManager: Fullscreen set to {_isFullscreen}");
            if (saveSettings)
            {
                SaveSettings();
            }
        }

        public void UpdateIconIndices(Dictionary<string, int> newIndices)
        {
            _iconIndices = newIndices ?? new Dictionary<string, int>();
            Console.WriteLine($"UISettingsManager: Updated icon indices, count: {_iconIndices.Count}");
            SaveSettings();
        }

        public void LoadSettings()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsPath);
                    Console.WriteLine($"UISettingsManager: Reading settings.json: {json}");
                    var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (settings == null)
                    {
                        Console.WriteLine("UISettingsManager: Failed to deserialize settings.json, using defaults");
                        return;
                    }
                    if (settings.TryGetValue("WindowWidth", out var widthObj) && int.TryParse(widthObj.ToString(), out int width))
                        _windowWidth = width;
                    if (settings.TryGetValue("WindowHeight", out var heightObj) && int.TryParse(heightObj.ToString(), out int height))
                        _windowHeight = height;
                    if (settings.TryGetValue("IsFullscreen", out var fullscreenObj) && bool.TryParse(fullscreenObj.ToString(), out bool fullscreen))
                        _isFullscreen = fullscreen;
                    if (settings.TryGetValue("IconIndices", out var iconIndicesObj))
                    {
                        _iconIndices = JsonSerializer.Deserialize<Dictionary<string, int>>(iconIndicesObj.ToString()) ?? _iconIndices;
                    }
                    if (settings.TryGetValue("CurrentRenderer", out var rendererObj))
                    {
                        _currentRenderer = rendererObj?.ToString() ?? "OpenGL";
                    }
                    else
                    {
                        _currentRenderer = "OpenGL";
                    }
                    if (settings.TryGetValue("AvailableRenderers", out var availObj))
                    {
                        _availableRenderers = JsonSerializer.Deserialize<List<string>>(availObj.ToString()) ?? new List<string> { "OpenGL" };
                    }
                    else
                    {
                        _availableRenderers = new List<string> { "OpenGL" };
                    }
                    if (settings.TryGetValue("AntiAliasing", out var aaObj) &&
                        AntiAliasingModeParser.TryParse(aaObj?.ToString(), out AntiAliasingMode aaMode))
                    {
                        _antiAliasingMode = aaMode;
                        _hasAntiAliasingOverride = true;
                    }
                    else
                    {
                        _hasAntiAliasingOverride = false;
                    }
                    Console.WriteLine($"UISettingsManager: Loaded settings: Window size {_windowWidth}x{_windowHeight}, Fullscreen: {_isFullscreen}, Renderer: {_currentRenderer}, AA: {(_hasAntiAliasingOverride ? _antiAliasingMode.ToString() : "unset")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"UISettingsManager: Failed to load settings: {ex.Message}");
                    _windowWidth = 1280;
                    _windowHeight = 720;
                    _isFullscreen = false;
                    _currentRenderer = "OpenGL";
                    _availableRenderers = new List<string> { "OpenGL" };
                    _hasAntiAliasingOverride = false;
                }
            }
            else
            {
                Console.WriteLine($"UISettingsManager: Settings file not found at {_settingsPath}, using defaults");
                _windowWidth = 1280;
                _windowHeight = 720;
                _isFullscreen = false;
                _currentRenderer = "OpenGL";
                _availableRenderers = new List<string> { "OpenGL" };
                _hasAntiAliasingOverride = false;
            }
        }

        public void SaveSettings()
        {
            try
            {
                var settings = new Dictionary<string, object>
                {
                    { "WindowWidth", _windowWidth },
                    { "WindowHeight", _windowHeight },
                    { "IsFullscreen", _isFullscreen },
                    { "IconIndices", _iconIndices },
                    { "CurrentRenderer", CurrentRenderer },
                    { "AvailableRenderers", AvailableRenderers }
                };
                if (_hasAntiAliasingOverride)
                    settings["AntiAliasing"] = AntiAliasingModeParser.ToPayloadString(_antiAliasingMode);
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
                Console.WriteLine($"UISettingsManager: Saved settings: Window size {_windowWidth}x{_windowHeight}, Fullscreen: {_isFullscreen}, Renderer: {_currentRenderer}, AA: {(_hasAntiAliasingOverride ? _antiAliasingMode.ToString() : "unset")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UISettingsManager: Failed to save settings: {ex.Message}");
            }
        }
    }
}
