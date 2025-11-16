// Folder: SiegeEngine.UI
// File: SelectElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class SelectElement : HtmlElement
    {
        public List<string> Options { get; set; } = new List<string>();
        public string Selected { get; set; } = "";
        public bool IsOpen { get; set; } = false;
        public HtmlElement Dropdown { get; set; }

        public SelectElement()
        {
            Tag = "select";
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
            if (!string.IsNullOrEmpty(Selected))
            {
                float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
                float lineWidth = textRenderer.GetTextSize(Selected, fs, Style.FontFamily ?? "Arial").X;
                float textX = ComputedContentX;
                string textAlign = string.IsNullOrEmpty(Style.TextAlign) ? "left" : Style.TextAlign;
                if (textAlign == "center")
                {
                    textX += (ComputedContentWidth - lineWidth) / 2;
                }
                else if (textAlign == "right")
                {
                    textX += ComputedContentWidth - lineWidth;
                }
                float textY = ComputedContentY + (ComputedContentHeight - fs) / 2;
                Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : new Vector4(0f, 0f, 0f, 1f);
                textRenderer.RenderText(Selected, textX, textY, viewportWidth, viewportHeight, fs, color, Style.FontFamily ?? "Arial", parentMatrix);
            }
        }

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            string fontFamily = Style.FontFamily ?? "Arial";
            float maxW = 0;
            float textH = textRenderer.GetTextSize("A", fs, fontFamily).Y;
            foreach (string opt in Options)
            {
                Vector2 size = textRenderer.GetTextSize(opt, fs, fontFamily);
                maxW = Math.Max(maxW, size.X);
                textH = Math.Max(textH, size.Y);
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