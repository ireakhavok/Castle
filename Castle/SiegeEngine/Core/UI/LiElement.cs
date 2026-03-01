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

            bool isNavDropdownParent = IsNavDropdownParent();

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

            if (!isNavDropdownParent)
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
            else
            {
                foreach (var child in Children.Where(c => c.GetEffectiveDisplay() != "none"))
                {
                    if (child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol")
                    {
                        Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                        maxWidth = Math.Max(maxWidth, childSize.X);
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
            if (IsNavDropdownParent() || IsTopLevelNavItem())
            {
                Vector2 intrinsic = ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, parentFs);
                forcedWidth = intrinsic.X;
            }
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            bool hit = base.HandleClick(mousePos, viewportWidth, viewportHeight);

            if (IsNavDropdownParent())
            {
                IsHover = hit;
            }

            return hit;
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
            bool isdropparent = IsNavDropdownParent();
            if (isdropparent && IsHover)
            {
                var dropdownUl = Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
                if (dropdownUl != null)
                {
                    float dropdownY = ComputedPosition.Y + ComputedHeight;
                    float dropdownX = ComputedPosition.X;

                    dropdownUl.Style.Display = "block";
                    dropdownUl.ComputeLayout(dropdownX, dropdownY, dropdownUl.ComputedWidth, dropdownUl.ComputedHeight, viewportWidth, viewportHeight, textRenderer, Style.FontSize);

                    CssStyle ulStyle = dropdownUl.Style;
                    if (ulStyle.BackgroundColor != Vector4.Zero)
                    {
                        float[] dropdownNdc = GetNdcQuad(dropdownX, dropdownY, dropdownUl.ComputedWidth, dropdownUl.ComputedHeight, ComputedFullTransform, viewportWidth, viewportHeight);
                        quadRenderer.DrawNdcQuad(dropdownNdc, ulStyle.BackgroundColor);
                    }

                    dropdownUl.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, ComputedFullTransform);
                }
            }
        }

        private bool IsTopLevelNavItem()
        {
            if (Parent == null || Parent.Tag.ToLower() != "ul") return false;
            HtmlElement grandParent = Parent.Parent;
            return grandParent != null && grandParent.Tag.ToLower() == "nav";
        }

        private bool IsNavDropdownParent()
        {
            if (Tag.ToLower() != "li") return false;

            string classes = Attributes.GetValueOrDefault("class", "");
            if (classes.Contains("nav-dropdown")) return true;

            HtmlElement current = Parent;
            while (current != null)
            {
                if (current.Tag.ToLower() == "nav") return Children.Any(c => c.Tag.ToLower() == "ul");
                current = current.Parent;
            }
            return false;
        }
    }
}