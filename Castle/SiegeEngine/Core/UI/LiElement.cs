// Folder: SiegeEngine.Core.UI
// File: LiElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Numerics;
using System.Linq;

namespace SiegeEngine.Core.UI
{
    public class LiElement : HtmlElement
    {
        public LiElement()
        {
            Tag = "li";
            Style.Display = "flex";
            Style.AlignItems = "center";
            Style.ListStyleType = "none";
            Style.PaddingStr = "0 22px";
            Style.HeightStr = "28px";
        }

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            var textChild = Children.OfType<TextElement>().FirstOrDefault();
            if (textChild != null && !string.IsNullOrWhiteSpace(textChild.Content))
            {
                Vector2 textSize = textRenderer.GetTextSize(textChild.Content.Trim(), fs, Style.FontFamily ?? "Arial");
                return new Vector2(textSize.X + 44f, 28f);
            }
            return new Vector2(90f, 28f);
        }
    }
}