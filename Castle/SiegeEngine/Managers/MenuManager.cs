// SiegeEngine/Managers/MenuManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SiegeEngine.Definitions;
using SiegeEngine.Interfaces;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Rendering.Definitions;
using SiegeEngine.Scenes;
using Silk.NET.GLFW;

namespace SiegeEngine.Managers
{
    public unsafe class MenuManager
    {
        private readonly UISettingsManager _settings;
        private readonly ModManager _modManager;
        private readonly MenuRegistry _menuRegistry;
        private MenuDefinition _currentMenu;
        private List<object> _elements;
        private readonly (int Width, int Height)[] _resolutions;
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

        public MenuManager(UISettingsManager settingsManager, ModManager modManager, IGameServer server, Glfw glfw, WindowHandle* window, Player player, PlayerMovement playerMovement, string configPath = null)
        {
            _settings = settingsManager;
            _modManager = modManager;
            _server = server;
            _glfw = glfw;
            _window = window;
            _player = player;
            _playerMovement = playerMovement;
            _menuRegistry = new MenuRegistry();
            _elements = new List<object>();
            _resolutions = new (int Width, int Height)[]
            {
                (800, 600), (1024, 768), (1280, 960), (1400, 1050), (1600, 1200),
                (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160),
                (1280, 800), (1440, 900), (1680, 1050), (1920, 1200), (2560, 1600),
                (2560, 1080), (3440, 1440), (5120, 2160),
                (3840, 1080), (5120, 1440)
            };
            if (_player != null)
                _player.InitializeCamera(glfw);

            string resolvedConfigPath = configPath != null ? _modManager.ResolvePath(configPath) : _modManager.GetMenuConfigPath();
            LoadMenuConfig(resolvedConfigPath);
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

        private void LoadMenuConfig(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Menu configuration file not found at: {configPath}");
                }
                string json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<MenuConfig>(json, options);
                if (config == null) throw new InvalidOperationException("Deserialized menu configuration is null.");
                if (config.Menus == null) throw new InvalidOperationException("Menu configuration 'Menus' property is null.");
                if (config.Menus.Count == 0) throw new InvalidOperationException("Menu configuration 'Menus' list is empty.");

                foreach (var menu in config.Menus)
                {
                    if (!string.IsNullOrEmpty(menu.Background) && !Path.IsPathRooted(menu.Background))
                    {
                        menu.Background = _modManager.ResolvePath(menu.Background);
                    }
                }

                _menuRegistry.RegisterBaseMenus(config.Menus);
                var extensions = _modManager.GetAllMenuExtensions();
                _menuRegistry.RegisterExtensions(extensions);

                var allMenus = _menuRegistry.GetAllMenus();
                if (allMenus.Count == 0) throw new InvalidOperationException("No menus available after registration.");

                _currentMenu = allMenus[0];
                if (_currentMenu == null) throw new InvalidOperationException("First menu is null.");

                LoadCurrentMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MenuManager: Failed to load menu config: {ex.Message}");
                string defaultConfigPath = _modManager.GetMenuConfigPath();
                if (configPath != defaultConfigPath)
                {
                    LoadMenuConfig(defaultConfigPath);
                }
                else
                {
                    throw new InvalidOperationException("Failed to load default menu configuration.", ex);
                }
            }
        }

        public void LoadCurrentMenu()
        {
            _glfw.GetWindowSize(_window, out int currentWidth, out int currentHeight);
            Console.WriteLine($"MenuManager: Loading menu {_currentMenu.Name}, current window size: {currentWidth}x{currentHeight}");
            _elements = new List<object>();

            var buttons = _currentMenu.Buttons ?? new List<ButtonDefinition>();
            var elements = _currentMenu.Elements ?? new List<Dictionary<string, object>>();

            foreach (var buttonDef in buttons)
            {
                if (buttonDef == null) continue;
                Action onClick = buttonDef.Text switch
                {
                    "Sandbox Mode" => () => _gameStarted = true,
                    "Toggle Inventory" => () => _showInventory = !_showInventory,
                    "Level Editor" => () =>
                    {
                        _editorMode = true;
                        Console.WriteLine("MenuManager: EditorMode triggered");
                    }
                    ,
                    _ => buttonDef.Action switch
                    {
                        "ModeSelected_MMO" => () => OnModeSelected?.Invoke(GameMode.MMO),
                        "ModeSelected_Standalone" => () => OnModeSelected?.Invoke(GameMode.Standalone),
                        "ModeSelected_SteamMatchmaking" => () => OnModeSelected?.Invoke(GameMode.SteamMatchmaking),
                        "SettingsSelected" => () => { SwitchMenu("UserSettingsMenu"); OnSettingsSelected?.Invoke(); }
                        ,
                        "InviteSelected" => () => OnInviteSelected?.Invoke(),
                        "Exit" => () => OnExit?.Invoke(),
                        "SandboxMode" => () => _gameStarted = true,
                        "EditorMode" => () =>
                        {
                            _editorMode = true;
                            Console.WriteLine("MenuManager: EditorMode triggered via action");
                        }
                        ,
                        _ => () => { }
                    }
                };
                _elements.Add(new Button(buttonDef, onClick));
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var element in elements)
            {
                string type = element.GetValueOrDefault("type")?.ToString();
                if (string.IsNullOrEmpty(type)) continue;

                switch (type.ToLower())
                {
                    case "button":
                        var buttonDef = JsonSerializer.Deserialize<ButtonDefinition>(JsonSerializer.Serialize(element), options);
                        if (buttonDef == null) continue;
                        Action onClick = buttonDef.Action switch
                        {
                            "ModeSelected_MMO" => () => OnModeSelected?.Invoke(GameMode.MMO),
                            "ModeSelected_Standalone" => () => OnModeSelected?.Invoke(GameMode.Standalone),
                            "ModeSelected_SteamMatchmaking" => () => OnModeSelected?.Invoke(GameMode.SteamMatchmaking),
                            "SettingsSelected" => () => { SwitchMenu("UserSettingsMenu"); OnSettingsSelected?.Invoke(); }
                            ,
                            "InviteSelected" => () => OnInviteSelected?.Invoke(),
                            "Exit" => () => OnExit?.Invoke(),
                            "ShowSettingsMenu" => () => SwitchMenu("UserSettingsMenu"),
                            "ShowMainMenu" => () => { _editorMode = false; SwitchMenu("MainMenu"); }
                            ,
                            "ChangeResolution" => () => OnChangeResolution?.Invoke(0),
                            "ToggleFullScreen" => () => OnToggleFullScreen?.Invoke(false),
                            "ApplySettings" => () => OnApplySettings?.Invoke(),
                            "SandboxMode" => () => _gameStarted = true,
                            "ToggleInventory" => () => _showInventory = !_showInventory,
                            "EditorMode" => () =>
                            {
                                _editorMode = true;
                                Console.WriteLine("MenuManager: EditorMode triggered via button element");
                            }
                            ,
                            "SaveLevel" => () => _editorScene?.SaveLevel("level.json"),
                            "SetBrush" => () => _editorScene?.SetBrush(buttonDef.Text ?? "Wall"),
                            "ToggleGridSnap" => () => _editorScene?.ToggleGridSnap(!_editorScene._gridSnap),
                            _ => () => { }
                        };
                        _elements.Add(new Button(buttonDef, onClick));
                        break;
                    case "dropdown":
                        var dropdownDef = JsonSerializer.Deserialize<DropdownDefinition>(JsonSerializer.Serialize(element), options);
                        if (dropdownDef == null) continue;
                        if (dropdownDef.Name == "Resolution" && (dropdownDef.Options == null || dropdownDef.Options.Count == 0))
                        {
                            dropdownDef.Options = new List<string>();
                            foreach (var (width, height) in _resolutions)
                            {
                                dropdownDef.Options.Add($"{width}x{height}");
                            }
                        }
                        if (dropdownDef.Name == "Renderer" && (dropdownDef.Options == null || dropdownDef.Options.Count == 0))
                        {
                            dropdownDef.Options = _settings.AvailableRenderers;
                            dropdownDef.SelectedIndex = _settings.AvailableRenderers.IndexOf(_settings.CurrentRenderer);
                        }
                        Action<int> onSelect = dropdownDef.Action switch
                        {
                            "ChangeResolution" => (index) => OnChangeResolution?.Invoke(index),
                            "ChangeAspectRatio" => (index) => OnChangeAspectRatio?.Invoke(index),
                            "SetBrush" => (index) => _editorScene?.SetBrush(dropdownDef.Options[index]),
                            "ChangeRenderer" => (index) =>
                            {
                                string selected = dropdownDef.Options[index];
                                if (_settings.AvailableRenderers.Contains(selected))
                                {
                                    _settings.CurrentRenderer = selected;
                                    _settings.SaveSettings();
                                    OnRendererChanged?.Invoke(selected);
                                }
                            }
                            ,
                            _ => (index) => { }
                        };
                        _elements.Add(new Dropdown(dropdownDef, onSelect));
                        break;
                    case "toggle":
                        var toggleDef = JsonSerializer.Deserialize<ToggleDefinition>(JsonSerializer.Serialize(element), options);
                        if (toggleDef == null) continue;
                        Action<bool> onToggle = toggleDef.Action switch
                        {
                            "ToggleFullScreen" => (state) => OnToggleFullScreen?.Invoke(state),
                            "ToggleGridSnap" => (state) => _editorScene?.ToggleGridSnap(state),
                            _ => (state) => { }
                        };
                        _elements.Add(new Toggle(toggleDef, onToggle));
                        break;
                    case "label":
                        var labelDef = JsonSerializer.Deserialize<LabelDefinition>(JsonSerializer.Serialize(element), options);
                        if (labelDef == null) continue;
                        _elements.Add(new Label(labelDef));
                        break;
                }
            }

            if (_showInventory && _player != null)
            {
                LoadInventoryMenu();
            }

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
                    int iconIndex = _currentMenu.Buttons?.Find(b => b.Text == text)?.IconIndex ??
                                    _currentMenu.Elements?.Find(e => e.GetValueOrDefault("name")?.ToString() == text)?.GetValueOrDefault("iconIndex") as int? ?? 0;
                    _settings.IconIndices[text] = iconIndex;
                }
            }
        }

        private void LoadInventoryMenu()
        {
            var entity = _server.GetEntityById(_player.EntityId);
            var inventory = entity?.GetComponent<InventoryComponent>();
            if (inventory == null) return;

            _elements.Add(new Label(new LabelDefinition
            {
                Text = $"Cash: {inventory.Cash}",
                Position = new Position { X = 10, Y = 10 },
                TextStyle = new TextStyle { FontSize = 16 }
            }));

            int yOffset = 40;
            foreach (var item in inventory.Items.Values)
            {
                var rarityColor = item.Rarity switch
                {
                    InventoryComponent.Rarity.Common => new Color { R = 1.0f, G = 1.0f, B = 1.0f, A = 1.0f },
                    InventoryComponent.Rarity.Uncommon => new Color { R = 0.0f, G = 1.0f, B = 0.0f, A = 1.0f },
                    InventoryComponent.Rarity.Rare => new Color { R = 0.0f, G = 0.0f, B = 1.0f, A = 1.0f },
                    InventoryComponent.Rarity.Epic => new Color { R = 0.5f, G = 0.0f, B = 0.5f, A = 1.0f },
                    InventoryComponent.Rarity.Legendary => new Color { R = 1.0f, G = 0.5f, B = 0.0f, A = 1.0f },
                    _ => new Color { R = 1.0f, G = 1.0f, B = 1.0f, A = 1.0f }
                };

                _elements.Add(new Label(new LabelDefinition
                {
                    Text = $"{item.Name} (T{item.Tier} L{item.Level}) [{item.StackSize}]",
                    Position = new Position { X = 10, Y = yOffset },
                    TextStyle = new TextStyle { FontSize = 14, Color = rarityColor }
                }));

                _elements.Add(new Button(new ButtonDefinition
                {
                    Text = "Upgrade",
                    Position = new Position { X = 220, Y = yOffset },
                    Size = new Size { Width = 80, Height = 20 },
                    Action = "UpgradeItem"
                }, () =>
                {
                    if (_server.ValidateInventory(_player.EntityId, "UpgradeItem", item.Id))
                    {
                        inventory.UpgradeItem(item.Id);
                        SwitchMenu(_currentMenu.Name);
                    }
                }));

                yOffset += 30;
            }
        }

        public void SwitchMenu(string menuName)
        {
            var newMenu = _menuRegistry.GetMenuByName(menuName);
            if (newMenu != null)
            {
                _glfw.GetWindowSize(_window, out int width, out int height);
                Console.WriteLine($"MenuManager: Switching to menu {menuName}, current window size: {width}x{height}");
                _currentMenu = newMenu;
                _currentTab = null;
                LoadCurrentMenu();
                OnMenuSwitched?.Invoke(newMenu.Background ?? "", _settings.IconIndices);
            }
        }

        public void SwitchTab(string tabName)
        {
            if (_currentMenu.Tabs == null) return;
            var tab = _currentMenu.Tabs.Find(t => t.Name == tabName);
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
                string text = element switch
                {
                    Button button => button.Text,
                    Dropdown dropdown => dropdown.Name,
                    Toggle toggle => toggle.Name,
                    Label label => label.Text,
                    _ => null
                };
                if (text != null)
                {
                    int currentIndex = newIndices[text];
                    int nextIndex = (Array.IndexOf(new int[] { 0, 1, 3, 13, 15, 23 }, currentIndex) + 1) % 6;
                    newIndices[text] = new int[] { 0, 1, 3, 13, 15, 23 }[nextIndex];
                }
            }
            _settings.UpdateIconIndices(newIndices);
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