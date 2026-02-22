// Folder: SiegeEngine.UI
// File: MenuPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
namespace SiegeEngine.Core.UI
{
    public class MenuPanel : BasePanel
    {
        private class MenuUIOverlay : UIOverlay
        {
            private readonly MenuPanel _parent;
            private readonly ModManager _modManager;
            private readonly EventBus _eventBus;
            public MenuUIOverlay(MenuPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window, ModManager modManager, EventBus eventBus) : base(renderContext, controlContext, window)
            {
                _parent = parent;
                _modManager = modManager;
                _eventBus = eventBus;
            }
            protected override void HandleDataHook(string hook)
            {
                var parts = hook.Split('.');
                if (parts.Length > 2)
                {
                    string dllName = parts[0] + ".dll";
                    string typeName = string.Join(".", parts, 0, parts.Length - 1);
                    string methodName = parts[parts.Length - 1];
                    string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dllName);
                    Assembly ass = null;
                    if (File.Exists(dllPath))
                    {
                        try
                        {
                            ass = Assembly.LoadFrom(dllPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"MenuUIOverlay: Failed to load {dllPath}: {ex.Message}");
                        }
                    }
                    Type type = ass?.GetType(typeName) ?? Type.GetType(typeName);
                    if (type != null)
                    {
                        MethodInfo mi = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(IRenderContext), typeof(IControlContext), typeof(nint), typeof(EventBus) }, null);
                        if (mi != null)
                        {
                            mi.Invoke(null, new object[] { _renderContext, _controlContext, _window, _eventBus });
                        }
                        else
                        {
                            Console.WriteLine($"MenuUIOverlay: Failed to find static method {methodName} in type {typeName}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"MenuUIOverlay: Failed to find type {typeName}");
                    }
                    _eventBus.Publish(new ClosePanelEvent(_parent));
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
                    RefreshUI();
                }
                else
                {
                    Console.WriteLine($"MenuUIOverlay: Failed to resolve or find href path: {href}, tried {newPath}");
                }
            }
        }
        private readonly ModManager _modManager;
        private readonly string _initialHtmlPath;
        public MenuPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, ModManager modManager, string initialHtmlPath) : base(renderContext, controlContext, window, eventBus)
        {
            _modManager = modManager;
            _initialHtmlPath = initialHtmlPath;
            AllowDragging = false;
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new MenuUIOverlay(this, _renderContext, _controlContext, _window, _modManager, _eventBus);
        }
        public override void Init()
        {
            base.Init();
            if (File.Exists(_initialHtmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(_initialHtmlPath), Path.GetDirectoryName(_initialHtmlPath) ?? "");
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            else
            {
                Console.WriteLine($"MenuPanel: Initial HTML file not found at {_initialHtmlPath}");
            }
        }
        public void SwitchMenu(string menuName)
        {
            string htmlPath = _modManager.ResolvePath($"{menuName}.html");
            if (htmlPath != null && File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath), Path.GetDirectoryName(htmlPath) ?? "");
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            else
            {
                Console.WriteLine($"MenuPanel: Failed to load menu {menuName}");
            }
        }
        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
        }
        public override void OnPanelResize(float w, float h)
        {
            base.OnPanelResize(w, h);
        }
        public override void Dispose()
        {
            base.Dispose();
        }
    }
}