// Folder: SiegeEngine.Core.UI
// File: LiElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public class LiElement : HtmlElement
    {
        public LiElement()
        {
            Tag = "li";
            Style.Display = "block";
            Style.AlignItems = "center";
            Style.ListStyleType = "none";
            Style.PaddingStr = "4px 8px";
            Style.MarginStr = "2px 0";
        }

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            float maxWidth = 0f;
            float totalHeight = 0f;
            string foundText = "";

            bool isTopLevelNavItem = IsTopLevelNavItem();

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

            // ONLY for normal (non-nav) nested lists include sub-ul size in height
            // This fixes "the second item of the first level of the nesting not taking the first parent's children into account"
            if (!isTopLevelNavItem)
            {
                foreach (var child in Children.Where(c => c.GetEffectiveDisplay() != "none"))
                {
                    if (child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol")
                    {
                        Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                        maxWidth = Math.Max(maxWidth, childSize.X);
                        totalHeight += childSize.Y;
                    }
                }
            }

            Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float finalWidth = maxWidth + pad.W + pad.Y + borderW.W + borderW.Y;
            float finalHeight = totalHeight + pad.X + pad.Z + borderW.X + borderW.Z;

            if (float.IsNaN(finalWidth) || finalWidth < 30f) finalWidth = 120f;
            if (float.IsNaN(finalHeight) || finalHeight < 20f) finalHeight = 28f;

            return new Vector2(finalWidth, finalHeight);
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            // ONLY force intrinsic width for TOP-LEVEL nav items (nav > ul > li)
            // This fixes hover box width and text alignment in the nav bar WITHOUT breaking normal nested lists or submenu items
            if (IsTopLevelNavItem())
            {
                Vector2 intrinsic = ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, parentFs);
                forcedWidth = intrinsic.X;
            }
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }

        private bool IsTopLevelNavItem()
        {
            if (Parent == null || Parent.Tag.ToLower() != "ul") return false;
            HtmlElement grandParent = Parent.Parent;
            return grandParent != null && grandParent.Tag.ToLower() == "nav";
        }
    }
}