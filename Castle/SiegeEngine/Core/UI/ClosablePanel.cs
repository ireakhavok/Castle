// Folder: SiegeEngine.Core.UI
// File: ClosablePanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Definitions;
using System;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    /// <summary>
    /// Base class for panels that should have a close "X" button in the title bar.
    /// Only panels in MapRoom, ReadingChamber, and ToolChest will inherit from this.
    /// Runtime/game panels continue inheriting directly from BasePanel (no X ever).
    /// Title bar height and layout are unchanged.
    /// </summary>
    public abstract class ClosablePanel : BasePanel
    {
        protected ClosablePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            if (!Visible) return;

            // Close button click detection (far right of existing title bar)
            if (mousePressed)
            {
                float closeX = Position.X + Size.X - 24f;
                bool overClose = absMousePos.X >= closeX && absMousePos.X <= Position.X + Size.X &&
                                 absMousePos.Y >= Position.Y && absMousePos.Y <= Position.Y + TitleHeight;

                if (overClose)
                {
                    _eventBus.Publish(new ClosePanelEvent(this));
                    return;
                }
            }

            // Normal BasePanel behavior
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
        }

        public override void Render()
        {
            if (!Visible) return;

            // Draw everything from BasePanel first (title bar background, content, borders)
            base.Render();

            // === CLEAN BLACK X - drawn LAST to guarantee it's on top ===
            float closeX = Size.X - 23f;
            float closeY = 4f;
            float len = 14f;
            float thick = 2f;

            Vector4 black = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

            // Diagonal 1: top-left to bottom-right (positive width/height)
            _quadRenderer.DrawQuad(closeX + 2, closeY + 2, len - 3, thick, black, Size.X, Size.Y);

            // Diagonal 2: top-right to bottom-left (positive width/height)
            _quadRenderer.DrawQuad(closeX + len - 1, closeY + 2, -(len - 3), thick, black, Size.X, Size.Y);
        }
    }
}