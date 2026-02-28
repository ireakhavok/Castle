// Folder: CastleBuilder
// File: IDEBasePanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
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

            string ideHtml = @"
<div style='display:flex;flex-direction:column;height:100%;width:100%;'>
  <nav id='ide-menu-bar' style='height:20px;background:#121212;color:#ddd;display:flex;align-items:center;padding:0 8px;font-size:13px;gap:16px;border-bottom:1px solid #444;user-select:none;'></nav>
  <div id='ide-content' style='flex:1;overflow:auto;'></div>
</div>";
            _uiOverlay.LoadUI(ideHtml);
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
    }
}