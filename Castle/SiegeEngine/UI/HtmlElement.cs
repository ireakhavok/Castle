using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
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
        public float ComputedContentX { get; set; }
        public float ComputedContentY { get; set; }
        public float ComputedContentWidth { get; set; }
        public float ComputedContentHeight { get; set; }
        public float ComputedBackgroundX { get; set; }
        public float ComputedBackgroundY { get; set; }
        public float ComputedBackgroundWidth { get; set; }
        public float ComputedBackgroundHeight { get; set; }
        public float BorderWidth { get; set; }
        public bool IsHover { get; set; }
        public bool IsActive { get; set; }
        public bool Checked { get; set; }
        public bool IsTarget { get; set; }
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
            float w = ParseSize(Style.WidthStr, parentWidth, viewportWidth, viewportHeight);
            float h = ParseSize(Style.HeightStr, parentHeight, viewportWidth, viewportHeight);
            float maxW = ParseSize(Style.MaxWidthStr, parentWidth, viewportWidth, viewportHeight);
            float borderW = ParseSize(Style.BorderWidthStr, parentWidth, viewportWidth, viewportHeight);
            if (float.IsNaN(borderW)) borderW = 0;
            BorderWidth = borderW;
            Vector4 pad = ParsePadding(Style.PaddingStr, parentWidth, viewportWidth, viewportHeight);
            Vector4 margin = ParsePadding(Style.MarginStr, parentWidth, viewportWidth, viewportHeight);
            Style.Margin = margin;
            float contentW, contentH, boxW, boxH;
            if (float.IsNaN(w) || float.IsNaN(h))
            {
                Vector2 intrinsic = ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                if (float.IsNaN(w)) w = intrinsic.X;
                if (float.IsNaN(h)) h = intrinsic.Y;
            }
            if (Style.BoxSizing == "border-box")
            {
                boxW = w;
                boxH = h;
                contentW = w - pad.W - pad.Y - borderW * 2;
                contentH = h - pad.X - pad.Z - borderW * 2;
            }
            else
            {
                contentW = w;
                contentH = h;
                boxW = w + pad.W + pad.Y + borderW * 2;
                boxH = h + pad.X + pad.Z + borderW * 2;
            }
            if (!float.IsNaN(maxW)) boxW = Math.Min(boxW, maxW);
            ComputedWidth = boxW;
            ComputedHeight = boxH;
            ComputedContentWidth = contentW;
            ComputedContentHeight = contentH;
            float boxX = parentPositionX + left + margin.W;
            float boxY = parentPositionY + top + margin.X;
            ComputedPosition = new Vector2(boxX, boxY);
            ComputedContentX = boxX + borderW + pad.W;
            ComputedContentY = boxY + borderW + pad.X;
            ComputedBackgroundX = boxX + borderW;
            ComputedBackgroundY = boxY + borderW;
            ComputedBackgroundWidth = boxW - borderW * 2;
            ComputedBackgroundHeight = boxH - borderW * 2;
            if (Children.Count > 0)
            {
                if (Style.Display == "flex")
                {
                    LayoutFlexChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
                else
                {
                    LayoutBlockChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
            }
        }
        private void LayoutFlexChildren(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            bool isRow = Style.FlexDirection == "row";
            float availableMain = isRow ? ComputedContentWidth : ComputedContentHeight;
            float availableCross = isRow ? ComputedContentHeight : ComputedContentWidth;
            float gap = ParseSize(Style.GapStr, availableMain, viewportWidth, viewportHeight);
            if (float.IsNaN(gap)) gap = 0;
            List<float> childBaseMain = new List<float>();
            List<float> childGrows = new List<float>();
            float totalBaseMain = 0;
            float totalGrow = 0;
            foreach (var child in Children)
            {
                float grow = 0;
                if (!string.IsNullOrEmpty(child.Style.Flex))
                {
                    var flexParts = child.Style.Flex.Split(' ');
                    if (flexParts.Length > 0) float.TryParse(flexParts[0], out grow);
                }
                childGrows.Add(grow);
                totalGrow += grow;
                float childSizeStr = ParseSize(isRow ? child.Style.WidthStr : child.Style.HeightStr, availableMain, viewportWidth, viewportHeight);
                float baseMain = float.IsNaN(childSizeStr) ? (isRow ? child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X : child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y) : childSizeStr;
                childBaseMain.Add(baseMain);
                totalBaseMain += baseMain;
            }
            float totalGap = gap * (Children.Count - 1);
            float extraMain = availableMain - totalBaseMain - totalGap;
            if (extraMain < 0) extraMain = 0;
            if (totalGrow > 0)
            {
                for (int j = 0; j < Children.Count; j++)
                {
                    childBaseMain[j] += (extraMain / totalGrow) * childGrows[j];
                }
            }
            float childPosMain = 0;
            float totalMain = childBaseMain.Sum() + totalGap;
            float spacing = gap;
            if (Style.JustifyContent == "center")
            {
                childPosMain = (availableMain - totalMain) / 2;
                spacing = gap;
            }
            else if (Style.JustifyContent == "space-between")
            {
                if (Children.Count > 1)
                {
                    spacing = (availableMain - childBaseMain.Sum()) / (Children.Count - 1);
                }
            }
            for (int j = 0; j < Children.Count; j++)
            {
                var child = Children[j];
                float childMain = childBaseMain[j];
                float childCrossStr = ParseSize(isRow ? child.Style.HeightStr : child.Style.WidthStr, availableCross, viewportWidth, viewportHeight);
                float childCross = float.IsNaN(childCrossStr) ? (isRow ? child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y : child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X) : childCrossStr;
                if (Style.AlignItems == "stretch" && float.IsNaN(childCrossStr))
                {
                    childCross = availableCross;
                }
                float offsetCross = 0;
                if (Style.AlignItems == "center")
                {
                    offsetCross = (availableCross - childCross) / 2;
                }
                else if (Style.AlignItems == "stretch")
                {
                    offsetCross = 0;
                }
                float childPosX = ComputedContentX + (isRow ? childPosMain : offsetCross);
                float childPosY = ComputedContentY + (isRow ? offsetCross : childPosMain);
                child.ComputeLayout(childPosX, childPosY, isRow ? childMain : childCross, isRow ? childCross : childMain, viewportWidth, viewportHeight, textRenderer, fs);
                childPosMain += childMain + spacing;
            }
        }
        private void LayoutBlockChildren(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            float currentY = 0;
            foreach (var child in Children)
            {
                float childW = ParseSize(child.Style.WidthStr, ComputedContentWidth, viewportWidth, viewportHeight);
                if (float.IsNaN(childW)) childW = GetAutoWidth(ComputedContentWidth, viewportWidth, viewportHeight, textRenderer);
                float childH = ParseSize(child.Style.HeightStr, ComputedContentHeight - currentY, viewportWidth, viewportHeight);
                if (float.IsNaN(childH)) childH = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y;
                child.ComputeLayout(ComputedContentX, ComputedContentY + currentY, childW, childH, viewportWidth, viewportHeight, textRenderer, fs);
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
            if (Style.Display == "none") return new Vector2(0, 0);
            float width = 0;
            float height = 0;
            Vector4 pad = ParsePadding(Style.PaddingStr, 0, viewportWidth, viewportHeight);
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
                float totalGap = gap * (Children.Count - 1);
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
                    else
                    {
                        height += childSize.Y;
                        width = Math.Max(width, childSize.X);
                    }
                }
                if (Style.Display == "flex")
                {
                    if (isRow)
                    {
                        width += totalGap;
                    }
                    else
                    {
                        height += totalGap;
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
            CssStyle effectiveStyle = Style;
            if (IsHover && PseudoStyles.TryGetValue("hover", out CssStyle hover))
            {
                effectiveStyle = hover;
            }
            if (IsActive && PseudoStyles.TryGetValue("active", out CssStyle active))
            {
                effectiveStyle = active;
            }
            if (effectiveStyle.BackgroundColor != Vector4.Zero)
            {
                quadRenderer.DrawQuad(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, ComputedBackgroundHeight, effectiveStyle.BackgroundColor, viewportWidth, viewportHeight);
            }
            float borderW = BorderWidth;
            Vector4 borderC = effectiveStyle.BorderColor;
            if (borderW > 0 && borderC != Vector4.Zero && effectiveStyle.BorderStyle != "none")
            {
                quadRenderer.DrawQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, borderW, borderC, viewportWidth, viewportHeight);
                quadRenderer.DrawQuad(ComputedPosition.X, ComputedPosition.Y + ComputedHeight - borderW, ComputedWidth, borderW, borderC, viewportWidth, viewportHeight);
                quadRenderer.DrawQuad(ComputedPosition.X, ComputedPosition.Y, borderW, ComputedHeight, borderC, viewportWidth, viewportHeight);
                quadRenderer.DrawQuad(ComputedPosition.X + ComputedWidth - borderW, ComputedPosition.Y, borderW, ComputedHeight, borderC, viewportWidth, viewportHeight);
            }
            foreach (var child in Children)
            {
                child.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight);
            }
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
                return true;
            }
            return false;
        }
    }
}