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

        public SelectElement()
        {
            Tag = "select";
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight);
            if (!string.IsNullOrEmpty(Selected))
            {
                float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
                float lineWidth = textRenderer.GetTextSize(Selected, fs, Style.FontFamily ?? "Arial").X;
                float textX = ComputedContentX;
                if (Style.TextAlign == "center")
                {
                    textX += (ComputedContentWidth - lineWidth) / 2;
                }
                else if (Style.TextAlign == "right")
                {
                    textX += ComputedContentWidth - lineWidth;
                }
                float textY = ComputedContentY + (ComputedContentHeight - fs) / 2;
                Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : Vector4.One;
                textRenderer.RenderText(Selected, textX, textY, (int)viewportWidth, (int)viewportHeight, fs, color, Style.FontFamily ?? "Arial");
            }
        }
    }
}