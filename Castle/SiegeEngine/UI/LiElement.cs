// Folder: SiegeEngine.UI
// File: LiElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class LiElement : HtmlElement
    {
        public LiElement()
        {
            Tag = "li";
            Style.Display = "list-item";
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            // Render marker
            string marker = "";
            if (Parent != null)
            {
                string listType = Parent.Style.ListStyleType ?? "disc";
                if (Parent.Tag.ToLower() == "ul")
                {
                    if (listType == "disc") marker = "• ";
                    else if (listType == "circle") marker = "○ ";
                    else if (listType == "square") marker = "■ ";
                }
                else if (Parent.Tag.ToLower() == "ol")
                {
                    int index = Parent.Children.IndexOf(this) + 1;
                    if (listType == "decimal") marker = index + ". ";
                    else if (listType == "lower-alpha") marker = ((char)('a' + index - 1)) + ". ";
                    else if (listType == "upper-alpha") marker = ((char)('A' + index - 1)) + ". ";
                    else if (listType == "lower-roman") marker = ToRoman(index, true) + ". ";
                    else if (listType == "upper-roman") marker = ToRoman(index, false) + ". ";
                }
            }
            if (!string.IsNullOrEmpty(marker))
            {
                float fs = Style.FontSize;
                float markerX = ComputedContentX - textRenderer.GetTextSize(marker, fs, Style.FontFamily ?? "Arial").X;
                float markerY = ComputedContentY;
                Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : new Vector4(0f, 0f, 0f, 1f);
                textRenderer.RenderText(marker, markerX, markerY, viewportWidth, viewportHeight, fs, color, Style.FontFamily ?? "Arial", parentMatrix);
            }

            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }

        private string ToRoman(int number, bool lower)
        {
            if (number < 1 || number > 3999) return number.ToString();
            string[] thousands = { "", "M", "MM", "MMM" };
            string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
            string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            string roman = thousands[number / 1000] + hundreds[(number % 1000) / 100] + tens[(number % 100) / 10] + ones[number % 10];
            return lower ? roman.ToLower() : roman;
        }
    }
}