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

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, Vector2 parentPosition, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight)
        {
            // No background for text
            textRenderer.RenderText(Content, parentPosition.X, parentPosition.Y, (int)parentWidth, (int)parentHeight, Style.FontSize, Style.TextColor);
        }
    }
}