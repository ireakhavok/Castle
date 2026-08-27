// Folder: SiegeEngine/Core/Rendering
// File: ChromeRenderer.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI;
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
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

            _quadRenderer.EnsureUIState();

            // Title bar + close button
            owner.chrome.Render(_quadRenderer, panelWidth, panelHeight);

            // BORDERS (moved here for atomic isolation - this was the remaining broken part)
            float bw = 2f;
            Vector4 bc = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            _quadRenderer.DrawQuad(0, 0, bw, panelHeight, bc, panelWidth, panelHeight);
            _quadRenderer.DrawQuad(panelWidth - bw, 0, bw, panelHeight, bc, panelWidth, panelHeight);
            _quadRenderer.DrawQuad(0, panelHeight - bw, panelWidth, bw, bc, panelWidth, panelHeight);
            _quadRenderer.DrawQuad(0, 0, panelWidth, 1.5f, bc, panelWidth, panelHeight);

            _quadRenderer.FinishDraw();
        }

        public void Dispose()
        {
            (_quadRenderer as IDisposable)?.Dispose();
        }
    }
}