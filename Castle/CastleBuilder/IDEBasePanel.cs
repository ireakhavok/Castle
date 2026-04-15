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
using System.Linq;

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
                    nav.IsHover = false;

                    var dropdownUl = nav.Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
                    if (dropdownUl != null)
                    {
                        dropdownUl.Style.Display = "none";
                    }
                }
            }
        }

        private bool _lastFrameHadOpenDropdown = false;   // used to detect newly-opened dropdowns

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

            // Run the normal UI update first (this processes clicks that open the dropdown)
            _uiOverlay.Update(deltaTime, relMousePos, mouseDown, Size.X, Size.Y);

            // Now force immediate hover recalc on open dropdowns so CSS appears instantly
            bool hasOpenDropdownNow = UpdateOpenDropdownHovers(absMousePos);

            // If a dropdown just opened this frame, do a one-time RefreshUI to guarantee CSS is applied
            if (hasOpenDropdownNow && !_lastFrameHadOpenDropdown)
            {
                _uiOverlay.RefreshUI();
            }

            _lastFrameHadOpenDropdown = hasOpenDropdownNow;
        }

        /// <summary>
        /// Forces immediate hover recalc on every open dropdown subtree.
        /// Returns true if any dropdown is currently open.
        /// </summary>
        private bool UpdateOpenDropdownHovers(Vector2 absMousePos)
        {
            if (_uiOverlay == null) return false;

            bool anyOpen = false;

            var navLis = _uiOverlay.FindElementsByTag("li")
                .Where(e => e is NavLiElement nav && nav.IsNavDropdownParent())
                .Cast<NavLiElement>();

            foreach (var nav in navLis)
            {
                var dropdownUl = nav.Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
                if (dropdownUl != null && dropdownUl.GetEffectiveDisplay() != "none")
                {
                    anyOpen = true;

                    // Force parent to be hovered so the dropdown stays active
                    bool originalNavHover = nav.IsHover;
                    nav.IsHover = true;

                    // Force the dropdown UL itself to update hover (this propagates to all children)
                    dropdownUl.UpdateHover(absMousePos, (int)Size.X, (int)Size.Y);

                    nav.IsHover = originalNavHover;
                }
            }
            return anyOpen;
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

        public override bool IsMouseOver(Vector2 absMousePos)
        {
            if (base.IsMouseOver(absMousePos))
                return true;

            if (_uiOverlay != null)
            {
                var navLis = _uiOverlay.FindElementsByTag("li")
                    .Where(e => e is NavLiElement nav && nav.IsNavDropdownParent())
                    .Cast<NavLiElement>();

                foreach (var nav in navLis)
                {
                    var dropdownUl = nav.Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
                    if (dropdownUl != null && dropdownUl.GetEffectiveDisplay() != "none")
                    {
                        bool originalHover = nav.IsHover;
                        nav.IsHover = true;

                        bool hit = nav.UpdateHover(absMousePos, (int)Size.X, (int)Size.Y);

                        nav.IsHover = originalHover;

                        if (hit)
                            return true;
                    }
                }
            }
            return false;
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new IDEBasePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }
    }
}