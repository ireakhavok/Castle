// Folder: SiegeEngine.Core.UI.Elements
// File: NavLiElement.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
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
            bool isParent = IsNavDropdownParent();
            bool isSubmenuItem = Parent != null && Parent.Attributes.GetValueOrDefault("class", "").Contains("nav-dropdown-content");

            bool hitOnLi = PixelHit(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, Math.Max(ComputedHeight, 1f), mousePos);

            if (isSubmenuItem && !isParent)
            {
                IsHover = hitOnLi;
                return hitOnLi;
            }

            bool hitPopup = false;
            var dropdownUl = Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
            if (isParent && dropdownUl != null)
            {
                bool wasOpen = IsHover || _isPinnedOpen;
                GetPopupRect(out float px, out float py, out float pw, out float ph);
                hitPopup = wasOpen || hitOnLi
                    ? PixelHit(px, py, pw, ph, mousePos)
                    : false;

                if (hitOnLi || hitPopup)
                {
                    dropdownUl.Style.Display = "block";
                    HoverPopupItems(dropdownUl, mousePos);
                }
                else
                {
                    dropdownUl.Style.Display = "none";
                    ClearHoverTree(dropdownUl);
                }
            }

            if (isParent || IsTopLevelNavItem())
                IsHover = hitOnLi || hitPopup;

            if (isParent && _isPinnedOpen && !hitOnLi && !hitPopup)
                _isPinnedOpen = false;

            return hitOnLi || hitPopup;
        }

        public bool ContainsPointer(Vector2 mousePos)
        {
            if (PixelHit(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, Math.Max(ComputedHeight, 1f), mousePos))
                return true;
            if (!IsNavDropdownParent() || !(IsHover || _isPinnedOpen))
                return false;
            GetPopupRect(out float x, out float y, out float w, out float h);
            return PixelHit(x, y, w, h, mousePos);
        }

        public void GetPopupRect(out float x, out float y, out float w, out float h)
        {
            x = ComputedContentX != 0f ? ComputedContentX : ComputedPosition.X;
            y = ComputedPosition.Y + Math.Max(ComputedHeight, 28f);
            var dropdownUl = Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
            int items = 1;
            if (dropdownUl != null)
                items = Math.Max(1, dropdownUl.Children.Count(c => c.Tag.ToLower() == "li"));
            w = Math.Max(200f, ComputedWidth);
            if (dropdownUl != null && dropdownUl.ComputedWidth > 20f)
                w = dropdownUl.ComputedWidth;
            h = items * 32f + 8f;
        }

        private static bool PixelHit(float x, float y, float w, float h, Vector2 mouse)
        {
            if (w <= 0f || h <= 0f) return false;
            return mouse.X >= x && mouse.X <= x + w && mouse.Y >= y && mouse.Y <= y + h;
        }

        private static void HoverPopupItems(HtmlElement ul, Vector2 mouse)
        {
            foreach (var child in ul.Children)
            {
                if (child.Tag.ToLower() != "li") continue;
                // Items laid out while the UL was display:none sit at y=0 and
                // would steal clicks from the 28px label. Ignore those.
                if (child.ComputedPosition.Y < 28f)
                {
                    child.IsHover = false;
                    continue;
                }
                child.IsHover = PixelHit(child.ComputedPosition.X, child.ComputedPosition.Y,
                    child.ComputedWidth > 0 ? child.ComputedWidth : ul.ComputedWidth,
                    child.ComputedHeight > 0 ? child.ComputedHeight : 32f,
                    mouse);
            }
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (UpdateHover(mousePos, viewportWidth, viewportHeight))
            {
                if (IsNavDropdownParent() || IsTopLevelNavItem())
                    _isPinnedOpen = !_isPinnedOpen;

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
            bool isSubmenuItem = Parent != null && Parent.Attributes.GetValueOrDefault("class", "").Contains("nav-dropdown-content");
            if (isSubmenuItem && ComputedWidth > 0 && ComputedHeight > 0)
            {
                Vector4 rowColor = IsHover
                    ? new Vector4(0.220f, 0.220f, 0.220f, 1f)
                    : new Vector4(0.145f, 0.145f, 0.149f, 1f);
                float[] itemNdc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, ComputedHeight, ComputedFullTransform, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(itemNdc, rowColor);
            }
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);

            var dropdownUl = Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
            if (IsNavDropdownParent() && (IsHover || _isPinnedOpen) && dropdownUl != null)
            {
                float dropdownX = ComputedContentX;
                float dropdownY = ComputedPosition.Y + ComputedHeight;
                dropdownUl.Style.Display = "block";
                dropdownUl.ComputeLayout(dropdownX, dropdownY, dropdownUl.ComputedWidth, dropdownUl.ComputedHeight, viewportWidth, viewportHeight, textRenderer, Style.FontSize);

                CssStyle ulStyle = dropdownUl.Style;
                Vector4 panelColor = ulStyle.BackgroundColor != Vector4.Zero
                    ? ulStyle.BackgroundColor
                    : new Vector4(0.145f, 0.145f, 0.149f, 1f);
                float panelW = dropdownUl.ComputedWidth > 0 ? dropdownUl.ComputedWidth : Math.Max(200f, ComputedWidth);
                float panelH = dropdownUl.ComputedHeight > 0 ? dropdownUl.ComputedHeight : 8f;
                float[] dropdownNdc = HtmlLayoutUtils.GetNdcQuad(dropdownX, dropdownY, panelW, panelH, ComputedFullTransform, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(dropdownNdc, panelColor);
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
            var tokens = classes.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Contains("nav-dropdown")) return true;
            if (tokens.Contains("nav-dropdown-item")) return false;
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
            ReleaseHover();
        }

        public void ReleaseHover()
        {
            _isPinnedOpen = false;
            IsHover = false;
            var dropdownUl = Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
            if (dropdownUl != null)
            {
                dropdownUl.Style.Display = "none";
                ClearHoverTree(dropdownUl);
            }
        }

        private static void ClearHoverTree(HtmlElement root)
        {
            if (root == null) return;
            root.IsHover = false;
            foreach (var child in root.Children)
                ClearHoverTree(child);
        }
    }
}