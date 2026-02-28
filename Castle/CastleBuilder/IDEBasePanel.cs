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

namespace CastleBuilder
{
    public class IDEBasePanel : BasePanel
    {
        private NavElement _navBar;
        private const float NavBarHeight = 20f;

        public IDEBasePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            AllowDragging = false;
            DockState = DockState.Tabbed;
            IsModal = false;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new UIOverlay(_renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            _navBar = new NavElement();
            _navBar.SetupIDEMenu();

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IDE_UI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"[IDEBasePanel] ERROR: IDE_UI.html not found at {htmlPath}. Please place the file in the executable directory.");
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
            Vector2 contentMouse = new Vector2(absMousePos.X, absMousePos.Y - NavBarHeight);
            base.Update(deltaTime, contentMouse, mouseDown, mousePressed, mouseReleased, scrollDelta);
        }

        public override void Render()
        {
            if (!Visible) return;
            base.Render();
        }

        public override void OnPanelResize(float w, float h)
        {
            base.OnPanelResize(w, h);
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new IDEBasePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }
    }
}