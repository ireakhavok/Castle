// Folder: SiegeEngine.Core.UI
// File: NavElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Numerics;
using System.Linq;

namespace SiegeEngine.Core.UI
{
    public class NavElement : HtmlElement
    {
        public NavElement()
        {
            Tag = "nav";
            Style.Display = "flex";
            Style.BackgroundColor = new Vector4(0.121f, 0.121f, 0.121f, 1f);
            Style.HeightStr = "28px";
            Style.AlignItems = "center";
            Style.JustifyContent = "flex-start";
            Style.PaddingStr = "0 8px";
            Style.FontSizeStr = "14px";
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            forcedHeight = 28f;
            if (float.IsNaN(forcedWidth)) forcedWidth = parentWidth;

            var mainUl = Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
            if (mainUl != null)
            {
                mainUl.Style.Display = "flex";
                mainUl.Style.FlexDirection = "row";
                mainUl.Style.AlignItems = "center";
                mainUl.Style.JustifyContent = "flex-start";
                mainUl.Style.HeightStr = "28px";
                mainUl.Style.ListStyleType = "none";
                mainUl.Style.PaddingStr = "0";
                mainUl.Style.MarginStr = "0";

                foreach (var li in mainUl.Children.Where(c => c.Tag.ToLower() == "li"))
                {
                    li.Style.Display = "flex";
                    li.Style.AlignItems = "center";
                    li.Style.ListStyleType = "none";
                    li.Style.PaddingStr = "0 22px";
                    li.Style.HeightStr = "28px";
                    li.Style.WhiteSpace = "nowrap";
                    li.Style.BoxSizing = "border-box";
                }

                foreach (var li in mainUl.Children.Where(c => c.Tag.ToLower() == "li"))
                {
                    var nestedUl = li.Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
                    if (nestedUl != null)
                    {
                        nestedUl.Style.Display = "none";
                        nestedUl.Style.Position = "absolute";
                        nestedUl.Style.TopStr = "28px";
                        nestedUl.Style.LeftStr = "0";
                    }
                }
            }

            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);

            if (mainUl == null) return;

            float currentX = mainUl.ComputedContentX;
            foreach (var li in mainUl.Children.Where(c => c.Tag.ToLower() == "li"))
            {
                var textElem = li.Children.OfType<TextElement>().FirstOrDefault();
                string text = textElem != null ? textElem.Content.Trim() : "no-text";
                float measuredTextWidth = textElem != null ? textRenderer.GetTextSize(text, Style.FontSize, Style.FontFamily ?? "Arial").X : 0f;

                float itemWidth = measuredTextWidth + 44f;

                li.ComputeLayout(currentX, mainUl.ComputedContentY, itemWidth, 28f, viewportWidth, viewportHeight, textRenderer, Style.FontSize);

                if (textElem != null)
                {
                    float textY = li.ComputedContentY + (28f - 20.86f) / 2f;
                    textElem.ComputeLayout(li.ComputedContentX + 22f, textY, measuredTextWidth, 20.86f, viewportWidth, viewportHeight, textRenderer, Style.FontSize);
                }

                // FINAL BOX MODEL SYNC (this makes .nav-item:hover span the exact text + 44px padding field)
                li.ComputedWidth = itemWidth;
                li.ComputedContentWidth = measuredTextWidth;
                li.ComputedContentX = li.ComputedPosition.X + 22f;

                li.ComputedBackgroundX = li.ComputedPosition.X;
                li.ComputedBackgroundY = li.ComputedPosition.Y;
                li.ComputedBackgroundWidth = itemWidth;
                li.ComputedBackgroundHeight = 28f;

                currentX += itemWidth;
            }

            mainUl.ComputedHeight = 28f;
            mainUl.ComputedContentHeight = 28f;
            mainUl.ComputedWidth = currentX - mainUl.ComputedContentX;
            mainUl.ComputedContentWidth = mainUl.ComputedWidth;

            UpdateFullTransforms(Matrix4x4.Identity);
            Console.WriteLine($"[Nav Debug] END Nav ComputeLayout - hover background now exactly matches full text + 44px padding");
        }
    }
}