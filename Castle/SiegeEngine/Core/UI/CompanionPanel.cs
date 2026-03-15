// Folder: SiegeEngine.Core.UI
// File: CompanionPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public class CompanionPanel : ClosablePanel
    {
        public IPanel AttachedTo { get; set; } = null;
        public bool IsResizable { get; set; } = true; // for future splitter support

        public CompanionPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            IsModal = false;
            Scaling = ScalingMode.BestFit;
            BaseWidth = 320f;   // much smaller default
            BaseHeight = 450f;
            AllowDragging = true;
            DockState = DockState.Floating;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new UIOverlay(_renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            // Force small size on first init
            Size = new Vector2(BaseWidth, BaseHeight);
            OnPanelResize(BaseWidth, BaseHeight);
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            // If attached to a parent, position relative to parent (future stage 2)
            if (AttachedTo != null)
            {
                // For now: stay floating but remember attachment
                // Real attachment logic in next stage
            }
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
        }
    }
}