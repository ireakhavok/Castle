// SiegeEngine.UI/HtmlElement.cs
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
        public HtmlElement Parent { get; set; }
        public Vector2 ComputedPosition { get; set; }
        public float ComputedWidth { get; set; }
        public float ComputedHeight { get; set; }
        public void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none")
            {
                ComputedWidth = 0;
                ComputedHeight = 0;
                return;
            }
            float left = ParseSize(Style.LeftStr, parentWidth, viewportWidth, viewportHeight);
            if (float.IsNaN(left)) left = 0;
            float top = ParseSize(Style.TopStr, parentHeight, viewportWidth, viewportHeight);
            if (float.IsNaN(top)) top = 0;
            float w = ParseSize(Style.WidthStr, parentWidth, viewportWidth, viewportHeight);
            float h = ParseSize(Style.HeightStr, parentHeight, viewportWidth, viewportHeight);
            Vector4 pad = ParsePadding(Style.PaddingStr, parentWidth, viewportWidth, viewportHeight);
            float innerParentWidth = parentWidth - pad.W - pad.Y;
            float innerParentHeight = parentHeight - pad.X - pad.Z;
            if (float.IsNaN(w) || w < 0)
            {
                w = GetAutoWidth(innerParentWidth, viewportWidth, viewportHeight);
            }
            if (float.IsNaN(h) || h < 0)
            {
                h = GetAutoHeight(innerParentHeight, viewportWidth, viewportHeight);
            }
            ComputedWidth = w;
            ComputedHeight = h;
            if (Style.Position == "absolute")
            {
                ComputedPosition = new Vector2(left, top);
            }
            else
            {
                ComputedPosition = new Vector2(parentPositionX + left + pad.W, parentPositionY + top + pad.X);
            }
            // Layout children
            if (Children.Count > 0)
            {
                if (Style.Display == "flex")
                {
                    LayoutFlexChildren(viewportWidth, viewportHeight);
                }
                else // block
                {
                    LayoutBlockChildren(viewportWidth, viewportHeight);
                }
            }
        }
        private void LayoutFlexChildren(float viewportWidth, float viewportHeight)
        {
            bool isRow = Style.FlexDirection == "row";
            float availableMain = isRow ? ComputedWidth : ComputedHeight;
            float availableCross = isRow ? ComputedHeight : ComputedWidth;
            // Calculate base sizes
            List<float> childBaseMain = new List<float>();
            float totalBaseMain = 0;
            foreach (var child in Children)
            {
                float childW = ParseSize(isRow ? child.Style.WidthStr : child.Style.HeightStr, availableMain, viewportWidth, viewportHeight);
                float baseMain = float.IsNaN(childW) ? (isRow ? child.ComputeIntrinsicSize(viewportWidth, viewportHeight).X : child.ComputeIntrinsicSize(viewportWidth, viewportHeight).Y) : childW;
                childBaseMain.Add(baseMain);
                totalBaseMain += baseMain;
            }
            // Simple distribution for now, no grow/shrink
            float scale = 1.0f;
            if (totalBaseMain > availableMain)
            {
                scale = availableMain / totalBaseMain;
            }
            float childPosMain = 0;
            float totalMain = childBaseMain.Sum();
            if (Style.JustifyContent == "center")
            {
                childPosMain = (availableMain - totalMain) / 2;
            }
            else if (Style.JustifyContent == "space-between")
            {
                if (Children.Count > 1)
                {
                    childPosMain = (availableMain - totalMain) / (Children.Count - 1);
                }
            } // add more
            float spacing = 0;
            if (Style.JustifyContent == "space-between" && Children.Count > 1)
            {
                spacing = (availableMain - totalMain) / (Children.Count - 1);
            }
            for (int j = 0; j < Children.Count; j++)
            {
                var child = Children[j];
                float childMain = childBaseMain[j] * scale;
                float childCross = ParseSize(isRow ? child.Style.HeightStr : child.Style.WidthStr, availableCross, viewportWidth, viewportHeight);
                if (float.IsNaN(childCross)) childCross = isRow ? child.ComputeIntrinsicSize(viewportWidth, viewportHeight).Y : child.ComputeIntrinsicSize(viewportWidth, viewportHeight).X;
                float offsetCross = 0;
                if (Style.AlignItems == "center")
                {
                    offsetCross = (availableCross - childCross) / 2;
                }
                float childPosX = ComputedPosition.X + (isRow ? childPosMain : offsetCross);
                float childPosY = ComputedPosition.Y + (isRow ? offsetCross : childPosMain);
                child.ComputeLayout(childPosX, childPosY, isRow ? childMain : childCross, isRow ? childCross : childMain, viewportWidth, viewportHeight);
                childPosMain += childMain + spacing;
            }
        }
        private void LayoutBlockChildren(float viewportWidth, float viewportHeight)
        {
            float currentY = 0;
            foreach (var child in Children)
            {
                float childW = ParseSize(child.Style.WidthStr, ComputedWidth, viewportWidth, viewportHeight);
                if (float.IsNaN(childW)) childW = GetAutoWidth(ComputedWidth, viewportWidth, viewportHeight);
                float childH = ParseSize(child.Style.HeightStr, ComputedHeight - currentY, viewportWidth, viewportHeight);
                if (float.IsNaN(childH)) childH = child.ComputeIntrinsicSize(viewportWidth, viewportHeight).Y;
                child.ComputeLayout(ComputedPosition.X, ComputedPosition.Y + currentY, childW, childH, viewportWidth, viewportHeight);
                currentY += child.ComputedHeight;
            }
        }
        private float GetAutoWidth(float parentWidth, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "block" || Style.Display == "flex")
            {
                return parentWidth;
            }
            return ComputeIntrinsicSize(viewportWidth, viewportHeight).X;
        }
        private float GetAutoHeight(float parentHeight, float viewportWidth, float viewportHeight)
        {
            return ComputeIntrinsicSize(viewportWidth, viewportHeight).Y;
        }
        private Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight)
        {
            float width = 0;
            float height = 0;
            Vector4 pad = ParsePadding(Style.PaddingStr, 0, viewportWidth, viewportHeight);
            if (Children.Count == 0)
            {
                if (this is TextElement text)
                {
                    float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
                    width = (text.Content?.Length ?? 0) * fs * 0.6f;
                    height = fs * 1.2f;
                }
            }
            else
            {
                bool isRow = Style.FlexDirection == "row";
                foreach (var child in Children)
                {
                    var childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight);
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
            }
            width += pad.W + pad.Y;
            height += pad.X + pad.Z;
            return new Vector2(width, height);
        }
        public virtual void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none") return;
            if (Style.BackgroundColor != Vector4.Zero)
            {
                Matrix4x4 ortho = Matrix4x4.CreateOrthographicOffCenter(0, viewportWidth, viewportHeight, 0, -1, 1);
                quadRenderer.DrawQuad(ComputedPosition, new Vector2(ComputedWidth, ComputedHeight), Style.BackgroundColor, ortho);
            }
            foreach (var child in Children)
            {
                child.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight);
            }
        }
        protected float ParseSize(string s, float parent, float vw, float vh)
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