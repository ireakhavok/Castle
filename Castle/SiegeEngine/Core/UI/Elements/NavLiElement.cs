// Folder: SiegeEngine.Core.UI.Elements
// File: NavLiElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
{
    public class NavLiElement : HtmlElement
    {
        private bool _isPinnedOpen = false;

        public NavLiElement()
        {
            Tag = "li";
        }

        public bool IsDropdownOpen => IsNavDropdownParent() && (_isPinnedOpen || IsHover);

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            // ... (unchanged from provided full code)
            bool isSubmenuItem = false;
            if (Parent != null)
            {
                string parentClass = Parent.Attributes.GetValueOrDefault("class", "");
                if (parentClass.Contains("nav-dropdown-content"))
                {
                    isSubmenuItem = true;
                }
            }
            if (isSubmenuItem && Parent != null && Parent.ComputedContentWidth > 0)
            {
                Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
                Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
                float fullWidth = Parent.ComputedContentWidth;
                float submenuHeight = 0f;
                string foundText = "";
                Queue<HtmlElement> queue = new Queue<HtmlElement>(Children);
                while (queue.Count > 0)
                {
                    var elem = queue.Dequeue();
                    if (elem is TextElement textElem && !string.IsNullOrWhiteSpace(textElem.Content))
                    {
                        foundText = textElem.Content.Trim();
                        Vector2 textSize = textRenderer.GetTextSize(foundText, fs, elem.Style.FontFamily ?? Style.FontFamily ?? "Arial");
                        submenuHeight = Math.Max(submenuHeight, textSize.Y);
                    }
                    foreach (var c in elem.Children)
                        queue.Enqueue(c);
                }
                float finalHeight = submenuHeight + pad.X + pad.Z + borderW.X + borderW.Z;
                if (float.IsNaN(finalHeight) || finalHeight < 28f) finalHeight = 28f;
                return new Vector2(fullWidth, finalHeight);
            }
            string foundText2 = "";
            float maxWidth = 0f;
            float totalHeight = 0f;
            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "ul") continue;
                if (child is TextElement textElem && !string.IsNullOrWhiteSpace(textElem.Content))
                {
                    foundText2 = textElem.Content.Trim();
                    Vector2 textSize = textRenderer.GetTextSize(foundText2, fs, child.Style.FontFamily ?? Style.FontFamily ?? "Arial");
                    maxWidth = Math.Max(maxWidth, textSize.X);
                    totalHeight = Math.Max(totalHeight, textSize.Y);
                    break;
                }
            }
            Vector4 pad2 = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW2 = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float finalWidth = maxWidth + pad2.W + pad2.Y + borderW2.W + borderW2.Y;
            float finalHeight2 = totalHeight + pad2.X + pad2.Z + borderW2.X + borderW2.Z;
            if (float.IsNaN(finalWidth) || finalWidth < 40f) finalWidth = 72f;
            if (float.IsNaN(finalHeight2) || finalHeight2 < 20f) finalHeight2 = 28f;
            return new Vector2(finalWidth, finalHeight2);
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            Vector2 intrinsic = ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, parentFs);
            forcedWidth = intrinsic.X;
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }

        public override bool UpdateHover(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            bool hitOnLi = false;
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
                hitOnLi = !(mx < minX || mx > maxX || my < minY || my > maxY);
            }

            bool dropdownHit = false;
            var dropdownUl = Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
            if (IsNavDropdownParent() && dropdownUl != null && dropdownUl.GetEffectiveDisplay() != "none")
            {
                dropdownHit = dropdownUl.UpdateHover(mousePos, viewportWidth, viewportHeight);
            }

            bool isSubmenuItem = Parent != null && Parent.Attributes.GetValueOrDefault("class", "").Contains("nav-dropdown-content");

            if (IsNavDropdownParent() || IsTopLevelNavItem())
            {
                IsHover = hitOnLi || dropdownHit;
            }
            else if (isSubmenuItem)
            {
                IsHover = hitOnLi;
            }

            if (IsNavDropdownParent() && _isPinnedOpen && !hitOnLi && !dropdownHit)
            {
                _isPinnedOpen = false;
            }

            return hitOnLi || dropdownHit;
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (UpdateHover(mousePos, viewportWidth, viewportHeight))
            {
                if (IsNavDropdownParent() || IsTopLevelNavItem())
                {
                    _isPinnedOpen = !_isPinnedOpen;
                }

                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    if (Children[i].HandleClick(mousePos, viewportWidth, viewportHeight)) return true;
                }
                return true;
            }
            return false;
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);

            var dropdownUl = Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
            if (IsNavDropdownParent() && (IsHover || _isPinnedOpen) && dropdownUl != null)
            {
                float dropdownX = ComputedContentX;
                float dropdownY = ComputedPosition.Y + ComputedHeight;
                dropdownUl.Style.Display = "block";
                dropdownUl.ComputeLayout(dropdownX, dropdownY, dropdownUl.ComputedWidth, dropdownUl.ComputedHeight, viewportWidth, viewportHeight, textRenderer, Style.FontSize);

                CssStyle ulStyle = dropdownUl.Style;
                if (ulStyle.BackgroundColor != Vector4.Zero)
                {
                    float[] dropdownNdc = HtmlLayoutUtils.GetNdcQuad(dropdownX, dropdownY, dropdownUl.ComputedWidth, dropdownUl.ComputedHeight, ComputedFullTransform, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(dropdownNdc, ulStyle.BackgroundColor);
                }
                renderContext.Disable(renderContext.Enums.ScissorTest);
                dropdownUl.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, ComputedFullTransform);
                renderContext.Enable(renderContext.Enums.ScissorTest);
            }
            else if (dropdownUl != null)
            {
                dropdownUl.Style.Display = "none";
            }
        }

        private bool IsTopLevelNavItem()
        {
            if (Parent == null || Parent.Tag.ToLower() != "ul") return false;
            HtmlElement grandParent = Parent.Parent;
            return grandParent != null && grandParent.Tag.ToLower() == "nav";
        }

        public bool IsNavDropdownParent()
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

        public void CloseDropdown()
        {
            _isPinnedOpen = false;
        }
    }
}