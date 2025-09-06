using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Definitions;
using SiegeEngine.Interfaces;
using SiegeEngine.Managers;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Definitions;
using Silk.NET.GLFW;
namespace SiegeEngine.PlayerSystem
{
    public unsafe class CustomUIController : IDisposable
    {
        private readonly Glfw _glfw;
        private readonly IRenderContext _renderContext;
        private readonly WindowHandle* _window;
        private readonly MenuManager _menuManager;
        private readonly PositionCalculator _positionCalculator;
        private readonly InputHandler _inputHandler;
        private readonly UIRenderer _uiRenderer;
        private readonly UISettingsManager _settingsManager;
        private bool _disposed;
        private Button _hoveredButtonOnMouseDown;
        private Dropdown _hoveredDropdownOnMouseDown;
        private Toggle _hoveredToggleOnMouseDown;
        private readonly (int Width, int Height)[] _resolutions;
        private readonly Dictionary<string, List<(int Width, int Height)>> _resolutionsByAspectRatio;
        private List<(int Width, int Height)> _filteredResolutions;
        private int _currentResolutionIndex;
        private int _currentAspectRatioIndex;
        private int _windowedPosX;
        private int _windowedPosY;
        private int _windowedWidth;
        private int _windowedHeight;
        private int _pendingResolutionIndex;
        private int _pendingAspectRatioIndex;
        private bool _pendingFullscreenState;
        public CustomUIController(Glfw glfw, IRenderContext renderContext, WindowHandle* window, UISettingsManager settingsManager, MenuManager menuManager, IGameServer server, InputHandler inputHandler)
        {
            _glfw = glfw;
            _renderContext = renderContext;
            _window = window;
            _settingsManager = settingsManager;
            _menuManager = menuManager;
            _positionCalculator = new PositionCalculator();
            _inputHandler = inputHandler;
            _uiRenderer = new UIRenderer(glfw, renderContext, window);
            _resolutions = new (int Width, int Height)[]
            {
                (800, 600), (1024, 768), (1280, 960), (1400, 1050), (1600, 1200),
                (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160),
                (1280, 800), (1440, 900), (1680, 1050), (1920, 1200), (2560, 1600),
                (2560, 1080), (3440, 1440), (5120, 2160),
                (3840, 1080), (5120, 1440)
            };
            Monitor* monitor = _glfw.GetPrimaryMonitor();
            VideoMode* mode = _glfw.GetVideoMode(monitor);
            int maxWidth = mode->Width;
            int maxHeight = mode->Height;
            var resolutionsList = _resolutions.ToList();
            if (!resolutionsList.Contains((maxWidth, maxHeight)))
            {
                resolutionsList.Add((maxWidth, maxHeight));
            }
            resolutionsList.Sort((a, b) => a.Width == b.Width ? a.Height.CompareTo(b.Height) : a.Width.CompareTo(b.Width));
            _resolutions = resolutionsList.ToArray();
            _resolutionsByAspectRatio = new Dictionary<string, List<(int Width, int Height)>>();
            foreach (var res in _resolutions)
            {
                string aspectRatio = GetAspectRatio(res.Width, res.Height);
                if (!_resolutionsByAspectRatio.ContainsKey(aspectRatio))
                {
                    _resolutionsByAspectRatio[aspectRatio] = new List<(int Width, int Height)>();
                }
                _resolutionsByAspectRatio[aspectRatio].Add(res);
            }
            _filteredResolutions = _resolutionsByAspectRatio["16:9"].ToList();
            // Initialize fullscreen state
            _pendingFullscreenState = _glfw.GetWindowMonitor(_window) != null;
            _settingsManager.UpdateFullscreen(_pendingFullscreenState, false);
            // Adopt current window size
            _settingsManager.LoadSettings();
            int currentWidth = _settingsManager.WindowWidth;
            int currentHeight = _settingsManager.WindowHeight;
            if (!_pendingFullscreenState)
            {
                // Windowed: use GLFW size if valid, else settings
                _glfw.GetWindowSize(_window, out currentWidth, out currentHeight);
                if (currentWidth <= 0 || currentHeight <= 0)
                {
                    currentWidth = _settingsManager.WindowWidth > 0 ? _settingsManager.WindowWidth : 1280;
                    currentHeight = _settingsManager.WindowHeight > 0 ? _settingsManager.WindowHeight : 720;
                }
                //Console.WriteLine($"CustomUIController: Adopting windowed size: {currentWidth}x{currentHeight}");
                _settingsManager.UpdateWindowSize(currentWidth, currentHeight, false);
            }
            else
            {
                // Fullscreen: use native
                currentWidth = maxWidth;
                currentHeight = maxHeight;
                _settingsManager.AllowResize = true;
                _glfw.SetWindowSize(_window, currentWidth, currentHeight);
                _settingsManager.AllowResize = false;
                //Console.WriteLine($"CustomUIController: Setting fullscreen to native: {currentWidth}x{currentHeight}");
                _settingsManager.UpdateWindowSize(currentWidth, currentHeight, false);
            }
            _currentResolutionIndex = _pendingResolutionIndex = 0;
            _currentAspectRatioIndex = _pendingAspectRatioIndex = 0;
            for (int i = 0; i < _resolutions.Length; i++)
            {
                if (_settingsManager.WindowWidth == _resolutions[i].Width && _settingsManager.WindowHeight == _resolutions[i].Height)
                {
                    _currentResolutionIndex = _pendingResolutionIndex = i;
                    _currentAspectRatioIndex = _pendingAspectRatioIndex = Array.IndexOf(_resolutionsByAspectRatio.Keys.ToArray(), GetAspectRatio(_resolutions[i].Width, _resolutions[i].Height));
                    _filteredResolutions = _resolutionsByAspectRatio[_resolutionsByAspectRatio.Keys.ToArray()[_currentAspectRatioIndex]].ToList();
                    break;
                }
            }
            _glfw.GetWindowPos(_window, out _windowedPosX, out _windowedPosY);
            _windowedWidth = currentWidth;
            _windowedHeight = currentHeight;
            _menuManager.OnMenuSwitched += (background, indices) =>
            {
                _glfw.GetWindowSize(_window, out int width, out int height);
                //Console.WriteLine($"CustomUIController: OnMenuSwitched to {background}, before Initialize, window size: {width}x{height}");
                _uiRenderer.Initialize(background, indices);
                _glfw.GetWindowSize(_window, out width, out height);
                //Console.WriteLine($"CustomUIController: OnMenuSwitched to {background}, after Initialize, window size: {width}x{height}");
            };
            _menuManager.OnModeSelected += mode => ModeSelected?.Invoke(mode);
            _menuManager.OnInviteSelected += () => InviteSelected?.Invoke();
            _menuManager.OnExit += () =>
            {
                _settingsManager.SaveSettings();
                //Console.WriteLine("CustomUIController: Saving settings before exit");
                _glfw.SetWindowShouldClose(_window, true);
            };
            _menuManager.OnChangeResolution += index =>
            {
                _pendingResolutionIndex = index;
                //Console.WriteLine($"CustomUIController: Resolution index changed to {_pendingResolutionIndex}");
            };
            _menuManager.OnChangeAspectRatio += index =>
            {
                _pendingAspectRatioIndex = index;
                UpdateResolutionOptions();
                //Console.WriteLine($"CustomUIController: Aspect ratio index changed to {_pendingAspectRatioIndex}");
            };
            _menuManager.OnToggleFullScreen += state =>
            {
                _pendingFullscreenState = state;
                _settingsManager.UpdateFullscreen(state);
                ToggleFullScreen();
            };
            _menuManager.OnSettingsSelected += () =>
            {
                _glfw.GetWindowSize(_window, out int width, out int height);
                //Console.WriteLine($"CustomUIController: SettingsSelected triggered, current window size: {width}x{height}");
                SettingsSelected?.Invoke();
            };
            _menuManager.OnApplySettings += () =>
            {
                _settingsManager.AllowResize = true;
                if (_pendingResolutionIndex >= 0 && _pendingResolutionIndex < _filteredResolutions.Count)
                {
                    var (width, height) = _filteredResolutions[_pendingResolutionIndex];
                    _currentResolutionIndex = _pendingResolutionIndex;
                    //Console.WriteLine($"CustomUIController: Applying resolution {width}x{height}");
                    _glfw.SetWindowSize(_window, width, height);
                    _settingsManager.UpdateWindowSize(width, height);
                    _windowedWidth = width;
                    _windowedHeight = height;
                    ToggleFullScreen();
                    _settingsManager.SaveSettings();
                }
                else
                {
                    //Console.WriteLine($"CustomUIController: Invalid resolution index {_pendingResolutionIndex}, using default 1280x720");
                    _glfw.SetWindowSize(_window, 1280, 720);
                    _settingsManager.UpdateWindowSize(1280, 720);
                    _windowedWidth = 1280;
                    _windowedHeight = 720;
                    ToggleFullScreen();
                    _settingsManager.SaveSettings();
                }
                _settingsManager.AllowResize = false;
            };
            Initialize();
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
        private void UpdateResolutionOptions()
        {
            string selectedAspectRatio = _resolutionsByAspectRatio.Keys.ToArray()[_pendingAspectRatioIndex];
            _filteredResolutions = _resolutionsByAspectRatio[selectedAspectRatio].OrderBy(r => r.Width).ToList();
            var newOptions = _filteredResolutions.Select(r => $"{r.Width}x{r.Height}").ToList();
            foreach (var element in _menuManager.Elements)
            {
                if (element is Dropdown dropdown && dropdown.Name == "Resolution")
                {
                    dropdown.UpdateOptions(newOptions);
                    if (_pendingResolutionIndex >= newOptions.Count)
                    {
                        dropdown.SelectOption(0);
                        _pendingResolutionIndex = 0;
                        _currentResolutionIndex = 0;
                    }
                    //Console.WriteLine($"CustomUIController: Updated resolution options for {selectedAspectRatio}: {string.Join(", ", newOptions)}");
                    break;
                }
            }
        }
        public void Initialize()
        {
            _glfw.SetInputMode(_window, CursorStateAttribute.Cursor, CursorModeValue.CursorNormal);
            _glfw.GetWindowSize(_window, out int width, out int height);
            //Console.WriteLine($"CustomUIController: Initialize called, current window size: {width}x{height}");
            if (_menuManager.CurrentMenu == null)
            {
                Console.WriteLine("CustomUIController: Menu not loaded, skipping UI initialization");
                return;
            }
            _uiRenderer.Initialize(_menuManager.CurrentMenu.Background ?? "", _settingsManager.IconIndices);
        }
        public void Update(float deltaTime)
        {
            // Skip input processing in EditorMode
            if (_menuManager.EditorMode)
            {
                //Console.WriteLine("CustomUIController: Skipping Update in EditorMode");
                return;
            }
            //Console.WriteLine("CustomUIController: Update called");
            int windowWidth, windowHeight;
            _glfw.GetWindowSize(_window, out windowWidth, out windowHeight);
            _glfw.SetInputMode(_window, CursorStateAttribute.Cursor, CursorModeValue.CursorNormal);
            _glfw.GetCursorPos(_window, out double mouseX, out double mouseY);
            Vector2 mousePos = new Vector2((float)mouseX, (float)mouseY);
            //Console.WriteLine($"CustomUIController: Mouse at ({mousePos.X}, {mousePos.Y}), Cursor mode: {_glfw.GetInputMode(_window, CursorStateAttribute.Cursor)}");
            if (_inputHandler.MouseDown)
            {
                Console.WriteLine($"CustomUIController: Mouse down at ({mousePos.X}, {mousePos.Y})");
            }
            foreach (var element in _menuManager.Elements)
            {
                if (element == null) continue;
                Vector2 adjustedPos = _positionCalculator.CalculateAdjustedPosition(
                    element switch
                    {
                        Button button => button.Position,
                        Dropdown dropdown => dropdown.Position,
                        Toggle toggle => toggle.Position,
                        Label label => label.Position,
                        _ => Vector2.Zero
                    },
                    _menuManager.CurrentMenu.PositioningMode, windowWidth, windowHeight);
                switch (element)
                {
                    case Button button:
                        button.Update(adjustedPos, mousePos);
                        float xMax = adjustedPos.X + button.Size.X;
                        float yMax = adjustedPos.Y + button.Size.Y;
                        bool inBounds = mousePos.X >= adjustedPos.X && mousePos.X <= xMax && mousePos.Y >= adjustedPos.Y && mousePos.Y <= yMax;
                        //Console.WriteLine($"CustomUIController: Button '{button.Text}' - Pos: ({adjustedPos.X}, {adjustedPos.Y}) to ({xMax}, {yMax}), Mouse: ({mousePos.X}, {mousePos.Y}), Hovered: {button.IsHovered}, InBounds: {inBounds}");
                        if (_inputHandler.MouseDown && inBounds)
                        {
                            _hoveredButtonOnMouseDown = button;
                            //Console.WriteLine($"CustomUIController: Mouse down on '{button.Text}'");
                        }
                        break;
                    case Dropdown dropdown:
                        dropdown.Update(adjustedPos, mousePos);
                        if (_inputHandler.MouseDown && dropdown.IsHovered)
                        {
                            _hoveredDropdownOnMouseDown = dropdown;
                        }
                        if (dropdown.IsExpanded && _inputHandler.MouseDown)
                        {
                            int selectedIndex = dropdown.GetOptionIndexAt(mousePos, adjustedPos);
                            if (selectedIndex >= 0)
                            {
                                _hoveredDropdownOnMouseDown = dropdown;
                            }
                        }
                        break;
                    case Toggle toggle:
                        toggle.Update(adjustedPos, mousePos);
                        if (_inputHandler.MouseDown && toggle.IsHovered)
                        {
                            _hoveredToggleOnMouseDown = toggle;
                        }
                        break;
                    case Label label:
                        label.Update(adjustedPos, mousePos);
                        break;
                }
            }
            if (_inputHandler.MouseReleased)
            {
                //Console.WriteLine($"CustomUIController: Mouse released at ({mousePos.X}, {mousePos.Y})");
                if (_hoveredButtonOnMouseDown != null && _hoveredButtonOnMouseDown.IsHovered)
                {
                    Console.WriteLine($"CustomUIController: Triggering click on '{_hoveredButtonOnMouseDown.Text}'");
                    _hoveredButtonOnMouseDown.TriggerClick();
                    _hoveredButtonOnMouseDown = null;
                }
                if (_hoveredDropdownOnMouseDown != null)
                {
                    Vector2 adjustedPos = _positionCalculator.CalculateAdjustedPosition(
                        _hoveredDropdownOnMouseDown.Position, _menuManager.CurrentMenu.PositioningMode, windowWidth, windowHeight);
                    if (_hoveredDropdownOnMouseDown.IsExpanded)
                    {
                        int selectedIndex = _hoveredDropdownOnMouseDown.GetOptionIndexAt(mousePos, adjustedPos);
                        if (selectedIndex >= 0)
                        {
                            _hoveredDropdownOnMouseDown.SelectOption(selectedIndex);
                        }
                        else
                        {
                            _hoveredDropdownOnMouseDown.ToggleExpanded();
                        }
                    }
                    else if (_hoveredDropdownOnMouseDown.IsHovered)
                    {
                        _hoveredDropdownOnMouseDown.ToggleExpanded();
                    }
                    _hoveredDropdownOnMouseDown = null;
                }
                if (_hoveredToggleOnMouseDown != null && _hoveredToggleOnMouseDown.IsHovered)
                {
                    _hoveredToggleOnMouseDown.ToggleState();
                    _hoveredToggleOnMouseDown = null;
                }
            }
            _inputHandler.ResetMouseReleased();
        }
        public void Render()
        {
            if (_menuManager.EditorMode)
            {
                //Console.WriteLine("CustomUIController: Skipping Render in EditorMode");
                return;
            }
            _glfw.GetWindowSize(_window, out int width, out int height);
            //Console.WriteLine($"CustomUIController: Window size: {width}x{height}");
            //Console.WriteLine("CustomUIController: Entering Render");
            _uiRenderer.Render(_menuManager.Elements, _menuManager.CurrentMenu.PositioningMode, _positionCalculator);
            //Console.WriteLine("CustomUIController: Exiting Render");
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                _uiRenderer.Dispose();
                _disposed = true;
            }
        }
        private void ToggleFullScreen()
        {
            _settingsManager.AllowResize = true;
            if (_pendingFullscreenState)
            {
                Monitor* monitor = _glfw.GetPrimaryMonitor();
                VideoMode* mode = _glfw.GetVideoMode(monitor);
                _glfw.SetWindowMonitor(_window, monitor, 0, 0, mode->Width, mode->Height, mode->RefreshRate);
                _settingsManager.UpdateWindowSize(mode->Width, mode->Height);
                _settingsManager.UpdateFullscreen(true);
                Console.WriteLine($"CustomUIController: Toggled fullscreen, size: {mode->Width}x{mode->Height}");
            }
            else
            {
                _glfw.SetWindowMonitor(_window, null, _windowedPosX, _windowedPosY, _windowedWidth, _windowedHeight, 0);
                _settingsManager.UpdateWindowSize(_windowedWidth, _windowedHeight);
                _settingsManager.UpdateFullscreen(false);
                Console.WriteLine($"CustomUIController: Toggled windowed, size: {_windowedWidth}x{_windowedHeight}");
            }
            _settingsManager.SaveSettings();
            _settingsManager.AllowResize = false;
        }
        public event Action<GameMode> ModeSelected;
        public event Action SettingsSelected;
        public event Action InviteSelected;
    }
}