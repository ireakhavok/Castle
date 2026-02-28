// Folder: SiegeEngine.Core.UI
// File: NavElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public class NavElement : HtmlElement
    {
        public NavElement()
        {
            Tag = "nav";
            Style.Display = "flex";
            Style.BackgroundColor = new Vector4(0.121f, 0.121f, 0.121f, 1f);
            Style.Color = "#e0e0e0";
            Style.HeightStr = "28px";
            Style.AlignItems = "center";
            Style.JustifyContent = "flex-start";
            Style.PaddingStr = "0 6px";
            Style.FontSizeStr = "14px";
            Style.BorderBottomWidthStr = "1px";
            Style.BorderBottomStyle = "solid";
            Style.BorderBottomColor = new Vector4(0.22f, 0.22f, 0.22f, 1f);
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            forcedHeight = 28f;
            if (float.IsNaN(forcedWidth)) forcedWidth = parentWidth;

            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);

            // Force clean horizontal menu layout with proper spacing
            float x = ComputedContentX;
            foreach (var child in Children)
            {
                if (child.GetEffectiveDisplay() == "none") continue;
                Vector2 intrinsic = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, Style.FontSize);
                child.ComputeLayout(x, ComputedContentY, intrinsic.X + 12, ComputedContentHeight, viewportWidth, viewportHeight, textRenderer, Style.FontSize);
                x += child.ComputedWidth + 8f;
            }
        }
    }
}