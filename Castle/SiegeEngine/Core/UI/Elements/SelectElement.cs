// Folder: SiegeEngine.Core.UI.Elements
// File: SelectElement.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
{
    public class SelectElement : HtmlElement
    {
        public bool IsOpen { get; set; } = false;
        public string Value
        {
            get
            {
                var options = Children.Where(c => c.Tag.ToLower() == "option").ToList();
                var selectedOpt = options.FirstOrDefault(o => o.Attributes.ContainsKey("selected"));
                if (selectedOpt == null && options.Count > 0)
                {
                    selectedOpt = options[0];
                }
                if (selectedOpt != null)
                {
                    if (selectedOpt.Attributes.TryGetValue("value", out string val))
                    {
                        return val;
                    }
                    return string.Join("", selectedOpt.Children.OfType<TextElement>().Select(t => t.Content));
                }
                return "";
            }
            set
            {
                var options = Children.Where(c => c.Tag.ToLower() == "option").ToList();
                foreach (var opt in options)
                {
                    opt.Attributes.Remove("selected");
                    string optVal = opt.Attributes.GetValueOrDefault("value", string.Join("", opt.Children.OfType<TextElement>().Select(t => t.Content)));
                    if (optVal == value)
                    {
                        opt.Attributes["selected"] = "";
                    }
                }
            }
        }
        public SelectElement()
        {
            Tag = "select";
        }
        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            float fs = HtmlLayoutUtils.ParseSize(Style.FontSizeStr, parentFs, viewportWidth, viewportHeight);
            if (float.IsNaN(fs)) fs = parentFs;
            Style.FontSize = fs;
            float lineH = textRenderer.GetLineHeight(fs, Style.FontFamily ?? "Arial");
            List<HtmlElement> options = Children.Where(c => c.Tag.ToLower() == "option").ToList();
            HtmlElement selectedOpt = options.FirstOrDefault(o => o.Attributes.ContainsKey("selected"));
            if (selectedOpt == null && options.Count > 0)
            {
                selectedOpt = options[0];
                selectedOpt.Attributes["selected"] = "";
            }
            foreach (var opt in options)
            {
                opt.Style.Display = "none";
            }
            if (selectedOpt != null)
            {
                selectedOpt.Style.Display = "block";
            }
            Style.Overflow = "hidden";
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
            float singleContentH = lineH;
            Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float singleBoxH = singleContentH + pad.X + pad.Z + borderW.X + borderW.Z;
            ComputedContentHeight = singleContentH;
            ComputedHeight = singleBoxH;
            ComputedBackgroundHeight = singleBoxH - borderW.X - borderW.Z;
            if (!float.IsNaN(parentWidth) && parentWidth > ComputedWidth)
            {
                ComputedWidth = parentWidth;
                ComputedBackgroundWidth = ComputedWidth - borderW.W - borderW.Y;
                if (ComputedBackgroundWidth < 0) ComputedBackgroundWidth = 0;
                ComputedContentWidth = ComputedBackgroundWidth - pad.W - pad.Y;
                if (ComputedContentWidth < 0) ComputedContentWidth = 0;
            }
            if (IsOpen)
            {
                float dropdownH = options.Count * lineH;
                float spaceBelow = viewportHeight - (ComputedPosition.Y + ComputedHeight);
                bool openUp = spaceBelow < dropdownH && ComputedPosition.Y > dropdownH;
                float currentY;
                if (openUp)
                {
                    currentY = ComputedPosition.Y - dropdownH;
                }
                else
                {
                    currentY = ComputedPosition.Y + ComputedHeight;
                }
                foreach (var opt in options)
                {
                    opt.Style.Display = "block";
                    opt.IsTarget = opt == selectedOpt;
                    opt.ComputeLayout(ComputedBackgroundX, currentY, ComputedBackgroundWidth, lineH, viewportWidth, viewportHeight, textRenderer, fs);
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
            if (effectiveStyle.BackgroundColor != Vector4.Zero)
            {
                float[] selectNdc = HtmlLayoutUtils.GetNdcQuad(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, ComputedBackgroundHeight, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(selectNdc, effectiveStyle.BackgroundColor);
            }
            Vector4 borderTopC = effectiveStyle.BorderTopColor != Vector4.Zero ? effectiveStyle.BorderTopColor : effectiveStyle.BorderColor;
            Vector4 borderRightC = effectiveStyle.BorderRightColor != Vector4.Zero ? effectiveStyle.BorderRightColor : effectiveStyle.BorderColor;
            Vector4 borderBottomC = effectiveStyle.BorderBottomColor != Vector4.Zero ? effectiveStyle.BorderBottomColor : effectiveStyle.BorderColor;
            Vector4 borderLeftC = effectiveStyle.BorderLeftColor != Vector4.Zero ? effectiveStyle.BorderLeftColor : effectiveStyle.BorderColor;
            string borderTopS = string.IsNullOrEmpty(effectiveStyle.BorderTopStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderTopStyle;
            string borderRightS = string.IsNullOrEmpty(effectiveStyle.BorderRightStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderRightStyle;
            string borderBottomS = string.IsNullOrEmpty(effectiveStyle.BorderBottomStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderBottomStyle;
            string borderLeftS = string.IsNullOrEmpty(effectiveStyle.BorderLeftStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderLeftStyle;
            Vector4 borderW = BorderWidth;
            if (borderTopS != "none" && borderTopC != Vector4.Zero && borderW.X > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, borderW.X, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderTopC);
            }
            if (borderBottomS != "none" && borderBottomC != Vector4.Zero && borderW.Z > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y + ComputedHeight - borderW.Z, ComputedWidth, borderW.Z, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderBottomC);
            }
            if (borderLeftS != "none" && borderLeftC != Vector4.Zero && borderW.W > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, borderW.W, ComputedHeight, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderLeftC);
            }
            if (borderRightS != "none" && borderRightC != Vector4.Zero && borderW.Y > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X + ComputedWidth - borderW.Y, ComputedPosition.Y, borderW.Y, ComputedHeight, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderRightC);
            }
            // === ONLY render selected option when closed (prevents double drawing when dropdown open) ===
            if (!IsOpen)
            {
                foreach (var child in Children)
                {
                    child.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, local);
                }
            }
        }
        public void RenderDropdown(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            Matrix4x4 local = parentMatrix * ComputedFullTransform;
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
            var options = Children.Where(c => c.Tag.ToLower() == "option").ToList();
            if (options.Count == 0) return;
            var first = options[0];
            float dropdownY = first.ComputedPosition.Y;
            var last = options.Last();
            float dropdownH = last.ComputedPosition.Y + last.ComputedHeight - dropdownY;
            if (effectiveStyle.BackgroundColor != Vector4.Zero)
            {
                float[] dropdownNdc = HtmlLayoutUtils.GetNdcQuad(ComputedBackgroundX, dropdownY, ComputedBackgroundWidth, dropdownH, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(dropdownNdc, effectiveStyle.BackgroundColor);
            }
            Vector4 borderTopC = effectiveStyle.BorderTopColor != Vector4.Zero ? effectiveStyle.BorderTopColor : effectiveStyle.BorderColor;
            Vector4 borderRightC = effectiveStyle.BorderRightColor != Vector4.Zero ? effectiveStyle.BorderRightColor : effectiveStyle.BorderColor;
            Vector4 borderBottomC = effectiveStyle.BorderBottomColor != Vector4.Zero ? effectiveStyle.BorderBottomColor : effectiveStyle.BorderColor;
            Vector4 borderLeftC = effectiveStyle.BorderLeftColor != Vector4.Zero ? effectiveStyle.BorderLeftColor : effectiveStyle.BorderColor;
            string borderTopS = string.IsNullOrEmpty(effectiveStyle.BorderTopStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderTopStyle;
            string borderRightS = string.IsNullOrEmpty(effectiveStyle.BorderRightStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderRightStyle;
            string borderBottomS = string.IsNullOrEmpty(effectiveStyle.BorderBottomStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderBottomStyle;
            string borderLeftS = string.IsNullOrEmpty(effectiveStyle.BorderLeftStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderLeftStyle;
            Vector4 borderW = BorderWidth;
            if (borderTopS != "none" && borderTopC != Vector4.Zero && borderW.X > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, dropdownY, ComputedWidth, borderW.X, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderTopC);
            }
            if (borderBottomS != "none" && borderBottomC != Vector4.Zero && borderW.Z > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, dropdownY + dropdownH - borderW.Z, ComputedWidth, borderW.Z, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderBottomC);
            }
            if (borderLeftS != "none" && borderLeftC != Vector4.Zero && borderW.W > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, dropdownY, borderW.W, dropdownH, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderLeftC);
            }
            if (borderRightS != "none" && borderRightC != Vector4.Zero && borderW.Y > 0)
            {
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X + ComputedWidth - borderW.Y, dropdownY, borderW.Y, dropdownH, local, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(ndc, borderRightC);
            }
            foreach (var opt in options)
            {
                opt.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, local);
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
            if (maxW == 0) maxW = 100;
            Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float iw = maxW + pad.W + pad.Y + borderW.W + borderW.Y;
            float ih = textH + pad.X + pad.Z + borderW.X + borderW.Z;
            return new Vector2(iw, ih);
        }
        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            return base.HandleClick(mousePos, viewportWidth, viewportHeight);
        }
    }
}