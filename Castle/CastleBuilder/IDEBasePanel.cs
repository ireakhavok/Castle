// Folder: CastleBuilder
// File: IDEBasePanel.cs
using CastleBuilder.Events;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.IO;
using System.Numerics;

namespace CastleBuilder
{
    public class IDEBasePanel : BasePanel
    {
        private class IDEUIOverlay : UIOverlay
        {
            private readonly EventBus _eventBus;

            public IDEUIOverlay(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
                : base(renderContext, controlContext, window, eventBus)
            {
                _eventBus = eventBus;
                _eventBus.Subscribe<ContextChangedEvent>(OnContextChanged);
            }

            private void OnContextChanged(ContextChangedEvent evt)
            {
                string context = evt.Context ?? "Scene Editor";
                Console.WriteLine($"[IDE Menu] Context changed to: {context}");
                RefreshMenuForContext(context);
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

            // === FINAL FIX FOR DATA-HOOK DROPDOWNS ===
            // We close the dropdown AND force IsHover = false on the parent NavLiElement.
            // This prevents UpdateHover from re-opening the dropdown on the next frame
            // (the mouse is still physically over the top-level nav item after the click).
            protected override void HandleDataHook(string hook)
            {
                CloseAllOpenNavDropdowns();
                RefreshUI();

                base.HandleDataHook(hook);
            }

            private void CloseAllOpenNavDropdowns()
            {
                var navLis = FindElementsByTag("li")
                    .Where(e => e is NavLiElement nav && nav.IsNavDropdownParent())
                    .Cast<NavLiElement>()
                    .ToList();

                foreach (var nav in navLis)
                {
                    nav.CloseDropdown();
                    nav.IsHover = false;   // critical: stops UpdateHover from re-showing the dropdown

                    var dropdownUl = nav.Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
                    if (dropdownUl != null)
                    {
                        dropdownUl.Style.Display = "none";
                    }
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
            // Size set before base.Init() eliminates the single-frame full-screen flicker
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            Size = new Vector2(winW, 28f);
            Position = Vector2.Zero;

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