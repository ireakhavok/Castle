// Folder: SiegeEngine.UI
// File: SelectElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class SelectElement : HtmlElement
    {
        public bool IsOpen { get; set; } = false;

        public SelectElement()
        {
            Tag = "select";
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            float fs = ParseSize(Style.FontSizeStr, parentFs, viewportWidth, viewportHeight);
            if (float.IsNaN(fs)) fs = parentFs;
            Style.FontSize = fs;
            float lineH = textRenderer.GetLineHeight(fs, Style.FontFamily ?? "Arial");
            List<HtmlElement> options = Children.Where(c => c.Tag.ToLower() == "option").ToList();
            HtmlElement selectedOpt = null;
            foreach (var opt in options)
            {
                if (opt.Attributes.ContainsKey("selected") || selectedOpt == null)
                {
                    selectedOpt = opt;
                    opt.Attributes["selected"] = "";
                }
                else
                {
                    opt.Attributes.Remove("selected");
                }
                opt.Style.Display = "none";
            }
            if (!IsOpen && selectedOpt != null)
            {
                selectedOpt.Style.Display = "block";
            }
            Style.Overflow = IsOpen ? "visible" : "hidden";
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
            float singleContentH = lineH;
            Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float singleBoxH = singleContentH + pad.X + pad.Z + borderW.X + borderW.Z;
            ComputedContentHeight = singleContentH;
            ComputedHeight = singleBoxH;
            ComputedBackgroundHeight = singleBoxH - borderW.X - borderW.Z;
            if (IsOpen)
            {
                float currentY = ComputedPosition.Y + ComputedHeight;
                foreach (var opt in options)
                {
                    opt.Style.Display = "block";
                    opt.IsTarget = (opt == selectedOpt);
                    opt.ComputeLayout(ComputedPosition.X, currentY, ComputedWidth, lineH, viewportWidth, viewportHeight, textRenderer, fs);
                    currentY += lineH;
                }
            }
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            CssStyle effectiveStyle = Style;
            if (Checked && PseudoStyles.TryGetValue("checked", out CssStyle checkedStyle))
            {
                effectiveStyle = checkedStyle;
            }
            if (IsHover && PseudoStyles.TryGetValue("hover", out CssStyle hover))
            {
                effectiveStyle = hover;
            }
            if (IsActive && PseudoStyles.TryGetValue("active", out CssStyle active))
            {
                effectiveStyle = active;
            }
            if (IsTarget && PseudoStyles.TryGetValue("target", out CssStyle targetStyle))
            {
                effectiveStyle = targetStyle;
            }
            if (effectiveStyle.Display == "none") return;
            Matrix4x4 local = parentMatrix * ComputedTransform;
            // Draw select background
            if (effectiveStyle.BackgroundColor != Vector4.Zero)
            {
                float[] selectNdc = GetNdcQuad(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, ComputedBackgroundHeight, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(selectNdc, effectiveStyle.BackgroundColor);
            }
            // If open, draw dropdown background
            float dropdownH = 0;
            float dropdownY = 0;
            var options = Children.Where(c => c.Tag.ToLower() == "option").ToList();
            if (IsOpen && options.Count > 0)
            {
                var firstDropdown = options[0];
                dropdownY = firstDropdown.ComputedPosition.Y;
                var last = options.Last();
                float dropdownBottom = last.ComputedPosition.Y + last.ComputedHeight;
                dropdownH = dropdownBottom - dropdownY;
                float[] dropdownNdc = GetNdcQuad(ComputedPosition.X, dropdownY, ComputedWidth, dropdownH, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(dropdownNdc, effectiveStyle.BackgroundColor);
            }
            // Borders for select
            Vector4 borderTopC = effectiveStyle.BorderTopColor != Vector4.Zero ? effectiveStyle.BorderTopColor : effectiveStyle.BorderColor;
            Vector4 borderRightC = effectiveStyle.BorderRightColor != Vector4.Zero ? effectiveStyle.BorderRightColor : effectiveStyle.BorderColor;
            Vector4 borderBottomC = effectiveStyle.BorderBottomColor != Vector4.Zero ? effectiveStyle.BorderBottomColor : effectiveStyle.BorderColor;
            Vector4 borderLeftC = effectiveStyle.BorderLeftColor != Vector4.Zero ? effectiveStyle.BorderLeftColor : effectiveStyle.BorderColor;
            string borderTopS = string.IsNullOrEmpty(effectiveStyle.BorderTopStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderTopStyle;
            string borderRightS = string.IsNullOrEmpty(effectiveStyle.BorderRightStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderRightStyle;
            string borderBottomS = string.IsNullOrEmpty(effectiveStyle.BorderBottomStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderBottomStyle;
            string borderLeftS = string.IsNullOrEmpty(effectiveStyle.BorderLeftStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderLeftStyle;
            Vector4 borderW = this.BorderWidth;
            if (borderTopS != "none" && borderTopC != Vector4.Zero && borderW.X > 0)
            {
                float[] ndc = GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, borderW.X, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderTopC);
            }
            if (borderBottomS != "none" && borderBottomC != Vector4.Zero && borderW.Z > 0)
            {
                float[] ndc = GetNdcQuad(ComputedPosition.X, ComputedPosition.Y + ComputedHeight - borderW.Z, ComputedWidth, borderW.Z, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderBottomC);
            }
            if (borderLeftS != "none" && borderLeftC != Vector4.Zero && borderW.W > 0)
            {
                float[] ndc = GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, borderW.W, ComputedHeight, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderLeftC);
            }
            if (borderRightS != "none" && borderRightC != Vector4.Zero && borderW.Y > 0)
            {
                float[] ndc = GetNdcQuad(ComputedPosition.X + ComputedWidth - borderW.Y, ComputedPosition.Y, borderW.Y, ComputedHeight, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderRightC);
            }
            // If open, borders for dropdown
            if (IsOpen && options.Count > 0 && borderW.X > 0)
            {
                Vector4 borderC = borderTopC; // assume uniform
                float bw = borderW.X;
                // top
                float[] topNdc = GetNdcQuad(ComputedPosition.X, dropdownY, ComputedWidth, bw, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(topNdc, borderC);
                // bottom
                float[] bottomNdc = GetNdcQuad(ComputedPosition.X, dropdownY + dropdownH - bw, ComputedWidth, bw, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(bottomNdc, borderC);
                // left
                float[] leftNdc = GetNdcQuad(ComputedPosition.X, dropdownY, bw, dropdownH, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(leftNdc, borderC);
                // right
                float[] rightNdc = GetNdcQuad(ComputedPosition.X + ComputedWidth - bw, dropdownY, bw, dropdownH, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(rightNdc, borderC);
            }
            // Render children
            foreach (var child in Children)
            {
                child.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, local);
            }
        }

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            string fontFamily = Style.FontFamily ?? "Arial";
            float maxW = 0;
            float textH = 0;
            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "option")
                {
                    string text = string.Join("", child.Children.OfType<TextElement>().Select(t => t.Content));
                    Vector2 size = textRenderer.GetTextSize(text, fs, fontFamily);
                    maxW = Math.Max(maxW, size.X);
                    textH = Math.Max(textH, size.Y);
                }
            }
            if (maxW == 0) maxW = 100; // Default width if no options
            Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float iw = maxW + pad.W + pad.Y + borderW.W + borderW.Y;
            float ih = textH + pad.X + pad.Z + borderW.X + borderW.Z;
            return new Vector2(iw, ih);
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            bool baseOver = base.HandleClick(mousePos, viewportWidth, viewportHeight);
            if (!IsOpen || baseOver) return baseOver;

            var options = Children.Where(c => c.Tag.ToLower() == "option").ToList();
            if (options.Count == 0) return false;
            var first = options[0];
            var last = options.Last();
            float dropdownY = first.ComputedPosition.Y;
            float dropdownH = last.ComputedPosition.Y + last.ComputedHeight - dropdownY;
            float[] fullNdc = GetNdcQuad(ComputedPosition.X, dropdownY, ComputedWidth, dropdownH, ComputedFullTransform, viewportWidth, viewportHeight);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                float nx = fullNdc[k * 2];
                float ny = fullNdc[k * 2 + 1];
                minX = Math.Min(minX, nx);
                maxX = Math.Max(maxX, nx);
                minY = Math.Min(minY, ny);
                maxY = Math.Max(maxY, ny);
            }
            float mx = 2 * mousePos.X / viewportWidth - 1;
            float my = 1 - 2 * mousePos.Y / viewportHeight;
            bool overFull = mx >= minX && mx <= maxX && my >= minY && my <= maxY;
            return overFull;
        }
    }
}