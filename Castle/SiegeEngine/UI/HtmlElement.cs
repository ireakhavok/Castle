using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class HtmlElement
    {
        public string Tag { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public List<HtmlElement> Children { get; set; } = new List<HtmlElement>();
        public CssStyle Style { get; set; } = new CssStyle();
        public Dictionary<string, CssStyle> PseudoStyles { get; set; } = new Dictionary<string, CssStyle>();
        public HtmlElement Parent { get; set; }
        public Vector2 ComputedPosition { get; set; }
        public float ComputedWidth { get; set; }
        public float ComputedHeight { get; set; }
        public bool IsHover { get; set; }
        public bool IsActive { get; set; }
        public bool Checked { get; set; }

        public virtual void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs)
        {
            if (Style.Display == "none")
            {
                ComputedWidth = 0;
                ComputedHeight = 0;
                return;
            }

            float fs = ParseSize(Style.FontSizeStr, parentFs, viewportWidth, viewportHeight);
            if (float.IsNaN(fs)) fs = parentFs;
            Style.FontSize = fs;

            float left = ParseSize(Style.LeftStr, parentWidth, viewportWidth, viewportHeight);
            if (float.IsNaN(left)) left = 0;
            float top = ParseSize(Style.TopStr, parentHeight, viewportWidth, viewportHeight);
            if (float.IsNaN(top)) top = 0;
            float maxW = ParseSize(Style.MaxWidthStr, parentWidth, viewportWidth, viewportHeight);
            float w = ParseSize(Style.WidthStr, parentWidth, viewportWidth, viewportHeight);
            float h = ParseSize(Style.HeightStr, parentHeight, viewportWidth, viewportHeight);
            Vector4 pad = ParsePadding(Style.PaddingStr, parentWidth, viewportWidth, viewportHeight);
            Vector4 margin = ParsePadding(Style.MarginStr, parentWidth, viewportWidth, viewportHeight);
            float borderW = Style.BorderWidth;
            float innerParentWidth = parentWidth - pad.W - pad.Y - margin.W - margin.Y - borderW * 2;
            float innerParentHeight = parentHeight - pad.X - pad.Z - margin.X - margin.Z - borderW * 2;
            float outerW = w;
            float outerH = h;
            if (Style.BoxSizing == "border-box")
            {
                if (!float.IsNaN(w)) innerParentWidth = w - pad.W - pad.Y - borderW * 2;
                if (!float.IsNaN(h)) innerParentHeight = h - pad.X - pad.Z - borderW * 2;
            }
            if (float.IsNaN(w) || w < 0)
            {
                w = GetAutoWidth(innerParentWidth, viewportWidth, viewportHeight, textRenderer);
            }
            if (float.IsNaN(h) || h < 0)
            {
                h = GetAutoHeight(innerParentHeight, viewportWidth, viewportHeight, textRenderer);
            }
            if (!float.IsNaN(maxW)) w = Math.Min(w, maxW);
            ComputedWidth = Style.BoxSizing == "border-box" ? outerW : w + pad.W + pad.Y + borderW * 2 + margin.W + margin.Y;
            ComputedHeight = Style.BoxSizing == "border-box" ? outerH : h + pad.X + pad.Z + borderW * 2 + margin.X + margin.Z;
            if (Style.Position == "absolute")
            {
                ComputedPosition = new Vector2(left + margin.W, top + margin.X);
            }
            else
            {
                ComputedPosition = new Vector2(parentPositionX + left + margin.W, parentPositionY + top + margin.X);
            }
            // Layout children
            if (Children.Count > 0)
            {
                if (Style.Display == "flex")
                {
                    LayoutFlexChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
                else // block
                {
                    LayoutBlockChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
            }
        }

        private void LayoutFlexChildren(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            bool isRow = Style.FlexDirection == "row";
            float gap = ParseSize(Style.GapStr, 0, viewportWidth, viewportHeight);
            if (float.IsNaN(gap)) gap = 0;
            float availableMain = isRow ? ComputedWidth : ComputedHeight;
            float availableCross = isRow ? ComputedHeight : ComputedWidth;
            // Calculate base sizes
            List<float> childBaseMain = new List<float>();
            float totalBaseMain = 0;
            foreach (var child in Children)
            {
                float childW = ParseSize(isRow ? child.Style.WidthStr : child.Style.HeightStr, availableMain, viewportWidth, viewportHeight);
                float baseMain = float.IsNaN(childW) ? (isRow ? child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X : child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y) : childW;
                childBaseMain.Add(baseMain);
                totalBaseMain += baseMain;
            }
            totalBaseMain += gap * (Children.Count - 1);
            // Simple distribution for now, no grow/shrink
            float scale = 1.0f;
            if (totalBaseMain > availableMain)
            {
                scale = availableMain / totalBaseMain;
            }
            float childPosMain = 0;
            float totalMain = (childBaseMain.Sum() + gap * (Children.Count - 1)) * scale;
            float spacing = gap * scale;
            if (Style.JustifyContent == "center")
            {
                childPosMain = (availableMain - totalMain) / 2;
            }
            else if (Style.JustifyContent == "space-between")
            {
                if (Children.Count > 1)
                {
                    spacing = (availableMain - childBaseMain.Sum() * scale) / (Children.Count - 1);
                }
            } // add more
            for (int j = 0; j < Children.Count; j++)
            {
                var child = Children[j];
                float childMain = childBaseMain[j] * scale;
                float childCross = ParseSize(isRow ? child.Style.HeightStr : child.Style.WidthStr, availableCross, viewportWidth, viewportHeight);
                if (float.IsNaN(childCross)) childCross = isRow ? child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y : child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X;
                float offsetCross = 0;
                if (Style.AlignItems == "center")
                {
                    offsetCross = (availableCross - childCross) / 2;
                }
                float childPosX = ComputedPosition.X + (isRow ? childPosMain : offsetCross);
                float childPosY = ComputedPosition.Y + (isRow ? offsetCross : childPosMain);
                child.ComputeLayout(childPosX, childPosY, isRow ? childMain : childCross, isRow ? childCross : childMain, viewportWidth, viewportHeight, textRenderer, fs);
                childPosMain += childMain + spacing;
            }
        }

        private void LayoutBlockChildren(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            float currentY = 0;
            foreach (var child in Children)
            {
                float childW = ParseSize(child.Style.WidthStr, ComputedWidth, viewportWidth, viewportHeight);
                if (float.IsNaN(childW)) childW = GetAutoWidth(ComputedWidth, viewportWidth, viewportHeight, textRenderer);
                float childH = ParseSize(child.Style.HeightStr, ComputedHeight - currentY, viewportWidth, viewportHeight);
                if (float.IsNaN(childH)) childH = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y;
                child.ComputeLayout(ComputedPosition.X, ComputedPosition.Y + currentY, childW, childH, viewportWidth, viewportHeight, textRenderer, fs);
                currentY += child.ComputedHeight;
            }
        }

        private float GetAutoWidth(float parentWidth, float viewportWidth, float viewportHeight, TextRenderer textRenderer)
        {
            if (Style.Display == "block" || Style.Display == "flex")
            {
                return parentWidth;
            }
            return ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, Style.FontSize).X;
        }

        private float GetAutoHeight(float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer)
        {
            return ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, Style.FontSize).Y;
        }

        protected Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            float width = 0;
            float height = 0;
            Vector4 pad = ParsePadding(Style.PaddingStr, 0, viewportWidth, viewportHeight);
            Vector4 margin = ParsePadding(Style.MarginStr, 0, viewportWidth, viewportHeight);
            float borderW = Style.BorderWidth;
            if (Children.Count == 0)
            {
                if (this is TextElement text)
                {
                    var size = textRenderer.GetTextSize(text.Content, fs);
                    width = size.X;
                    height = size.Y;
                }
            }
            else
            {
                bool isRow = Style.FlexDirection == "row";
                float gap = ParseSize(Style.GapStr, 0, viewportWidth, viewportHeight);
                if (float.IsNaN(gap)) gap = 0;
                foreach (var child in Children)
                {
                    var childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                    if (Style.Display == "flex")
                    {
                        if (isRow)
                        {
                            width += childSize.X;
                            height = Math.Max(height, childSize.Y);
                        }
                        else
                        {
                            height += childSize.Y;
                            width = Math.Max(width, childSize.X);
                        }
                    }
                    else // block
                    {
                        height += childSize.Y;
                        width = Math.Max(width, childSize.X);
                    }
                }
                if (Style.Display == "flex")
                {
                    if (isRow)
                    {
                        width += gap * (Children.Count - 1);
                    }
                    else
                    {
                        height += gap * (Children.Count - 1);
                    }
                }
            }
            width += pad.W + pad.Y + borderW * 2 + margin.W + margin.Y;
            height += pad.X + pad.Z + borderW * 2 + margin.X + margin.Z;
            return new Vector2(width, height);
        }

        public virtual void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none") return;
            CssStyle effectiveStyle = Style;
            if (IsHover && PseudoStyles.TryGetValue("hover", out var hoverStyle))
            {
                effectiveStyle = MergeStyles(Style, hoverStyle);
            }
            if (IsActive && PseudoStyles.TryGetValue("active", out var activeStyle))
            {
                effectiveStyle = MergeStyles(effectiveStyle, activeStyle);
            }
            if (effectiveStyle.BackgroundColor != Vector4.Zero)
            {
                Matrix4x4 ortho = Matrix4x4.CreateOrthographicOffCenter(0, viewportWidth, viewportHeight, 0, -1, 1);
                quadRenderer.DrawQuad(ComputedPosition, new Vector2(ComputedWidth, ComputedHeight), effectiveStyle.BackgroundColor, ortho);
            }
            float borderW = effectiveStyle.BorderWidth;
            Vector4 borderC = effectiveStyle.BorderColor;
            if (borderW > 0 && borderC != Vector4.Zero && effectiveStyle.BorderStyle != "none")
            {
                Matrix4x4 ortho = Matrix4x4.CreateOrthographicOffCenter(0, viewportWidth, viewportHeight, 0, -1, 1);
                // Top
                quadRenderer.DrawQuad(new Vector2(ComputedPosition.X, ComputedPosition.Y), new Vector2(ComputedWidth, borderW), borderC, ortho);
                // Bottom
                quadRenderer.DrawQuad(new Vector2(ComputedPosition.X, ComputedPosition.Y + ComputedHeight - borderW), new Vector2(ComputedWidth, borderW), borderC, ortho);
                // Left
                quadRenderer.DrawQuad(new Vector2(ComputedPosition.X, ComputedPosition.Y), new Vector2(borderW, ComputedHeight), borderC, ortho);
                // Right
                quadRenderer.DrawQuad(new Vector2(ComputedPosition.X + ComputedWidth - borderW, ComputedPosition.Y), new Vector2(borderW, ComputedHeight), borderC, ortho);
            }
            foreach (var child in Children)
            {
                child.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight);
            }
        }

        private CssStyle MergeStyles(CssStyle baseStyle, CssStyle overrideStyle)
        {
            CssStyle merged = new CssStyle();
            // Copy base
            merged.Position = baseStyle.Position;
            merged.LeftStr = baseStyle.LeftStr;
            merged.TopStr = baseStyle.TopStr;
            merged.WidthStr = baseStyle.WidthStr;
            merged.HeightStr = baseStyle.HeightStr;
            merged.Background = baseStyle.Background;
            merged.BackgroundColor = baseStyle.BackgroundColor;
            merged.Color = baseStyle.Color;
            merged.TextColor = baseStyle.TextColor;
            merged.FontSizeStr = baseStyle.FontSizeStr;
            merged.FontSize = baseStyle.FontSize;
            merged.Display = baseStyle.Display;
            merged.FlexDirection = baseStyle.FlexDirection;
            merged.AlignItems = baseStyle.AlignItems;
            merged.JustifyContent = baseStyle.JustifyContent;
            merged.PaddingStr = baseStyle.PaddingStr;
            merged.MarginStr = baseStyle.MarginStr;
            merged.GapStr = baseStyle.GapStr;
            merged.TextAlign = baseStyle.TextAlign;
            merged.WhiteSpace = baseStyle.WhiteSpace;
            merged.TextTransform = baseStyle.TextTransform;
            merged.BorderWidthStr = baseStyle.BorderWidthStr;
            merged.BorderWidth = baseStyle.BorderWidth;
            merged.BorderStyle = baseStyle.BorderStyle;
            merged.BorderColor = baseStyle.BorderColor;
            merged.BoxSizing = baseStyle.BoxSizing;
            merged.MaxWidthStr = baseStyle.MaxWidthStr;

            // Override with overrideStyle if set
            if (!string.IsNullOrEmpty(overrideStyle.Position)) merged.Position = overrideStyle.Position;
            if (!string.IsNullOrEmpty(overrideStyle.LeftStr)) merged.LeftStr = overrideStyle.LeftStr;
            if (!string.IsNullOrEmpty(overrideStyle.TopStr)) merged.TopStr = overrideStyle.TopStr;
            if (!string.IsNullOrEmpty(overrideStyle.WidthStr)) merged.WidthStr = overrideStyle.WidthStr;
            if (!string.IsNullOrEmpty(overrideStyle.HeightStr)) merged.HeightStr = overrideStyle.HeightStr;
            if (!string.IsNullOrEmpty(overrideStyle.Background)) merged.Background = overrideStyle.Background;
            if (overrideStyle.BackgroundColor != Vector4.Zero) merged.BackgroundColor = overrideStyle.BackgroundColor;
            if (!string.IsNullOrEmpty(overrideStyle.Color)) merged.Color = overrideStyle.Color;
            if (overrideStyle.TextColor != Vector4.Zero) merged.TextColor = overrideStyle.TextColor;
            if (!string.IsNullOrEmpty(overrideStyle.FontSizeStr)) merged.FontSizeStr = overrideStyle.FontSizeStr;
            if (!string.IsNullOrEmpty(overrideStyle.Display)) merged.Display = overrideStyle.Display;
            if (!string.IsNullOrEmpty(overrideStyle.FlexDirection)) merged.FlexDirection = overrideStyle.FlexDirection;
            if (!string.IsNullOrEmpty(overrideStyle.AlignItems)) merged.AlignItems = overrideStyle.AlignItems;
            if (!string.IsNullOrEmpty(overrideStyle.JustifyContent)) merged.JustifyContent = overrideStyle.JustifyContent;
            if (!string.IsNullOrEmpty(overrideStyle.PaddingStr)) merged.PaddingStr = overrideStyle.PaddingStr;
            if (!string.IsNullOrEmpty(overrideStyle.MarginStr)) merged.MarginStr = overrideStyle.MarginStr;
            if (!string.IsNullOrEmpty(overrideStyle.GapStr)) merged.GapStr = overrideStyle.GapStr;
            if (!string.IsNullOrEmpty(overrideStyle.TextAlign)) merged.TextAlign = overrideStyle.TextAlign;
            if (!string.IsNullOrEmpty(overrideStyle.WhiteSpace)) merged.WhiteSpace = overrideStyle.WhiteSpace;
            if (!string.IsNullOrEmpty(overrideStyle.TextTransform)) merged.TextTransform = overrideStyle.TextTransform;
            if (!string.IsNullOrEmpty(overrideStyle.BorderWidthStr)) merged.BorderWidthStr = overrideStyle.BorderWidthStr;
            if (!string.IsNullOrEmpty(overrideStyle.BorderStyle)) merged.BorderStyle = overrideStyle.BorderStyle;
            if (overrideStyle.BorderColor != Vector4.Zero) merged.BorderColor = overrideStyle.BorderColor;
            if (!string.IsNullOrEmpty(overrideStyle.BoxSizing)) merged.BoxSizing = overrideStyle.BoxSizing;
            if (!string.IsNullOrEmpty(overrideStyle.MaxWidthStr)) merged.MaxWidthStr = overrideStyle.MaxWidthStr;

            return merged;
        }

        public float ParseSize(string s, float parent, float vw, float vh)
        {
            if (string.IsNullOrEmpty(s) || s == "auto") return float.NaN;
            s = s.Trim();
            float value;
            if (float.TryParse(s, out value)) return value;
            if (s.EndsWith("%"))
            {
                value = float.Parse(s.Replace("%", ""));
                return value / 100 * parent;
            }
            else if (s.EndsWith("vh"))
            {
                value = float.Parse(s.Replace("vh", ""));
                return value / 100 * vh;
            }
            else if (s.EndsWith("vw"))
            {
                value = float.Parse(s.Replace("vw", ""));
                return value / 100 * vw;
            }
            else if (s.EndsWith("px"))
            {
                value = float.Parse(s.Replace("px", ""));
                return value;
            }
            else if (s.EndsWith("em"))
            {
                value = float.Parse(s.Replace("em", ""));
                return value * parent;
            }
            return float.NaN;
        }

        protected Vector4 ParsePadding(string s, float parent, float vw, float vh)
        {
            if (string.IsNullOrEmpty(s)) return Vector4.Zero;
            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float GetVal(int index, float defaultVal)
            {
                if (index < parts.Length)
                {
                    float val = ParseSize(parts[index], parent, vw, vh);
                    if (float.IsNaN(val)) val = defaultVal;
                    return val;
                }
                return defaultVal;
            }
            float top = GetVal(0, 0);
            float right = GetVal(1, top);
            float bottom = GetVal(2, top);
            float left = GetVal(3, right);
            return new Vector4(top, right, bottom, left);
        }

        public virtual bool HandleClick(Vector2 mousePos)
        {
            if (Style.Display == "none") return false;
            if (mousePos.X >= ComputedPosition.X && mousePos.X <= ComputedPosition.X + ComputedWidth &&
                mousePos.Y >= ComputedPosition.Y && mousePos.Y <= ComputedPosition.Y + ComputedHeight)
            {
                foreach (var child in Children)
                {
                    if (child.HandleClick(mousePos)) return true;
                }
                return true; // If no child handled, assume element handles if clickable
            }
            return false;
        }
    }
}