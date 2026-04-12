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
            // Header row only (toggle + label) - this keeps rows tight
            float headerWidth = 0f;
            float headerHeight = 28f; // default tree row height

            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol")
                    break;

                Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                headerWidth = Math.Max(headerWidth, childSize.X);
                headerHeight = Math.Max(headerHeight, childSize.Y);
            }

            Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);

            float finalWidth = headerWidth + pad.W + pad.Y + borderW.W + borderW.Y + 30f; // space for toggle + indent
            float finalHeight = headerHeight + pad.X + pad.Z + borderW.X + borderW.Z;

            // IMPORTANT: add full expanded subtree height so parent <ul> can push siblings down correctly
            foreach (var child in Children)
            {
                if ((child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol") && child.GetEffectiveDisplay() != "none")
                {
                    Vector2 subtreeSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                    finalHeight += subtreeSize.Y + 4f; // tiny gap
                    finalWidth = Math.Max(finalWidth, subtreeSize.X + 22f); // indented width
                }
            }

            if (float.IsNaN(finalWidth) || finalWidth < 40f) finalWidth = 180f;
            if (float.IsNaN(finalHeight) || finalHeight < 28f) finalHeight = 28f;

            return new Vector2(finalWidth, finalHeight);
        }

        private float GetTreeNodeHeaderHeight()
        {
            float height = 28f;
            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol") break;
                height = Math.Max(height, child.ComputedHeight);
            }
            return height;
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            // Let base block layout position header + children normally (this fixes sibling stacking)
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);

            // After base layout runs, ensure the li's final height includes the full subtree
            // (so next sibling in parent ul is pushed down correctly)
            float fullHeight = GetTreeNodeHeaderHeight();
            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol")
                {
                    fullHeight += child.ComputedHeight + 4f;
                    break;
                }
            }
            ComputedHeight = Math.Max(ComputedHeight, fullHeight);
        }

        public override bool UpdateHover(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none") return false;

            string classes = Attributes.GetValueOrDefault("class", "");
            bool isTreeNode = classes.Contains("node");

            // Nested nodes get first chance
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i].UpdateHover(mousePos, viewportWidth, viewportHeight))
                    return true;
            }

            if (!isTreeNode) return base.UpdateHover(mousePos, viewportWidth, viewportHeight);

            float testHeight = GetTreeNodeHeaderHeight();
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

            if (over && !IsHover) IsHover = true;
            else if (!over && IsHover) IsHover = false;

            return over;
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            string classes = Attributes.GetValueOrDefault("class", "");
            bool isTreeNode = classes.Contains("node");

            // Nested nodes get first chance
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i].HandleClick(mousePos, viewportWidth, viewportHeight))
                    return true;
            }

            if (!isTreeNode) return base.HandleClick(mousePos, viewportWidth, viewportHeight);

            float testHeight = GetTreeNodeHeaderHeight();
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
            bool rowHit = !(mx < minX || mx > maxX || my < minY || my > maxY);

            return rowHit;
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }
    }
}