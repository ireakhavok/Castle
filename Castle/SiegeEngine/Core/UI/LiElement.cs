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
            Style.Display = "block";
            Style.AlignItems = "center";
            Style.ListStyleType = "none";
            Style.PaddingStr = "4px 8px";
            Style.MarginStr = "2px 0";
        }

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            float iw = 0f;
            float ih = 0f;

            // Text content (for both horizontal nav and vertical lists)
            var textChild = Children.OfType<TextElement>().FirstOrDefault();
            if (textChild != null && !string.IsNullOrWhiteSpace(textChild.Content))
            {
                Vector2 textSize = textRenderer.GetTextSize(textChild.Content.Trim(), fs, Style.FontFamily ?? "Arial");
                iw = Math.Max(iw, textSize.X);
                ih += textSize.Y;
            }

            // Recursively add height from nested lists (ul/ol inside this li) - this fixes sibling spacing
            foreach (var child in Children.Where(c => c.GetEffectiveDisplay() != "none"))
            {
                if (child.Tag.ToLower() == "ul" || child.Tag.ToLower() == "ol")
                {
                    Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                    iw = Math.Max(iw, childSize.X);
                    ih += childSize.Y;
                }
            }

            // Add padding (standard for lists)
            Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            iw += pad.W + pad.Y;
            ih += pad.X + pad.Z;

            if (float.IsNaN(iw)) iw = 90f;
            if (float.IsNaN(ih)) ih = 28f;

            return new Vector2(iw, ih);
        }
    }
}