// Folder: SiegeEngine.Core.UI.Elements
// File: LiElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
{
    public class LiElement : HtmlElement
    {
        public LiElement()
        {
            Tag = "li";
        }

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            float maxWidth = 0f;
            float totalHeight = 0f;
            string foundText = "";
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            foreach (var child in Children)
            {
                queue.Enqueue(child);
            }
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                if (elem is TextElement textElem && !string.IsNullOrWhiteSpace(textElem.Content))
                {
                    foundText = textElem.Content.Trim();
                    Vector2 textSize = textRenderer.GetTextSize(foundText, fs, elem.Style.FontFamily ?? Style.FontFamily ?? "Arial");
                    maxWidth = Math.Max(maxWidth, textSize.X);
                    totalHeight = Math.Max(totalHeight, textSize.Y);
                }
                foreach (var c in elem.Children)
                {
                    queue.Enqueue(c);
                }
            }

            foreach (var child in Children.Where(c => c.GetEffectiveDisplay() != "none"))
            {
                if (child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol")
                {
                    Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                    maxWidth = Math.Max(maxWidth, childSize.X);
                    totalHeight += childSize.Y;
                }
            }
            Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float finalWidth = maxWidth + pad.W + pad.Y + borderW.W + borderW.Y;
            float finalHeight = totalHeight + pad.X + pad.Z + borderW.X + borderW.Z;
            if (float.IsNaN(finalWidth) || finalWidth < 30f) finalWidth = 120f;
            if (float.IsNaN(finalHeight) || finalHeight < 20f) finalHeight = 28f;
            return new Vector2(finalWidth, finalHeight);
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }

        // === SIMPLE FULL-ROW HOVER FOR TREE NODES ONLY ===
        // This makes the entire <li class="node"> highlight when the mouse is anywhere over it
        // (including the label text, toggle, and even the children area).
        // No header-only restriction — this is what the CSS .node:hover expects.
        public override bool UpdateHover(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none") return false;

            // Let children claim hover first (nested rows must still work independently)
            bool anyChildHovered = false;
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i].UpdateHover(mousePos, viewportWidth, viewportHeight))
                {
                    anyChildHovered = true;
                }
            }

            // If a child (nested row) is hovered, this parent li does NOT get hover
            if (anyChildHovered)
            {
                if (IsHover)
                {
                    IsHover = false;
                }
                return true;
            }

            // No child hovered → full-row hover for this li.node
            float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, ComputedHeight, ComputedFullTransform, viewportWidth, viewportHeight);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                float nx = ndc[k * 2];
                float ny = ndc[k * 2 + 1];
                minX = Math.Min(minX, nx);
                maxX = Math.Max(maxX, nx);
                minY = Math.Min(minY, ny);
                maxY = Math.Max(maxY, ny);
            }
            float mx = 2 * mousePos.X / viewportWidth - 1;
            float my = 1 - 2 * mousePos.Y / viewportHeight;
            bool over = !(mx < minX || mx > maxX || my < minY || my > maxY);

            bool changed = false;
            if (over && !IsHover)
            {
                IsHover = true;
                changed = true;
            }
            else if (!over && IsHover)
            {
                IsHover = false;
                changed = true;
            }

            return over;
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            bool hit = false;
            if (ComputedWidth > 0 && ComputedHeight > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, ComputedHeight, ComputedFullTransform, viewportWidth, viewportHeight);
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                for (int k = 0; k < 4; k++)
                {
                    float nx = ndc[k * 2];
                    float ny = ndc[k * 2 + 1];
                    minX = Math.Min(minX, nx);
                    maxX = Math.Max(maxX, nx);
                    minY = Math.Min(minY, ny);
                    maxY = Math.Max(maxY, ny);
                }
                float mx = 2 * mousePos.X / viewportWidth - 1;
                float my = 1 - 2 * mousePos.Y / viewportHeight;
                hit = !(mx < minX || mx > maxX || my < minY || my > maxY);
            }
            if (hit)
            {
                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    if (Children[i].HandleClick(mousePos, viewportWidth, viewportHeight)) return true;
                }
            }
            return hit;
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }
    }
}