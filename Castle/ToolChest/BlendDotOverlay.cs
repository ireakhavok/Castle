using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using System;
using System.Numerics;

namespace ToolChest
{
    public class BlendDotOverlay : ICustomOverlay
    {
        private readonly AnimationBlendPanel _panel;

        public BlendDotOverlay(AnimationBlendPanel panel)
        {
            _panel = panel;
        }

        // Matches ICustomOverlay.Draw exactly (UIQuadRenderer first)
        public void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight)
        {
            var gridElem = _panel._uiOverlay.FindElementById("blendGrid");
            if (gridElem == null || gridElem.ComputedWidth <= 0 || gridElem.ComputedHeight <= 0) return;

            float gx = gridElem.ComputedPosition.X;
            float gy = gridElem.ComputedPosition.Y;
            float gw = gridElem.ComputedWidth;
            float gh = gridElem.ComputedHeight;

            // Current blend point (green)
            float cx = Math.Clamp(gx + ((_panel._currentBlendPoint.X + 1f) / 2f * gw), gx, gx + gw);
            float cy = Math.Clamp(gy + ((_panel._currentBlendPoint.Y + 1f) / 2f * gh), gy, gy + gh);

            // FIXED: Draw white outline FIRST, then colored fill on top
            quadRenderer.DrawQuad(cx - 7, cy - 7, 14, 14, new Vector4(1f, 1f, 1f, 1f), panelWidth, panelHeight);
            quadRenderer.DrawQuad(cx - 6, cy - 6, 12, 12, new Vector4(0.29f, 0.87f, 0.5f, 1f), panelWidth, panelHeight);

            // Clip nodes (orange)
            foreach (var clip in _panel._currentStack.Clips)
            {
                float px = Math.Clamp(gx + ((clip.BlendCoordinate.X + 1f) / 2f * gw), gx, gx + gw);
                float py = Math.Clamp(gy + ((clip.BlendCoordinate.Y + 1f) / 2f * gh), gy, gy + gh);

                quadRenderer.DrawQuad(px - 6, py - 6, 12, 12, new Vector4(1f, 1f, 1f, 1f), panelWidth, panelHeight);
                quadRenderer.DrawQuad(px - 5, py - 5, 10, 10, new Vector4(0.96f, 0.62f, 0.04f, 1f), panelWidth, panelHeight);
            }
        }
    }
}