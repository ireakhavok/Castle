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

        private float GetTreeNodeHeaderHeight()
        {
            // For tree nodes we only want hover/click on the actual row (toggle + label), NOT the full subtree height
            float headerHeight = 0f;
            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol") break; // children list starts here
                headerHeight = Math.Max(headerHeight, child.ComputedHeight);
            }
            // fallback to a sensible row height if layout not yet run
            if (headerHeight < 1f) headerHeight = 28f;
            return headerHeight;
        }

        public override bool UpdateHover(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none") return false;

            string classes = Attributes.GetValueOrDefault("class", "");
            bool isTreeNode = classes.Contains("node");

            // === ONLY nested child <li class="node"> blocks parent hover ===
            bool anyNestedNodeHovered = false;
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                var child = Children[i];
                if (child.UpdateHover(mousePos, viewportWidth, viewportHeight))
                {
                    if (child is LiElement childLi && childLi.Attributes.GetValueOrDefault("class", "").Contains("node"))
                    {
                        anyNestedNodeHovered = true;
                    }
                }
            }

            if (anyNestedNodeHovered)
            {
                if (IsHover) IsHover = false;
                return true;
            }

            if (ComputedWidth <= 0 || ComputedHeight <= 0) return false;

            float testHeight = isTreeNode ? GetTreeNodeHeaderHeight() : ComputedHeight;

            float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, testHeight, ComputedFullTransform, viewportWidth, viewportHeight);
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

            if (over && !IsHover)
            {
                IsHover = true;
            }
            else if (!over && IsHover)
            {
                IsHover = false;
            }

            return over;
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            string classes = Attributes.GetValueOrDefault("class", "");
            bool isTreeNode = classes.Contains("node");

            bool rowHit = false;
            if (ComputedWidth > 0 && ComputedHeight > 0)
            {
                float testHeight = isTreeNode ? GetTreeNodeHeaderHeight() : ComputedHeight;

                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, testHeight, ComputedFullTransform, viewportWidth, viewportHeight);
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
                rowHit = !(mx < minX || mx > maxX || my < minY || my > maxY);
            }

            if (rowHit)
            {
                // Give children first chance — only nested tree node children can steal the click
                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    var child = Children[i];
                    if (child.HandleClick(mousePos, viewportWidth, viewportHeight))
                    {
                        if (child is LiElement childLi && childLi.Attributes.GetValueOrDefault("class", "").Contains("node"))
                        {
                            return true;
                        }
                    }
                }
            }

            // No nested node stole it → this row claims the click (text, toggle, padding all work)
            return rowHit;
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }
    }
}