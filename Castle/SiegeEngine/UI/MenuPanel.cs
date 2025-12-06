// Folder: SiegeEngine.UI
// File: MenuPanel.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Managers;
using SiegeEngine.PlayerSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SiegeEngine.UI
{
    public class MenuPanel : BasePanel
    {
        private class MenuUIOverlay : UIOverlay
        {
            private readonly MenuPanel _parent;
            private readonly ModManager _modManager;
            private readonly EventBus _eventBus;
            public MenuUIOverlay(MenuPanel parent, IRenderContext renderContext, IControlContext controlContext, IntPtr window, ModManager modManager, EventBus eventBus) : base(renderContext, controlContext, window)
            {
                _parent = parent;
                _modManager = modManager;
                _eventBus = eventBus;
            }
            protected override void HandleDataHook(string hook)
            {
                if (hook == "RealmFoundry.Test.LaunchSandbox")
                {
                    _eventBus.Publish(new SwitchSceneEvent("Sandbox"));
                }
                else if (hook == "ReadingChamber.OpenAssetViewer")
                {
                    try
                    {
                        string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReadingChamber.dll");
                        Assembly ass = Assembly.LoadFrom(dllPath);
                        Type type = ass.GetType("ReadingChamber.AssetViewerPanel");
                        IPanel panel = (IPanel)Activator.CreateInstance(type, _renderContext, _controlContext, _window, _eventBus);
                        _eventBus.Publish(new OpenPanelEvent(panel));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"MenuUIOverlay: Failed to open ReadingChamber panel: {ex.Message}");
                    }
                }
                else if (hook.Contains("Scene"))
                {
                    //_eventBus.Publish(new SwitchSceneEvent { Hook = hook });
                    Console.WriteLine($"MenuUIOverlay: Published SwitchSceneEvent with hook {hook}");
                }
                else if (hook == "CastleBuilder.CreateProject")
                {
                    var data = new Dictionary<string, string>();
                    var nameJs = _document.getElementById("project-name");
                    data["name"] = nameJs.value;
                    var typeJs = _document.getElementById("game-type");
                    data["projectType"] = typeJs.value;
                    var modeJs = _document.getElementById("project-mode");
                    data["mode"] = modeJs.value;
                    var modsJs = _document.getElementById("allow-mods");
                    data["allowMods"] = modsJs.@checked.ToString();
                    data["path"] = "Projects/" + data["name"];
                    _eventBus.Publish(new GenericEvent { Hook = "CreateProject", Data = data });
                }
                else
                {
                    _eventBus.Publish(new GenericEvent { Hook = hook });
                    Console.WriteLine($"MenuUIOverlay: Published GenericEvent with hook {hook}");
                }
            }
            protected override void HandleLink(string href)
            {
                if (string.IsNullOrEmpty(href)) return;
                string newPath = null;
                if (_modManager != null)
                {
                    newPath = _modManager.ResolvePath(href);
                }
                if (newPath == null)
                {
                    newPath = Path.GetFullPath(Path.Combine(_currentBaseDir, href));
                }
                if (File.Exists(newPath))
                {
                    LoadUI(File.ReadAllText(newPath), Path.GetDirectoryName(newPath) ?? "");
                    _controlContext.GetWindowSize(_window, out int w, out int h);
                    RecomputeLayout(w, h);
                }
                else
                {
                    Console.WriteLine($"MenuUIOverlay: Failed to resolve or find href path: {href}, tried {newPath}");
                }
            }
        }

        private readonly ModManager _modManager;
        private readonly string _initialHtmlPath;
        private readonly InputHandler _inputHandler;

        public MenuPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus, ModManager modManager, string initialHtmlPath, InputHandler inputHandler) : base(renderContext, controlContext, window, eventBus)
        {
            _modManager = modManager;
            _initialHtmlPath = initialHtmlPath;
            _inputHandler = inputHandler;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new MenuUIOverlay(this, _renderContext, _controlContext, _window, _modManager, _eventBus);
        }

        public override void Init()
        {
            base.Init();
            _inputHandler.KeyEvent += OnKeyEvent;
            if (File.Exists(_initialHtmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(_initialHtmlPath), Path.GetDirectoryName(_initialHtmlPath) ?? "");
            }
            else
            {
                Console.WriteLine($"MenuPanel: Initial HTML file not found at {_initialHtmlPath}");
            }
        }

        private void OnKeyEvent(Key key, InputAction action)
        {
            if (action == InputAction.Release) return;

            var focused = _uiOverlay.FocusedElement as InputElement;
            if (focused == null || focused.Type != "text") return;

            bool shiftPressed = _controlContext.GetKey(_window, Key.LeftShift) == InputAction.Press ||
                                _controlContext.GetKey(_window, Key.RightShift) == InputAction.Press;

            bool changed = false;

            if (key >= Key.A && key <= Key.Z)
            {
                char ch = (char)((int)key - (int)Key.A + (shiftPressed ? 'A' : 'a'));
                focused.Value += ch;
                changed = true;
            }
            else if (key >= Key.Key0 && key <= Key.Key9)
            {
                char noShift = (char)((int)key - (int)Key.Key0 + '0');
                char withShift = key switch
                {
                    Key.Key0 => ')',
                    Key.Key1 => '!',
                    Key.Key2 => '@',
                    Key.Key3 => '#',
                    Key.Key4 => '$',
                    Key.Key5 => '%',
                    Key.Key6 => '^',
                    Key.Key7 => '&',
                    Key.Key8 => '*',
                    Key.Key9 => '(',
                    _ => noShift
                };
                focused.Value += shiftPressed ? withShift : noShift;
                changed = true;
            }
            else if (key == Key.Space)
            {
                focused.Value += ' ';
                changed = true;
            }
            else if (key == Key.Minus)
            {
                focused.Value += shiftPressed ? '_' : '-';
                changed = true;
            }
            else if (key == Key.Equal)
            {
                focused.Value += shiftPressed ? '+' : '=';
                changed = true;
            }
            else if (key == Key.LeftBracket)
            {
                focused.Value += shiftPressed ? '{' : '[';
                changed = true;
            }
            else if (key == Key.RightBracket)
            {
                focused.Value += shiftPressed ? '}' : ']';
                changed = true;
            }
            else if (key == Key.Backslash)
            {
                focused.Value += shiftPressed ? '|' : '\\';
                changed = true;
            }
            else if (key == Key.Semicolon)
            {
                focused.Value += shiftPressed ? ':' : ';';
                changed = true;
            }
            else if (key == Key.Apostrophe)
            {
                focused.Value += shiftPressed ? '"' : '\'';
                changed = true;
            }
            else if (key == Key.Comma)
            {
                focused.Value += shiftPressed ? '<' : ',';
                changed = true;
            }
            else if (key == Key.Period)
            {
                focused.Value += shiftPressed ? '>' : '.';
                changed = true;
            }
            else if (key == Key.Slash)
            {
                focused.Value += shiftPressed ? '?' : '/';
                changed = true;
            }
            else if (key == Key.GraveAccent)
            {
                focused.Value += shiftPressed ? '~' : '`';
                changed = true;
            }
            else if (key == Key.Backspace)
            {
                if (focused.Value.Length > 0)
                {
                    focused.Value = focused.Value.Substring(0, focused.Value.Length - 1);
                    changed = true;
                }
            }

            if (changed)
            {
                _uiOverlay.RefreshUI();
            }
        }

        public void SwitchMenu(string menuName)
        {
            string htmlPath = _modManager.ResolvePath($"{menuName}.html");
            if (htmlPath != null && File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath), Path.GetDirectoryName(htmlPath) ?? "");
                _controlContext.GetWindowSize(_window, out int w, out int h);
                _uiOverlay.RecomputeLayout(w, h);
            }
            else
            {
                Console.WriteLine($"MenuPanel: Failed to load menu {menuName}");
            }
        }
    }
}