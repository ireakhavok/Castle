// Folder: SiegeEngine.UI
// File: TextElement.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
{
    public class TextElement : HtmlElement
    {
        private string _content = "";
        public string Content
        {
            get => _content;
            set
            {
                string next = value ?? "";
                if (_content != next)
                {
                    _content = next;
                    // Paint on the next frame without waiting for a full relayout.
                    // PostProcess slider readouts call this every Update().
                    _lines = new List<string> { _content };
                    if (_lineHeight <= 0f)
                        _lineHeight = (Style.FontSize > 0f ? Style.FontSize : 13f) * 1.2f;
                    MarkTextDirty();
                }
            }
        }

        private List<string> _lines = new List<string>();
        private float _lineHeight;

        public TextElement()
        {
            Tag = "text";
            Style.Display = "inline";
            // White-space inherits from the parent (nowrap on a menu item / option
            // must reach this text node or glyphs wrap and overlap the next row).
        }

        public void MarkTextDirty()
        {
            MarkIntrinsicDirty();
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);

            float fs = Style.FontSize;
            _lineHeight = fs * 1.2f;

            string whiteSpace = ResolveWhiteSpace();
            bool nowrap = whiteSpace == "nowrap" || whiteSpace == "pre" || whiteSpace == "pre-line";
            if (!nowrap && (string.IsNullOrEmpty(whiteSpace) || whiteSpace == "normal")
                && !float.IsNaN(ComputedContentWidth) && ComputedContentWidth > 0)
            {
                _lines = GetWrappedLines(ComputedContentWidth, fs, textRenderer, Style.FontFamily ?? "Arial");
            }
            else
            {
                _lines = new List<string> { Content ?? "" };
            }

            ComputedContentHeight = Math.Max(_lineHeight, _lines.Count * _lineHeight);
            if (_lines.Count > 1)
            {
                ComputedHeight = Math.Max(ComputedHeight, ComputedContentHeight);
            }
        }

        private string ResolveWhiteSpace()
        {
            if (!string.IsNullOrEmpty(Style.WhiteSpace))
                return Style.WhiteSpace;
            HtmlElement walk = Parent;
            while (walk != null)
            {
                if (!string.IsNullOrEmpty(walk.Style.WhiteSpace))
                    return walk.Style.WhiteSpace;
                walk = walk.Parent;
            }
            return "normal";
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

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);

            float fs = Style.FontSize;
            if (_lineHeight <= 0f) _lineHeight = fs * 1.2f;
            if (_lines == null || _lines.Count == 0)
                _lines = new List<string> { Content ?? "" };
            float y = ComputedContentY;
            Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : new Vector4(0.8f, 0.8f, 0.8f, 1f);
            string textAlign = string.IsNullOrEmpty(Style.TextAlign) ? "left" : Style.TextAlign;

            foreach (string line in _lines)
            {
                string renderLine = (Style.TextTransform == "uppercase") ? line.ToUpper() : line;
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
                textRenderer.RenderText(renderLine, x, y, viewportWidth, viewportHeight, fs, color, Style.FontFamily ?? "Arial", parentMatrix);
                y += _lineHeight;
            }
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            return base.HandleClick(mousePos, viewportWidth, viewportHeight);
        }
    }
}
