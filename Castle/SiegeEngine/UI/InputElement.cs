// Folder: SiegeEngine.UI
// File: InputElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class InputElement : HtmlElement
    {
        public string Type { get; set; }

        public InputElement()
        {
            Tag = "input";
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs)
        {
            Type = Attributes.GetValueOrDefault("type", "text");

            Checked = Attributes.ContainsKey("checked");

            if (Type == "radio")
            {
                Style.Display = "none";
            }

            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs);

            if (Type == "checkbox")
            {
                float fs = Style.FontSize;

                if (float.IsNaN(ComputedWidth)) ComputedWidth = fs * 1.5f;

                if (float.IsNaN(ComputedHeight)) ComputedHeight = fs;
            }
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight);

            if (Type == "checkbox")
            {
                string symbol = Checked ? "x" : "";

                float fs = Style.FontSize;

                float textX = ComputedContentX;

                float textY = ComputedContentY + (ComputedContentHeight - fs) / 2;

                Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : Vector4.One;

                textRenderer.RenderText(symbol, textX, textY, (int)viewportWidth, (int)viewportHeight, fs, color);
            }
        }
    }
}