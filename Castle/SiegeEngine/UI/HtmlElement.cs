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
        public virtual void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, Vector2 parentPosition, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight)
        {
            // Calculate position and size
            float left = ParseSize(Style.LeftStr, parentWidth, viewportWidth, viewportHeight);
            float top = ParseSize(Style.TopStr, parentHeight, viewportWidth, viewportHeight);
            float w = ParseSize(Style.WidthStr, parentWidth, viewportWidth, viewportHeight);
            float h = ParseSize(Style.HeightStr, parentHeight, viewportWidth, viewportHeight);
            Vector4 pad = ParsePadding(Style.PaddingStr, parentWidth, viewportWidth, viewportHeight);
            Vector2 position = parentPosition;
            if (Style.Position == "absolute")
            {
                position = new Vector2(left, top);
            }
            else if (Style.Position == "relative")
            {
                position += new Vector2(left, top);
            }
            position.X += pad.W;
            position.Y += pad.X;
            w -= pad.W + pad.Y;
            h -= pad.X + pad.Z;
            // Render background quad if color set
            if (Style.BackgroundColor != Vector4.Zero)
            {
                Matrix4x4 ortho = Matrix4x4.CreateOrthographicOffCenter(0, viewportWidth, viewportHeight, 0, -1, 1);
                quadRenderer.DrawQuad(position, new Vector2(w, h), Style.BackgroundColor, ortho);
            }
            // Render children with layout
            if (Style.Display == "flex")
            {
                float availableMain = Style.FlexDirection == "row" ? w : h;
                float availableCross = Style.FlexDirection == "row" ? h : w;
                float totalFixedMain = 0;
                List<float> childMainSizes = new List<float>();
                List<float> childCrossSizes = new List<float>();
                int autoCount = 0;
                for (int j = 0; j < Children.Count; j++)
                {
                    var child = Children[j];
                    float childMain = ParseSize(Style.FlexDirection == "row" ? child.Style.WidthStr : child.Style.HeightStr, availableMain, viewportWidth, viewportHeight);
                    float childCross = ParseSize(Style.FlexDirection == "row" ? child.Style.HeightStr : child.Style.WidthStr, availableCross, viewportWidth, viewportHeight);
                    childMainSizes.Add(childMain);
                    childCrossSizes.Add(childCross);
                    if ((Style.FlexDirection == "row" ? child.Style.WidthStr : child.Style.HeightStr) == "auto")
                    {
                        autoCount++;
                    }
                    else
                    {
                        totalFixedMain += childMain;
                    }
                }
                float remainingMain = availableMain - totalFixedMain;
                float autoMain = autoCount > 0 ? remainingMain / autoCount : 0;
                for (int j = 0; j < Children.Count; j++)
                {
                    if ((Style.FlexDirection == "row" ? Children[j].Style.WidthStr : Children[j].Style.HeightStr) == "auto")
                    {
                        childMainSizes[j] = autoMain;
                    }
                }
                float offsetMain = 0;
                float totalMain = childMainSizes.Sum();
                if (Style.JustifyContent == "center")
                {
                    offsetMain = (availableMain - totalMain) / 2;
                } // add space-between etc if needed
                Vector2 childPos = position;
                if (Style.FlexDirection == "row") childPos.X += offsetMain;
                else childPos.Y += offsetMain;
                for (int j = 0; j < Children.Count; j++)
                {
                    var child = Children[j];
                    float childW = Style.FlexDirection == "row" ? childMainSizes[j] : childCrossSizes[j];
                    float childH = Style.FlexDirection == "row" ? childCrossSizes[j] : childMainSizes[j];
                    float offsetCross = 0;
                    if (Style.AlignItems == "center")
                    {
                        offsetCross = (availableCross - childCrossSizes[j]) / 2;
                    }
                    Vector2 childStart = childPos;
                    if (Style.FlexDirection == "row") childStart.Y += offsetCross;
                    else childStart.X += offsetCross;
                    child.Render(renderContext, textRenderer, quadRenderer, childStart, childW, childH, viewportWidth, viewportHeight);
                    if (Style.FlexDirection == "row") childPos.X += childMainSizes[j];
                    else childPos.Y += childMainSizes[j];
                }
            }
            else // block
            {
                Vector2 childPos = position;
                float currentH = 0;
                foreach (var child in Children)
                {
                    float childW = ParseSize(child.Style.WidthStr, w, viewportWidth, viewportHeight);
                    float childH = ParseSize(child.Style.HeightStr, h - currentH, viewportWidth, viewportHeight);
                    child.Render(renderContext, textRenderer, quadRenderer, childPos, childW, childH, viewportWidth, viewportHeight);
                    childPos.Y += childH;
                    currentH += childH;
                }
            }
        }
        protected float ParseSize(string s, float parent, float vw, float vh)
        {
            if (string.IsNullOrEmpty(s) || s == "auto") return parent;
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
            return 0;
        }
        protected Vector4 ParsePadding(string s, float parent, float vw, float vh)
        {
            if (string.IsNullOrEmpty(s)) return Vector4.Zero;
            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float GetVal(int index, float defaultVal)
            {
                if (index < parts.Length)
                    return ParseSize(parts[index], parent, vw, vh);
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
            // Check bounds and handle if button
            return false;
        }
    }
}