using SiegeEngine.Definitions;
using SiegeEngine.Interfaces;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Rendering.Definitions;
using SiegeEngine.Scenes;
using Silk.NET.GLFW;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SiegeEngine.Managers
{
    public unsafe class MenuManager
    {
        private readonly UISettingsManager _settings;
        private readonly ModManager _modManager;
        private readonly (int Width, int Height)[] _resolutions;
        private readonly Dictionary<string, List<(int Width, int Height)>> _resolutionsByAspectRatio;
        private readonly IGameServer _server;
        private readonly Glfw _glfw;
        private readonly WindowHandle* _window;
        private Player _player;
        private PlayerMovement _playerMovement;
        private bool _gameStarted;
        private bool _showInventory;
        private bool _editorMode;
        private EditorScene _editorScene;
        private string _currentTab;
        private string _menuDir;
        private MenuDefinition _currentMenu;
        private List<object> _elements;

        public MenuManager(UISettingsManager settingsManager, ModManager modManager, IGameServer server, Glfw glfw, WindowHandle* window, Player player, PlayerMovement playerMovement, string initialMenu = "MainMenu.html")
        {
            _settings = settingsManager;
            _modManager = modManager;
            _server = server;
            _glfw = glfw;
            _window = window;
            _player = player;
            _playerMovement = playerMovement;
            _elements = new List<object>();
            _resolutions = new (int Width, int Height)[]
            {
                (800, 600), (1024, 768), (1280, 960), (1400, 1050), (1600, 1200),
                (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160),
                (1280, 800), (1440, 900), (1680, 1050), (1920, 1200), (2560, 1600),
                (2560, 1080), (3440, 1440), (5120, 2160),
                (3840, 1080), (5120, 1440)
            };
            _resolutionsByAspectRatio = new Dictionary<string, List<(int Width, int Height)>>();
            foreach (var res in _resolutions)
            {
                string aspect = GetAspectRatio(res.Width, res.Height);
                if (!_resolutionsByAspectRatio.ContainsKey(aspect))
                {
                    _resolutionsByAspectRatio[aspect] = new List<(int Width, int Height)>();
                }
                _resolutionsByAspectRatio[aspect].Add(res);
            }
            if (_player != null)
                _player.InitializeCamera(_glfw);

            string resolvedPath = _modManager.ResolvePath(initialMenu);
            _menuDir = Path.GetDirectoryName(resolvedPath);
            LoadMenu(resolvedPath);
        }

        public CameraController Camera => _player?.Camera;
        public bool GameStarted => _gameStarted;
        public MenuDefinition CurrentMenu => _currentMenu;
        public List<object> Elements => _elements;
        public bool ShowInventory => _showInventory;
        public bool EditorMode => _editorMode;
        public EditorScene EditorScene => _editorScene;

        public void SetPlayerAndMovement(Player player, PlayerMovement playerMovement)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _playerMovement = playerMovement ?? throw new ArgumentNullException(nameof(playerMovement));
            _player.InitializeCamera(_glfw);
        }

        private void LoadMenu(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Menu HTML file not found at: {path}");
                }
                string html = File.ReadAllText(path);
                string menuName = Path.GetFileNameWithoutExtension(path);
                var parser = new HtmlMenuParser();
                _currentMenu = parser.ParseMenu(html, menuName);
                if (_currentMenu == null) throw new InvalidOperationException("Failed to parse menu HTML.");
                LoadCurrentMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MenuManager: Failed to load menu HTML: {ex.Message}");
            }
        }

        public void LoadCurrentMenu()
        {
            _glfw.GetWindowSize(_window, out int currentWidth, out int currentHeight);
            Console.WriteLine($"MenuManager: Loading menu {_currentMenu.Name}, current window size: {currentWidth}x{currentHeight}");
            _elements = new List<object>();

            if (_currentMenu.Tabs != null && _currentMenu.Tabs.Count > 0)
            {
                _currentTab = _currentMenu.Tabs[0].Name;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var buttonDef in _currentMenu.Buttons ?? new List<ButtonDefinition>())
            {
                if (buttonDef == null) continue;
                Action onClick = GetButtonAction(buttonDef.Action);
                _elements.Add(new Button(buttonDef, onClick));
            }

            foreach (var element in _currentMenu.Elements ?? new List<Dictionary<string, object>>())
            {
                string type = element.GetValueOrDefault("type")?.ToString();
                if (string.IsNullOrEmpty(type)) continue;

                switch (type.ToLower())
                {
                    case "dropdown":
                        var dropdownDef = JsonSerializer.Deserialize<DropdownDefinition>(JsonSerializer.Serialize(element), options);
                        if (dropdownDef == null) continue;
                        Action<int> onSelect = GetDropdownAction(dropdownDef.Action);
                        var dropdown = new Dropdown(dropdownDef, onSelect);
                        _elements.Add(dropdown);
                        break;
                    case "toggle":
                        var toggleDef = JsonSerializer.Deserialize<ToggleDefinition>(JsonSerializer.Serialize(element), options);
                        if (toggleDef == null) continue;
                        Action<bool> onToggle = GetToggleAction(toggleDef.Action);
                        _elements.Add(new Toggle(toggleDef, onToggle));
                        break;
                    case "label":
                        var labelDef = JsonSerializer.Deserialize<LabelDefinition>(JsonSerializer.Serialize(element), options);
                        if (labelDef == null) continue;
                        _elements.Add(new Label(labelDef));
                        break;
                }
            }

            //if (_currentTab != null)
            //{
            //    SwitchTab(_currentTab);
            //}

            //if (_showInventory && _player != null)
            //{
            //    LoadInventoryMenu();
            //}

            UpdateIconIndices();
            OnMenuSwitched?.Invoke(_currentMenu.Background ?? "", _settings.IconIndices);
        }

        private void UpdateIconIndices()
        {
            foreach (var element in _elements)
            {
                string text = element switch
                {
                    Button button => button.Text,
                    Dropdown dropdown => dropdown.Name,
                    Toggle toggle => toggle.Name,
                    Label label => label.Text,
                    _ => null
                };
                if (text != null && !_settings.IconIndices.ContainsKey(text))
                {
                    _settings.IconIndices[text] = 0;
                }
            }
        }

        public void SwitchMenu(string menuName)
        {
            string path = Path.Combine(_menuDir, menuName + ".html");
            if (File.Exists(path))
            {
                _glfw.GetWindowSize(_window, out int width, out int height);
                Console.WriteLine($"MenuManager: Switching to menu {menuName}, current window size: {width}x{height}");
                LoadMenu(path);
            }
        }

        public void SwitchTab(string tabName)
        {
            var tab = _currentMenu.Tabs?.Find(t => t.Name == tabName);
            if (tab != null)
            {
                _currentTab = tabName;
                _currentMenu.Buttons = tab.Buttons ?? new List<ButtonDefinition>();
                _currentMenu.Elements = tab.Elements ?? new List<Dictionary<string, object>>();
                LoadCurrentMenu();
            }
        }

        public void CycleIcons()
        {
            var newIndices = new Dictionary<string, int>(_settings.IconIndices);
            foreach (var element in _elements)
            {
                string name = element switch
                {
                    Button btn => btn.Text,
                    Dropdown dd => dd.Name,
                    Toggle tg => tg.Name,
                    Label lb => lb.Text,
                    _ => null
                };
                if (name != null)
                {
                    int currentIndex = newIndices.GetValueOrDefault(name, 0);
                    int nextIndex = (Array.IndexOf(new int[] { 0, 1, 3, 13, 15, 23 }, currentIndex) + 1) % 6;
                    newIndices[name] = new int[] { 0, 1, 3, 13, 15, 23 }[nextIndex];
                }
            }
            _settings.UpdateIconIndices(newIndices);
        }

        private Action GetButtonAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return () => { };
            if (action.StartsWith("SwitchMenu_"))
            {
                string menuName = action.Substring(11);
                return () => SwitchMenu(menuName);
            }
            if (action.StartsWith("SwitchTab_"))
            {
                string tabName = action.Substring(10);
                return () => SwitchTab(tabName);
            }
            if (action.StartsWith("ModeSelected_"))
            {
                if (Enum.TryParse<GameMode>(action.Substring(13), out var mode))
                {
                    return () => OnModeSelected?.Invoke(mode);
                }
            }
            if (action.StartsWith("SetBrush_"))
            {
                string brush = action.Substring(9);
                return () => _editorScene?.SetBrush(brush);
            }
            switch (action)
            {
                case "SettingsSelected":
                    return () => { SwitchMenu("UserSettingsMenu"); OnSettingsSelected?.Invoke(); };
                case "InviteSelected":
                    return () => OnInviteSelected?.Invoke();
                case "Exit":
                    return () => OnExit?.Invoke();
                case "SandboxMode":
                    return () => _gameStarted = true;
                case "ToggleInventory":
                    return () => _showInventory = !_showInventory;
                case "EditorMode":
                    return () => _editorMode = true;
                case "SaveLevel":
                    return () => _editorScene?.SaveLevel("level.json");
                case "ShowMainMenu":
                    return () => { _editorMode = false; SwitchMenu("MainMenu"); };
                case "ApplySettings":
                    return () => OnApplySettings?.Invoke();
                default:
                    return () => { };
            }
        }

        private Action<int> GetDropdownAction(string action)
        {
            switch (action)
            {
                case "ChangeResolution":
                    return index => OnChangeResolution?.Invoke(index);
                case "ChangeAspectRatio":
                    return index => OnChangeAspectRatio?.Invoke(index);
                case "SetBrush":
                    return index => { };
                case "ChangeRenderer":
                    return index => { string selected = _settings.AvailableRenderers[index]; _settings.CurrentRenderer = selected; _settings.SaveSettings(); OnRendererChanged?.Invoke(selected); };
                default:
                    return index => { };
            }
        }

        private Action<bool> GetToggleAction(string action)
        {
            switch (action)
            {
                case "ToggleFullScreen":
                    return state => OnToggleFullScreen?.Invoke(state);
                case "ToggleGridSnap":
                    return state => _editorScene?.ToggleGridSnap(state);
                default:
                    return state => { };
            }
        }

        private string GetAspectRatio(int width, int height)
        {
            if (width <= 0 || height <= 0) return "16:9";
            int gcd = GCD(width, height);
            int ratioW = width / gcd;
            int ratioH = height / gcd;
            return $"{ratioW}:{ratioH}";
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

        public event Action<string, Dictionary<string, int>> OnMenuSwitched;
        public event Action<GameMode> OnModeSelected;
        public event Action OnSettingsSelected;
        public event Action OnInviteSelected;
        public event Action OnExit;
        public event Action<int> OnChangeResolution;
        public event Action<int> OnChangeAspectRatio;
        public event Action<bool> OnToggleFullScreen;
        public event Action OnApplySettings;
        public event Action<string> OnRendererChanged;
    }
}