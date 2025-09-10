using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
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

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs);
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight);
            if (!string.IsNullOrEmpty(Selected))
            {
                float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
                float lineWidth = textRenderer.GetTextSize(Selected, fs).X;
                float textX = ComputedPosition.X;
                if (Style.TextAlign == "center")
                {
                    textX += (ComputedWidth - lineWidth) / 2;
                }
                else if (Style.TextAlign == "right")
                {
                    textX += ComputedWidth - lineWidth;
                }
                float textY = ComputedPosition.Y + (ComputedHeight - fs) / 2;
                Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : Vector4.One;
                textRenderer.RenderText(Selected, textX, textY, (int)viewportWidth, (int)viewportHeight, fs, color);
            }
        }
    }
}