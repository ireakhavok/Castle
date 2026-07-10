// Folder: SiegeEngine/Core/Rendering
// File: ChromeRenderer.cs
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using System;
using System.Numerics;

namespace SiegeEngine.Core.Rendering
{
    public sealed class ChromeRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly UIQuadRenderer _quadRenderer;

        public ChromeRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext;
            _quadRenderer = new UIQuadRenderer(renderContext);
        }

        public void RenderPanelChrome(BasePanel owner, float panelWidth, float panelHeight)
        {
            if (owner == null || !owner.HasTitleBar || owner.chrome == null) return;

            // FUTURE-PROOF: Completely isolated UI state for every chrome render (no bleed from content, NDC, terrain, live-state, or previous panels)
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);

            // Title bar + close button
            owner.chrome.Render(_quadRenderer, panelWidth, panelHeight);

            // BORDERS (moved here for atomic isolation - this was the remaining broken part)
            float bw = 2f;
            Vector4 bc = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            _quadRenderer.DrawQuad(0, 0, bw, panelHeight, bc, panelWidth, panelHeight);
            _quadRenderer.DrawQuad(panelWidth - bw, 0, bw, panelHeight, bc, panelWidth, panelHeight);
            _quadRenderer.DrawQuad(0, panelHeight - bw, panelWidth, bw, bc, panelWidth, panelHeight);
            _quadRenderer.DrawQuad(0, 0, panelWidth, 1.5f, bc, panelWidth, panelHeight);

            // Explicit cleanup for next draw (prevents any vertex attrib / shader / buffer state leakage)
            _renderContext.BindVertexArray(0);
            _renderContext.DisableVertexAttribArray(0);
            _renderContext.DisableVertexAttribArray(1);
        }

        public void Dispose()
        {
            (_quadRenderer as IDisposable)?.Dispose();
        }
    }
}