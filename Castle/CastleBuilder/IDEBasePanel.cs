// Folder: CastleBuilder
// File: IDEBasePanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using CastleBuilder.Events;   // for ContextChangedEvent

namespace CastleBuilder
{
    public class IDEBasePanel : BasePanel
    {
        private class IDEUIOverlay : UIOverlay
        {
            private readonly EventBus _eventBus;

            public IDEUIOverlay(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
                : base(renderContext, controlContext, window)
            {
                _eventBus = eventBus;
                _eventBus.Subscribe<ContextChangedEvent>(OnContextChanged);
            }

            protected override void HandleDataHook(string hook)
            {
                Console.WriteLine($"[IDE Menu] Clicked: {hook}");
                var parts = hook.Split('.');
                if (parts.Length < 3) return;
                string ns = parts[0];
                string className = parts[1];
                string methodName = parts[2];
                Type type = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType($"{ns}.{className}");
                    if (type != null) break;
                }
                if (type == null)
                {
                    Console.WriteLine($"[IDE Menu] Type not found: {ns}.{className}");
                    return;
                }
                var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
                if (method != null)
                {
                    try
                    {
                        method.Invoke(null, new object[] { _renderContext, _controlContext, _window, _eventBus });
                        Console.WriteLine($"[IDE Menu] SUCCESS: Opened panel via {hook}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[IDE Menu] Error calling {hook}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[IDE Menu] Method '{methodName}' not found on {ns}.{className}");
                }
            }

            private void OnContextChanged(ContextChangedEvent evt)
            {
                string context = evt.Context ?? "Scene Editor";
                Console.WriteLine($"[IDE Menu] Context changed to: {context}");
                RefreshMenuForContext(context);
                // Visual blade highlighting is already handled by the inline JS in IDE_UI.html
            }

            private void RefreshMenuForContext(string context)
            {
                Console.WriteLine($"[IDE Menu] Top menu updated for context: {context}");

                if (context == "Terrain")
                {
                    Console.WriteLine("  Panels menu now shows: Terrain Creator, Sculpt, Brush Settings, Export Heightmap, Import GeoTIFF");
                }
                else if (context == "Animator")
                {
                    Console.WriteLine("  Panels menu now shows: Animation Viewer, Import FBX, Blend Editor, Preview in Scene, Animation List");
                }
                else if (context == "Scene Editor")
                {
                    Console.WriteLine("  Panels menu now shows: Hierarchy, Properties, Asset Browser, Scene List, Load Game Scene");
                }
                else if (context == "Configuration")
                {
                    Console.WriteLine("  Panels menu now shows: Project Settings, Mod Manager, Server Rules, Blueprint Governance");
                }
            }
        }

        public IDEBasePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            AllowDragging = false;
            DockState = DockState.Tabbed;
            IsModal = false;
            RenderOrder = 1000;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new IDEUIOverlay(_renderContext, _controlContext, _window, _eventBus);
        }

        public override void Init()
        {
            base.Init();
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            Size = new Vector2(winW, 28f);
            Position = Vector2.Zero;

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IDE_UI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"[IDEBasePanel] ERROR: IDE_UI.html not found at {htmlPath}");
                return;
            }
            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            if (!Visible) return;

            Vector2 relMousePos = absMousePos - Position;

            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            _uiOverlay.Update(deltaTime, relMousePos, mouseDown, Size.X, Size.Y);
        }

        public override void Render()
        {
            if (!Visible) return;
            if (_lastW != (int)Size.X || _lastH != (int)Size.Y)
            {
                _lastW = (int)Size.X;
                _lastH = (int)Size.Y;
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _uiOverlay.Render();
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new IDEBasePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }
    }
}