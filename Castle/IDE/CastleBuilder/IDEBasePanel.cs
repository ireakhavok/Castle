using CastleBuilder.Events;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Systems;
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

            public void SeedMenus(string context)
            {
                RefreshMenuForContext(context ?? "Scene Editor");
            }

            private void RefreshMenuForContext(string context)
            {
                Console.WriteLine($"[IDE Menu] Top menu updated for context: {context}");
                PopulateBladeWindows(context);
                MarkActiveBlade(context);
            }

            private static string BladeItem(string hook, string label)
            {
                return $"<li class=\"nav-dropdown-item\" data-hook=\"{hook}\">{label}</li>";
            }

            private static string BladeWindowsHtml(string context)
            {
                context = context ?? "Scene Editor";
                if (string.Equals(context, "Terrain", StringComparison.OrdinalIgnoreCase))
                    return BladeItem("CastleBuilder.MenuCommands.OpenTerrain", "Terrain Creator")
                         + BladeItem("CastleBuilder.MenuCommands.OpenHierarchy", "Hierarchy")
                         + BladeItem("CastleBuilder.MenuCommands.OpenProperties", "Properties");
                if (string.Equals(context, "Animator", StringComparison.OrdinalIgnoreCase))
                    return BladeItem("CastleBuilder.MenuCommands.OpenAnimation", "Animation Viewer")
                         + BladeItem("CastleBuilder.MenuCommands.OpenAnimationBlend", "Animation Blend")
                         + BladeItem("CastleBuilder.MenuCommands.OpenAnimationTimeline", "Animation Timeline")
                         + BladeItem("CastleBuilder.MenuCommands.OpenHierarchy", "Hierarchy")
                         + BladeItem("CastleBuilder.MenuCommands.OpenProperties", "Properties");
                if (string.Equals(context, "Workshop", StringComparison.OrdinalIgnoreCase))
                    return BladeItem("CastleBuilder.MenuCommands.OpenScriptsPanel", "Scripts")
                         + BladeItem("ToolChest.ConsolePanel.Open", "Console")
                         + BladeItem("CastleBuilder.MenuCommands.OpenPlayHost", "Play Host");
                if (string.Equals(context, "Runtime", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(context, "Runtime Gameplay", StringComparison.OrdinalIgnoreCase))
                    return BladeItem("CastleBuilder.MenuCommands.OpenRuntime", "Runtime")
                         + BladeItem("CastleBuilder.MenuCommands.OpenPlayHost", "Play Host")
                         + BladeItem("ToolChest.ConsolePanel.Open", "Console");
                return BladeItem("CastleBuilder.MenuCommands.OpenEditorScene", "Editor Scene")
                     + BladeItem("CastleBuilder.MenuCommands.OpenHierarchy", "Hierarchy")
                     + BladeItem("CastleBuilder.MenuCommands.OpenProperties", "Properties")
                     + BladeItem("CastleBuilder.MenuCommands.OpenAssetBrowser", "Asset Browser");
            }

            private void PopulateBladeWindows(string context)
            {
                var list = FindElementById("blade-windows");
                if (list == null) return;
                var parsed = new HtmlParser().Parse("<ul id=\"blade-windows\">" + BladeWindowsHtml(context) + "</ul>");
                list.Children.Clear();
                if (parsed == null) return;
                var source = parsed;
                if (!string.Equals(parsed.Tag, "ul", StringComparison.OrdinalIgnoreCase) && parsed.Children.Count == 1)
                    source = parsed.Children[0];
                foreach (var child in source.Children.ToList())
                {
                    child.Parent = list;
                    list.Children.Add(child);
                }
                RefreshUI();
            }

            private static string ElementClass(HtmlElement el)
            {
                if (el?.Attributes != null && el.Attributes.TryGetValue("class", out string cls))
                    return cls ?? "";
                return "";
            }

            private static string ElementLabel(HtmlElement el)
            {
                if (el == null) return "";
                var parts = new System.Collections.Generic.List<string>();
                void Walk(HtmlElement n)
                {
                    if (n is TextElement te && !string.IsNullOrWhiteSpace(te.Content))
                        parts.Add(te.Content.Trim());
                    if (n?.Children == null) return;
                    for (int i = 0; i < n.Children.Count; i++)
                        Walk(n.Children[i]);
                }
                Walk(el);
                return string.Join(" ", parts).Trim();
            }

            private void MarkActiveBlade(string context)
            {
                string want = context ?? "Scene Editor";
                if (string.Equals(want, "Runtime Gameplay", StringComparison.OrdinalIgnoreCase))
                    want = "Runtime";
                var blades = FindElementsByTag("div");
                foreach (var el in blades)
                {
                    string cls = ElementClass(el);
                    if (cls.IndexOf("context-blade", StringComparison.Ordinal) < 0) continue;
                    bool on = string.Equals(ElementLabel(el), want, StringComparison.OrdinalIgnoreCase);
                    var tokens = new System.Collections.Generic.List<string>(
                        cls.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    if (on && !tokens.Contains("active")) tokens.Add("active");
                    if (!on) tokens.RemoveAll(t => t == "active");
                    el.Attributes["class"] = string.Join(" ", tokens);
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
        public override bool WantsContinuousUpdate => true;

        public IDEBasePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            AllowDragging = false;
            DockState = DockState.Tabbed;
            IsModal = false;
            RenderOrder = 1000;

            BaseWidth = 0f;
            BaseHeight = 0f;
            Keystone.EditorHistory.Current.Initialize(eventBus);
            Keystone.EditorHistory.Current.BindInput(controlContext, window);
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
            if (_uiOverlay is IDEUIOverlay ideUi)
                ideUi.SeedMenus(CompanionLayoutHelper.CurrentContext);

            CompanionLayoutHelper.Bind(_renderContext, _controlContext, _window, _eventBus);
            MusicPlayerPanel.Open(_renderContext, _controlContext, _window, _eventBus);
        }

        private void GetNdcViewport(out int vw, out int vh)
        {
            _controlContext.GetWindowSize(_window, out vw, out vh);
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            Keystone.EditorHistory.Current.BindInput(_controlContext, _window);
            Keystone.EditorHistory.Current.Tick();

            GetNdcViewport(out int winW, out int winH);
            if ((int)Size.X != winW)
                Size = new Vector2(winW, 28f);

            Vector2 relMousePos = absMousePos - Position;
            bool overBar = IsOverMenuBar(absMousePos);

            // Pixel hover first. Do not call base.Update — it runs overlay.Update
            // with Size.Y=28 and maps the File label onto the whole window.
            SyncNavHovers(relMousePos);
            bool overPopup = IsOverAnyPopup(relMousePos);

            if (!overBar && !overPopup)
            {
                ReleaseAllNavHovers();
                _lastFrameHadOpenDropdown = false;
                return;
            }

            PanelManager.Current?.ForceDrawOverThisFrame(this);

            if (_uiOverlay != null)
            {
                _uiOverlay.PanelWidth = winW;
                _uiOverlay.Update(deltaTime, relMousePos, mouseDown, winW, winH);
                SyncNavHovers(relMousePos);
            }

            _lastFrameHadOpenDropdown = overPopup || AnyDropdownOpen();
        }

        private void SyncNavHovers(Vector2 relMousePos)
        {
            if (_uiOverlay == null) return;
            foreach (var nav in _uiOverlay.FindElementsByTag("li").OfType<NavLiElement>().Where(n => n.IsNavDropdownParent()))
                nav.UpdateHover(relMousePos, Size.X, Size.Y);
        }

        private bool IsOverAnyPopup(Vector2 relMousePos)
        {
            if (_uiOverlay == null) return false;
            return _uiOverlay.FindElementsByTag("li").OfType<NavLiElement>()
                .Any(nav => nav.IsNavDropdownParent() && nav.ContainsPointer(relMousePos));
        }

        private bool AnyDropdownOpen()
        {
            if (_uiOverlay == null) return false;
            return _uiOverlay.FindElementsByTag("li").OfType<NavLiElement>()
                .Any(nav => nav.IsNavDropdownParent() && nav.IsDropdownOpen);
        }

        private void ReleaseAllNavHovers()
        {
            if (_uiOverlay == null) return;
            foreach (var nav in _uiOverlay.FindElementsByTag("li").OfType<NavLiElement>().Where(n => n.IsNavDropdownParent()))
                nav.ReleaseHover();
        }

        private static bool IsOverMenuBar(Vector2 absMousePos)
        {
            return absMousePos.Y >= 0f && absMousePos.Y <= 28f && absMousePos.X >= 0f;
        }

        public override void Render()
        {
            if (!Visible) return;
            GetNdcViewport(out int winW, out int winH);
            if (_lastW != winW)
            {
                _lastW = winW;
                _lastH = 28;
                Size = new Vector2(winW, 28f);
                _uiOverlay.PanelWidth = winW;
                _uiOverlay.PanelHeight = 28f;
                _uiOverlay.RefreshUI();
            }
            _uiOverlay.PanelWidth = winW;
            _uiOverlay.PanelHeight = winH;
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _uiOverlay.Render();
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _uiOverlay.PanelHeight = 28f;
        }

        public override bool IsMouseOver(Vector2 absMousePos)
        {
            if (IsOverMenuBar(absMousePos))
                return true;
            Vector2 relMousePos = absMousePos - Position;
            if (IsOverAnyPopup(relMousePos))
            {
                PanelManager.Current?.ForceDrawOverThisFrame(this);
                return true;
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