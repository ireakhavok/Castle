// Folder: CastleBuilder
// File: IDEBasePanel.cs
using CastleBuilder.Events;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.IO;
using System.Linq;
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
                    Console.WriteLine("  Panels menu now shows: Hierarchy, Properties, Asset Browser, Scene List, Load Game Scene, Play Game, Export Game");
                }
                else if (context == "Configuration")
                {
                    Console.WriteLine("  Panels menu now shows: Project Settings, Mod Manager, Server Rules, Blueprint Governance");
                }
                else if (context == "Runtime Gameplay")
                {
                    Console.WriteLine("  [Runtime Gameplay] Editor panels/dropdowns hidden • Runtime UI + Sandbox scene with cached Level/terrain/entities/player/fly cam active");
                }
            }

            protected override void HandleDataHook(string hook)
            {
                CloseAllOpenNavDropdowns();
                RefreshUI();
                if (hook == "PlayGame")
                {
                    MenuCommands.PlayGame(_renderContext, _controlContext, _window, _eventBus);
                }
                else if (hook == "ExportGame")
                {
                    MenuCommands.ExportGame(_renderContext, _controlContext, _window, _eventBus);
                }
                else if (hook == "BuildScripts")
                {
                    MenuCommands.BuildScripts(_renderContext, _controlContext, _window, _eventBus);
                }
                else if (hook == "OpenScriptsPanel")
                {
                    MenuCommands.OpenScriptsPanel(_renderContext, _controlContext, _window, _eventBus);
                }
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

        private bool _lastFrameHadOpenDropdown = false;

        public IDEBasePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            AllowDragging = false;
            DockState = DockState.Tabbed;
            IsModal = false;
            RenderOrder = 1000;

            BaseWidth = 0f;
            BaseHeight = 0f;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new IDEUIOverlay(_renderContext, _controlContext, _window, _eventBus);
        }

        public override void Init()
        {
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            Position = Vector2.Zero;

            base.Init();

            Size = new Vector2(winW, 28f);

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
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);

            if (PanelManager.Current?.GetTopmostPanelAt(absMousePos) != this)
                return;

            Vector2 relMousePos = absMousePos - Position;

            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;

            _uiOverlay.Update(deltaTime, relMousePos, mouseDown, Size.X, Size.Y);

            bool hasOpenDropdownNow = UpdateOpenDropdownHovers(relMousePos);

            if (hasOpenDropdownNow && !_lastFrameHadOpenDropdown)
            {
                _uiOverlay.RefreshUI();
            }

            _lastFrameHadOpenDropdown = hasOpenDropdownNow;
        }

        private bool UpdateOpenDropdownHovers(Vector2 relMousePos)
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
                    dropdownUl.UpdateHover(relMousePos, (int)Size.X, (int)Size.Y);
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
            if (absMousePos.X >= Position.X && absMousePos.X <= Position.X + Size.X &&
                absMousePos.Y >= Position.Y && absMousePos.Y <= Position.Y + 28f)
                return true;

            if (_uiOverlay != null)
            {
                Vector2 relMousePos = absMousePos - Position;

                var navLis = _uiOverlay.FindElementsByTag("li")
                    .Where(e => e is NavLiElement nav && nav.IsNavDropdownParent())
                    .Cast<NavLiElement>();

                foreach (var nav in navLis)
                {
                    if (nav.IsDropdownOpen)
                    {
                        PanelManager.Current?.ForceDrawOverThisFrame(this);

                        bool hit = nav.UpdateHover(relMousePos, (int)Size.X, (int)Size.Y);

                        if (hit) return true;
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