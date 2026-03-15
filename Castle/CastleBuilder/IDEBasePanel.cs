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
        }

        public IDEBasePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            AllowDragging = false;
            DockState = DockState.Tabbed;
            IsModal = false;
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

            // Force full refresh on every frame for the top menu bar (ensures hover works on first mouse move)
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();                     // <-- this line fixes the "only after click" problem

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