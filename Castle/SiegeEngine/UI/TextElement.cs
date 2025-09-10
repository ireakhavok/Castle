using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class TextElement : HtmlElement
    {
        public string Content { get; set; }
        private List<string> _lines;
        private float _lineHeight;

        public TextElement()
        {
            Tag = "text";
            Style.Display = "inline";
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer);
            float fs = Style.FontSize;
            _lineHeight = fs * 1.2f;
            if (Style.WhiteSpace == "normal" && !float.IsNaN(ComputedWidth) && ComputedWidth > 0)
            {
                _lines = GetWrappedLines(ComputedWidth, fs, textRenderer);
                ComputedHeight = _lines.Count * _lineHeight;
            }
            else
            {
                _lines = new List<string> { Content };
                ComputedHeight = _lineHeight;
            }
        }

        private List<string> GetWrappedLines(float maxWidth, float fs, TextRenderer textRenderer)
        {
            List<string> lines = new List<string>();
            string[] words = Content.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string line = "";
            foreach (string word in words)
            {
                string test = line + (string.IsNullOrEmpty(line) ? "" : " ") + word;
                float testWidth = textRenderer.GetTextSize(test, fs).X;
                if (testWidth > maxWidth)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        lines.Add(line);
                    }
                    line = word;
                }
                else
                {
                    line = test;
                }
            }
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
            return lines;
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            float fs = Style.FontSize;
            float y = ComputedPosition.Y;
            Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : Vector4.One;
            foreach (string line in _lines)
            {
                float lineWidth = textRenderer.GetTextSize(line, fs).X;
                float x = ComputedPosition.X;
                if (Style.TextAlign == "center")
                {
                    x += (ComputedWidth - lineWidth) / 2;
                }
                else if (Style.TextAlign == "right")
                {
                    x += ComputedWidth - lineWidth;
                }
                textRenderer.RenderText(line, x, y, (int)viewportWidth, (int)viewportHeight, fs, color);
                y += _lineHeight;
            }
        }
    }
}