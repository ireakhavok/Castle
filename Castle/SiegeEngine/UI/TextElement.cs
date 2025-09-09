// SiegeEngine.UI/TextElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class TextElement : HtmlElement
    {
        public string Content { get; set; }
        public TextElement()
        {
            Tag = "text";
        }
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
            float approxWidth = Content.Length * fs * 0.6f;
            float textX = ComputedPosition.X;
            if (Style.TextAlign == "center")
            {
                textX += (ComputedWidth - approxWidth) / 2;
            }
            else if (Style.TextAlign == "right")
            {
                textX += ComputedWidth - approxWidth;
            }
            float textY = ComputedPosition.Y + (ComputedHeight - fs) / 2;
            Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : Vector4.One;
            textRenderer.RenderText(Content, textX, textY, (int)viewportWidth, (int)viewportHeight, fs, color);
        }
    }
}