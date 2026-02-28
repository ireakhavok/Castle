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
        private const float NavBarHeight = 28f; // Matches the new menu bar height

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

                string fullTypeName = $"{ns}.{className}";
                Type type = Type.GetType(fullTypeName) ?? Type.GetType(fullTypeName + ", CastleBuilder");

                if (type == null)
                {
                    Console.WriteLine($"[IDE Menu] Type not found: {fullTypeName}");
                    return;
                }

                var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
                if (method != null)
                {
                    try
                    {
                        method.Invoke(null, new object[] { _renderContext, _controlContext, _window, _eventBus });
                        Console.WriteLine($"[IDE Menu] Successfully opened panel: {hook}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[IDE Menu] Error calling {hook}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[IDE Menu] Static method {methodName} not found on {fullTypeName}");
                }
            }
        }

        public IDEBasePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            AllowDragging = true;     // Keep the standard title bar for dragging
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

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new IDEBasePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }
    }
}