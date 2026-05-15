// Folder: SiegeEngine/Core/Rendering
// File: LayeredUIRenderer.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.UI;
using System.Numerics;

namespace SiegeEngine.Core.Rendering
{
    public sealed class LayeredUIRenderer
    {
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly UIQuadRenderer _quadRenderer;

        public LayeredUIRenderer(IRenderContext renderContext, IControlContext controlContext, UIQuadRenderer quadRenderer)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _quadRenderer = quadRenderer;
        }

        public void RenderPanel(BasePanel panel)
        {
            if (panel == null || !panel.Visible) return;

            _controlContext.GetWindowSize(panel.WindowHandle, out int winW, out int winH);

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);

            int fullX = (int)panel.Position.X;
            int fullY = winH - (int)(panel.Position.Y + panel.Size.Y);
            uint fullW = (uint)panel.Size.X;
            uint fullH = (uint)panel.Size.Y;

            _renderContext.Enable(_renderContext.Enums.ScissorTest);
            _renderContext.Scissor(fullX, fullY, fullW, fullH);
            _renderContext.Viewport(fullX, fullY, fullW, fullH);

            // Backgrounds now receive scroll matrix from UIOverlay.RenderBackgrounds
            if (panel._uiOverlay != null)
            {
                panel._uiOverlay.RenderBackgrounds(fullW, fullH);
            }

            panel.RenderContentLayer();

            // Draw registered custom overlays while panel stencil is still active
            foreach (var overlay in panel.CustomOverlays)
            {
                overlay.Draw(panel.QuadRenderer, panel.Size.X, panel.Size.Y);
            }

            _renderContext.Scissor(fullX, fullY, fullW, fullH);
            _renderContext.Viewport(fullX, fullY, fullW, fullH);

            _renderContext.Disable(_renderContext.Enums.DepthTest);

            if (panel.HasTitleBar && panel.chrome != null)
            {
                panel.chrome.Render(panel.QuadRenderer, panel.Size.X, panel.Size.Y);
            }

            float bw = 2f;
            Vector4 bc = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            _quadRenderer.DrawQuad(0, 0, bw, panel.Size.Y, bc, panel.Size.X, panel.Size.Y);
            _quadRenderer.DrawQuad(panel.Size.X - bw, 0, bw, panel.Size.Y, bc, panel.Size.X, panel.Size.Y);
            _quadRenderer.DrawQuad(0, panel.Size.Y - bw, panel.Size.X, bw, bc, panel.Size.X, panel.Size.Y);
            _quadRenderer.DrawQuad(0, 0, panel.Size.X, 1.5f, bc, panel.Size.X, panel.Size.Y);

            // Restore full window
            _renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            _renderContext.Viewport(0, 0, (uint)winW, (uint)winH);
            _renderContext.Disable(_renderContext.Enums.ScissorTest);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }
    }
}