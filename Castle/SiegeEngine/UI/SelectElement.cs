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

            HtmlElement selectedOpt = null;

            // First, compute intrinsic as closed
            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "option")
                {
                    if (child.Attributes.ContainsKey("selected") || selectedOpt == null)
                    {
                        selectedOpt = child;
                        child.Attributes["selected"] = "";
                    }
                    else
                    {
                        child.Attributes.Remove("selected");
                    }
                    child.Style.Display = "none";
                    if (child == selectedOpt)
                    {
                        child.Style.Display = "block";
                    }
                }
            }

            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);

            // Now, if open, layout options absolutely
            if (IsOpen)
            {
                float optTop = lineH;
                foreach (var child in Children)
                {
                    if (child.Tag.ToLower() == "option")
                    {
                        child.Style.Display = "block";
                        child.Style.Position = "absolute";
                        child.Style.LeftStr = "0px";
                        child.Style.TopStr = optTop + "px";
                        child.Style.WidthStr = ComputedContentWidth + "px";
                        child.ComputeLayout(ComputedContentX, ComputedContentY, ComputedContentWidth, float.NaN, viewportWidth, viewportHeight, textRenderer, fs);
                        optTop += child.ComputedHeight;
                    }
                }
            }

            Style.Overflow = IsOpen ? "visible" : "hidden";

            float singleContentH = lineH;
            Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float singleBoxH = singleContentH + pad.X + pad.Z + borderW.X + borderW.Z;

            ComputedContentHeight = singleContentH;
            ComputedHeight = singleBoxH;
            ComputedBackgroundHeight = singleBoxH - borderW.X - borderW.Z;
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
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
            return base.HandleClick(mousePos, viewportWidth, viewportHeight);
        }
    }
}