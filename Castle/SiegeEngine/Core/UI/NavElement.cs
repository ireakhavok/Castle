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
                // Force top-level nav bar to horizontal flex (overrides UlElement block default)
                mainUl.Style.Display = "flex";
                mainUl.Style.FlexDirection = "row";
                mainUl.Style.AlignItems = "center";
                mainUl.Style.JustifyContent = "flex-start";
                mainUl.Style.HeightStr = "28px";
                mainUl.Style.ListStyleType = "none";
                mainUl.Style.PaddingStr = "0";
                mainUl.Style.MarginStr = "0";

                // Force top-level LIs inside nav to horizontal nav-item behavior
                // (overrides LiElement block + recursive height for dropdowns)
                foreach (var li in mainUl.Children.Where(c => c.Tag.ToLower() == "li"))
                {
                    li.Style.Display = "flex";
                    li.Style.AlignItems = "center";
                    li.Style.HeightStr = "28px";
                    li.Style.PaddingStr = "0 22px";
                    li.Style.MarginStr = "0";
                    li.Style.WhiteSpace = "nowrap";
                    li.Style.BoxSizing = "border-box";
                    li.Style.Position = "relative";   // required for absolute dropdowns

                    // Nested dropdown ULs stay vertical block (absolute so they do NOT affect parent LI height)
                    var nestedUl = li.Children.FirstOrDefault(c => c.Tag.ToLower() == "ul");
                    if (nestedUl != null)
                    {
                        nestedUl.Style.Display = "none";
                        nestedUl.Style.Position = "absolute";
                        nestedUl.Style.TopStr = "100%";
                        nestedUl.Style.LeftStr = "0";
                        nestedUl.Style.BackgroundColor = new Vector4(0.18f, 0.18f, 0.18f, 1f);
                        nestedUl.Style.PaddingStr = "4px 0";
                        nestedUl.Style.MinWidthStr = "160px";
                        nestedUl.Style.BorderWidthStr = "1px";
                        nestedUl.Style.BorderColor = new Vector4(0.45f, 0.45f, 0.45f, 1f);
                        nestedUl.Style.ListStyleType = "none";
                        nestedUl.Style.MarginStr = "0";
                    }
                }
            }

            // Let base (and LiElement/UlElement recursive intrinsic) run with our forced styles
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);

            Console.WriteLine($"[Nav Debug] NavElement expanded - top UL forced horizontal flex, direct LIs forced horizontal (22px padding), nested dropdown ULs absolute + vertical block");
        }
    }
}