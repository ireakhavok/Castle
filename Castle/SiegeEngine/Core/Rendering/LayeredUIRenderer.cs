// Folder: SiegeEngine/Core/Rendering
// File: LayeredUIRenderer.cs
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using System.Numerics;

namespace SiegeEngine.Core.Rendering
{
    public sealed class LayeredUIRenderer
    {
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly UIQuadRenderer _quadRenderer;
        private readonly ChromeRenderer _chromeRenderer; // dedicated, isolated chrome path

        public LayeredUIRenderer(IRenderContext renderContext, IControlContext controlContext, UIQuadRenderer quadRenderer)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _quadRenderer = quadRenderer;
            _chromeRenderer = new ChromeRenderer(renderContext);
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

            Vector4 panelBgColor = new Vector4(0.12f, 0.12f, 0.12f, 1f);
            if (panel._uiOverlay?._uiRoot != null)
            {
                var rootStyle = panel._uiOverlay._uiRoot.Style;
                if (rootStyle.BackgroundColor != Vector4.Zero)
                {
                    panelBgColor = rootStyle.BackgroundColor;
                }
            }

            _quadRenderer.DrawQuad(0, 0, panel.Size.X, panel.Size.Y, panelBgColor, panel.Size.X, panel.Size.Y);

            if (panel._uiOverlay != null)
            {
                panel._uiOverlay.RenderBackgrounds(fullW, fullH);
            }

            panel.RenderContentLayer();

            foreach (var overlay in panel.CustomOverlays)
            {
                overlay.Draw(panel.QuadRenderer, panel.Size.X, panel.Size.Y);
            }

            _renderContext.Scissor(fullX, fullY, fullW, fullH);
            _renderContext.Viewport(fullX, fullY, fullW, fullH);

            // CHROME RENDER IS NOW ISOLATED AND ALWAYS LAST (independent of content, NDC, live-state, or any prior draws)
            if (panel.HasTitleBar && panel.chrome != null)
            {
                _chromeRenderer.RenderPanelChrome(panel, panel.Size.X, panel.Size.Y);
            }

            float bw = 2f;
            Vector4 bc = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            _quadRenderer.DrawQuad(0, 0, bw, panel.Size.Y, bc, panel.Size.X, panel.Size.Y);
            _quadRenderer.DrawQuad(panel.Size.X - bw, 0, bw, panel.Size.Y, bc, panel.Size.X, panel.Size.Y);
            _quadRenderer.DrawQuad(0, panel.Size.Y - bw, panel.Size.X, bw, bc, panel.Size.X, panel.Size.Y);
            _quadRenderer.DrawQuad(0, 0, panel.Size.X, 1.5f, bc, panel.Size.X, panel.Size.Y);

            _renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            _renderContext.Viewport(0, 0, (uint)winW, (uint)winH);
            _renderContext.Disable(_renderContext.Enums.ScissorTest);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public void Dispose()
        {
            _chromeRenderer?.Dispose();
        }
    }
}