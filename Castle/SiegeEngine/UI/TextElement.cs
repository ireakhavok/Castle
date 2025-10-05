// Folder: SiegeEngine.UI
// File: TextElement.cs
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
        private List<string> _lines = new List<string>();
        private float _lineHeight;
        public TextElement()
        {
            Tag = "text";
            Style.Display = "inline";
        }
        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
            float fs = Style.FontSize;
            _lineHeight = fs * 1.2f;
            if (Style.WhiteSpace == "normal" && !float.IsNaN(ComputedContentWidth) && ComputedContentWidth > 0)
            {
                _lines = GetWrappedLines(ComputedContentWidth, fs, textRenderer, Style.FontFamily ?? "Arial");
                ComputedContentHeight = _lines.Count * _lineHeight;
            }
            else
            {
                _lines = new List<string> { Content };
                ComputedContentHeight = _lineHeight;
            }
        }
        private List<string> GetWrappedLines(float maxWidth, float fs, TextRenderer textRenderer, string fontFamily)
        {
            List<string> lines = new List<string>();
            string[] words = Content.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string line = "";
            foreach (string word in words)
            {
                string test = line + (string.IsNullOrEmpty(line) ? "" : " ") + word;
                float testWidth = textRenderer.GetTextSize(test, fs, fontFamily).X;
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
            float y = ComputedContentY;
            Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : Vector4.One;
            string renderContent = Content;
            if (Style.TextTransform == "uppercase") renderContent = Content.ToUpper();
            string textAlign = string.IsNullOrEmpty(Style.TextAlign) ? "left" : Style.TextAlign;
            foreach (string line in _lines)
            {
                string renderLine = line;
                if (Style.TextTransform == "uppercase") renderLine = line.ToUpper();
                float lineWidth = textRenderer.GetTextSize(renderLine, fs, Style.FontFamily ?? "Arial").X;
                float x = ComputedContentX;
                if (textAlign == "center")
                {
                    x += (ComputedContentWidth - lineWidth) / 2;
                }
                else if (textAlign == "right")
                {
                    x += ComputedContentWidth - lineWidth;
                }
                textRenderer.RenderText(renderLine, x, y, (int)viewportWidth, (int)viewportHeight, fs, color, Style.FontFamily ?? "Arial");
                y += _lineHeight;
            }
        }
    }
}