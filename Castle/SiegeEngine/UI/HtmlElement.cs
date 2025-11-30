// Folder: SiegeEngine.UI
// File: HtmlElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
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
        public Vector4 BorderWidth { get; set; }
        public bool IsHover { get; set; }
        public bool IsActive { get; set; }
        public bool Checked { get; set; }
        public bool IsTarget { get; set; }
        public bool IsFocused { get; set; }
        public string OnClickJS { get; set; }
        public string OnChangeJS { get; set; }
        public string OnMouseEnterJS { get; set; }
        public string OnMouseLeaveJS { get; set; }
        public string OnMouseDownJS { get; set; }
        public string OnMouseUpJS { get; set; }
        public string OnFocusJS { get; set; }
        public string OnBlurJS { get; set; }
        private BackgroundRenderer _bgRenderer;
        private string _baseDir;
        protected Matrix4x4 ComputedTransform;
        protected Matrix4x4 ComputedFullTransform;
        public bool IsDescendantOf(HtmlElement ancestor)
        {
            var current = this;
            while (current != null)
            {
                if (current == ancestor) return true;
                current = current.Parent;
            }
            return false;
        }
        public string GetEffectiveDisplay()
        {
            CssStyle effective = Style;
            if (IsTarget && PseudoStyles.TryGetValue("target", out CssStyle ts))
                effective = ts;
            if (Checked && PseudoStyles.TryGetValue("checked", out CssStyle cs))
                effective = cs;
            if (IsHover && PseudoStyles.TryGetValue("hover", out CssStyle hs))
                effective = hs;
            if (IsActive && PseudoStyles.TryGetValue("active", out CssStyle a))
                effective = a;
            return effective.Display;
        }
        private HtmlElement FindContainingBlock()
        {
            HtmlElement current = Parent;
            while (current != null && current.Style.Position == "static")
            {
                current = current.Parent;
            }
            return current;
        }
        public virtual void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            CssStyle effectiveStyle = Style;
            if (IsTarget && PseudoStyles.TryGetValue("target", out CssStyle ts))
            {
                effectiveStyle = ts;
            }
            if (Checked && PseudoStyles.TryGetValue("checked", out CssStyle cs))
                effectiveStyle = cs;
            if (effectiveStyle.Display == "none")
            {
                ComputedWidth = 0;
                ComputedHeight = 0;
                return;
            }
            float baseX = parentPositionX;
            float baseY = parentPositionY;
            float refWidth = parentWidth;
            float refHeight = parentHeight;
            if (effectiveStyle.Position == "absolute")
            {
                HtmlElement cb = FindContainingBlock();
                baseX = cb == null ? 0 : cb.ComputedContentX;
                baseY = cb == null ? 0 : cb.ComputedContentY;
                refWidth = cb == null ? viewportWidth : cb.ComputedContentWidth;
                refHeight = cb == null ? viewportHeight : cb.ComputedContentHeight;
            }
            else if (effectiveStyle.Position == "fixed")
            {
                baseX = 0;
                baseY = 0;
                refWidth = viewportWidth;
                refHeight = viewportHeight;
            }
            else
            {
                baseX = parentPositionX;
                baseY = parentPositionY;
                refWidth = parentWidth;
                refHeight = parentHeight;
            }
            float fs = ParseSize(effectiveStyle.FontSizeStr, parentFs, viewportWidth, viewportHeight);
            if (float.IsNaN(fs)) fs = parentFs;
            Style.FontSize = fs;
            float left = ParseSize(effectiveStyle.LeftStr, refWidth, viewportWidth, viewportHeight);
            float top = ParseSize(effectiveStyle.TopStr, refHeight, viewportWidth, viewportHeight);
            float right = ParseSize(effectiveStyle.RightStr, refWidth, viewportWidth, viewportHeight);
            float bottom = ParseSize(effectiveStyle.BottomStr, refHeight, viewportWidth, viewportHeight);
            float w = ParseSize(effectiveStyle.WidthStr, refWidth, viewportWidth, viewportHeight);
            float h = ParseSize(effectiveStyle.HeightStr, refHeight, viewportWidth, viewportHeight);
            bool isBlockOrFlex = effectiveStyle.Display == "block" || effectiveStyle.Display == "flex";
            bool isStaticOrRelative = string.IsNullOrEmpty(effectiveStyle.Position) || effectiveStyle.Position == "static" || effectiveStyle.Position == "relative";
            if (isBlockOrFlex && isStaticOrRelative && float.IsNaN(w))
            {
                w = refWidth;
            }
            float minW = ParseSize(effectiveStyle.MinWidthStr, refWidth, viewportWidth, viewportHeight);
            float minH = ParseSize(effectiveStyle.MinHeightStr, refHeight, viewportWidth, viewportHeight);
            float maxW = ParseSize(effectiveStyle.MaxWidthStr, refWidth, viewportWidth, viewportHeight);
            float maxH = ParseSize(effectiveStyle.MaxHeightStr, refHeight, viewportWidth, viewportHeight);
            Vector4 pad = ParsePaddings(effectiveStyle, refWidth, viewportWidth, viewportHeight);
            Vector4 margin = ParseMargins(effectiveStyle, refWidth, viewportWidth, viewportHeight);
            Vector4 borderW = ParseBorderWidths(effectiveStyle, refWidth, viewportWidth, viewportHeight);
            if (Parent == null)
            {
                if (float.IsNaN(w)) w = viewportWidth;
                if (float.IsNaN(h)) h = viewportHeight;
            }
            if (float.IsNaN(w) || float.IsNaN(h))
            {
                Vector2 intrinsic = ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                if (float.IsNaN(w)) w = intrinsic.X;
                if (float.IsNaN(h)) h = intrinsic.Y;
            }
            if (!float.IsNaN(forcedWidth)) w = forcedWidth;
            if (!float.IsNaN(forcedHeight)) h = forcedHeight;
            if (!float.IsNaN(minW)) w = Math.Max(w, minW);
            if (!float.IsNaN(minH)) h = Math.Max(h, minH);
            if (!float.IsNaN(maxW)) w = Math.Min(w, maxW);
            if (!float.IsNaN(maxH)) h = Math.Min(h, maxH);
            if (float.IsNaN(w)) w = 0;
            if (float.IsNaN(h)) h = 0;
            float boxW = w;
            float boxH = h;
            float contentW = w - pad.W - pad.Y - borderW.W - borderW.Y;
            float contentH = h - pad.X - pad.Z - borderW.X - borderW.Z;
            if (float.IsNaN(boxW)) boxW = 0;
            if (float.IsNaN(boxH)) boxH = 0;
            if (float.IsNaN(contentW)) contentW = 0;
            if (float.IsNaN(contentH)) contentH = 0;
            ComputedWidth = boxW;
            ComputedHeight = boxH;
            ComputedContentWidth = contentW;
            ComputedContentHeight = contentH;
            float boxX = baseX;
            float boxY = baseY;
            if (effectiveStyle.Position == "absolute" || effectiveStyle.Position == "fixed")
            {
                if (!float.IsNaN(left))
                {
                    boxX += left;
                }
                else if (!float.IsNaN(right))
                {
                    boxX += refWidth - right - boxW;
                }
                if (!float.IsNaN(top))
                {
                    boxY += top;
                }
                else if (!float.IsNaN(bottom))
                {
                    boxY += refHeight - bottom - boxH;
                }
            }
            ComputedPosition = new Vector2(boxX, boxY);
            ComputedBackgroundX = boxX + borderW.W;
            ComputedBackgroundY = boxY + borderW.X;
            ComputedBackgroundWidth = boxW - borderW.W - borderW.Y;
            ComputedBackgroundHeight = boxH - borderW.X - borderW.Z;
            ComputedContentX = ComputedBackgroundX + pad.W;
            ComputedContentY = ComputedBackgroundY + pad.X;
            this.BorderWidth = borderW;
            Style.Margin = margin;
            if (Children.Count > 0)
            {
                if (effectiveStyle.Display == "flex")
                {
                    LayoutFlexChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
                else
                {
                    LayoutBlockChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
            }
            ComputedTransform = ComputeTransform(viewportWidth, viewportHeight);
        }
        public virtual void UpdateFullTransforms(Matrix4x4 parentMatrix)
        {
            ComputedFullTransform = parentMatrix * ComputedTransform;
            foreach (var child in Children)
            {
                child.UpdateFullTransforms(ComputedFullTransform);
            }
        }
        public void PrepareResources(string baseDir, IControlContext controlContext, IntPtr window, IRenderContext renderContext, ShaderProgram shader)
        {
            _baseDir = baseDir;
            if (!string.IsNullOrEmpty(Style.BackgroundImage))
            {
                string relativePath = Style.BackgroundImage;
                string fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                Console.WriteLine($"HtmlElement: Attempting to load background texture from: {fullPath}");
                if (File.Exists(fullPath))
                {
                    _bgRenderer = new BackgroundRenderer(controlContext, window, renderContext);
                    _bgRenderer.Initialize(fullPath, shader);
                }
                else
                {
                    Console.WriteLine($"HtmlElement: Background file not found: {fullPath}");
                }
            }
            foreach (var child in Children)
            {
                child.PrepareResources(baseDir, controlContext, window, renderContext, shader);
            }
        }
        private void LayoutFlexChildren(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            List<HtmlElement> visibleChildren = Children.Where(c => c.GetEffectiveDisplay() != "none").ToList();
            if (visibleChildren.Count == 0) return;
            List<HtmlElement> normalChildren = visibleChildren.Where(c => c.Style.Position != "absolute" && c.Style.Position != "fixed").ToList();
            List<HtmlElement> positionedChildren = visibleChildren.Where(c => c.Style.Position == "absolute" || c.Style.Position == "fixed").ToList();
            bool isRow = string.IsNullOrEmpty(Style.FlexDirection) || Style.FlexDirection == "row";
            float availableMain = isRow ? ComputedContentWidth : ComputedContentHeight;
            float availableCross = isRow ? ComputedContentHeight : ComputedContentWidth;
            float gap = ParseSize(Style.GapStr, availableMain, viewportWidth, viewportHeight);
            if (float.IsNaN(gap)) gap = 0;
            List<float> childBaseMain = new List<float>();
            List<float> childGrow = new List<float>();
            List<float> childShrink = new List<float>(); // assume 1 if not set
            float totalGrow = 0;
            float totalShrink = 0;
            float totalBaseMain = 0;
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                float grow = 0;
                float shrink = 1f;
                if (!string.IsNullOrEmpty(child.Style.Flex))
                {
                    var flexParts = child.Style.Flex.Split(' ');
                    if (flexParts.Length > 0) float.TryParse(flexParts[0], out grow);
                    if (flexParts.Length > 1) float.TryParse(flexParts[1], out shrink);
                }
                childGrow.Add(grow);
                childShrink.Add(shrink);
                totalGrow += grow;
                string main_str_raw = isRow ? child.Style.WidthStr : child.Style.HeightStr;
                float mainStr = ParseSize(main_str_raw, availableMain, viewportWidth, viewportHeight);
                float min_main = ParseSize(isRow ? child.Style.MinWidthStr : child.Style.MinHeightStr, availableMain, viewportWidth, viewportHeight);
                float max_main = ParseSize(isRow ? child.Style.MaxWidthStr : child.Style.MaxHeightStr, availableMain, viewportWidth, viewportHeight);
                if (!float.IsNaN(mainStr))
                {
                    if (!float.IsNaN(min_main)) mainStr = Math.Max(mainStr, min_main);
                    if (!float.IsNaN(max_main)) mainStr = Math.Min(mainStr, max_main);
                }
                Vector4 pad = ParsePaddings(child.Style, 0, viewportWidth, viewportHeight);
                Vector4 border_w = ParseBorderWidths(child.Style, 0, viewportWidth, viewportHeight);
                float pad_start = isRow ? pad.W : pad.X;
                float pad_end = isRow ? pad.Y : pad.Z;
                float border_start = isRow ? border_w.W : border_w.X;
                float border_end = isRow ? border_w.Y : border_w.Z;
                float baseMain;
                if (float.IsNaN(mainStr))
                {
                    baseMain = isRow ? child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X : child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y;
                }
                else
                {
                    float specified = mainStr;
                    if (child.Style.BoxSizing != "border-box")
                    {
                        specified += pad_start + pad_end + border_start + border_end;
                    }
                    baseMain = specified;
                }
                childBaseMain.Add(baseMain);
                totalBaseMain += baseMain;
                totalShrink += shrink * baseMain;
            }
            float totalGap = gap * (normalChildren.Count - 1);
            float free = availableMain - totalBaseMain - totalGap;
            if (free > 0)
            {
                if (totalGrow > 0)
                {
                    for (int i = 0; i < normalChildren.Count; i++)
                    {
                        float extra = (free / totalGrow) * childGrow[i];
                        childBaseMain[i] += extra;
                    }
                }
            }
            else if (free < 0)
            {
                if (totalShrink > 0)
                {
                    for (int i = 0; i < normalChildren.Count; i++)
                    {
                        float reduce = (Math.Abs(free) / totalShrink) * (childShrink[i] * childBaseMain[i]);
                        childBaseMain[i] = Math.Max(0, childBaseMain[i] - reduce);
                    }
                }
            }
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                float max_main = ParseSize(isRow ? child.Style.MaxWidthStr : child.Style.MaxHeightStr, availableMain, viewportWidth, viewportHeight);
                if (!float.IsNaN(max_main)) childBaseMain[i] = Math.Min(childBaseMain[i], max_main);
                float min_main = ParseSize(isRow ? child.Style.MinWidthStr : child.Style.MinHeightStr, availableMain, viewportWidth, viewportHeight);
                if (!float.IsNaN(min_main)) childBaseMain[i] = Math.Max(childBaseMain[i], min_main);
            }
            float sum_border_boxes = 0;
            float sum_fixed_margins = 0;
            int num_auto_main = 0;
            List<float> childMarginStart = new List<float>();
            List<float> childMarginEnd = new List<float>();
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                Vector4 parsedMargin = ParseMargins(child.Style, availableMain, viewportWidth, viewportHeight);
                float start = isRow ? parsedMargin.W : parsedMargin.X;
                float end = isRow ? parsedMargin.Y : parsedMargin.Z;
                float c_start = float.IsNaN(start) ? 0 : start;
                float c_end = float.IsNaN(end) ? 0 : end;
                childMarginStart.Add(c_start);
                childMarginEnd.Add(c_end);
                if (float.IsNaN(start)) num_auto_main++;
                else sum_fixed_margins += start;
                if (float.IsNaN(end)) num_auto_main++;
                else sum_fixed_margins += end;
                sum_border_boxes += childBaseMain[i];
            }
            float total_gap = gap * (normalChildren.Count - 1);
            free = availableMain - sum_border_boxes - sum_fixed_margins - total_gap;
            float auto_size = (num_auto_main > 0 && free > 0) ? free / num_auto_main : 0;
            bool has_auto_main = num_auto_main > 0;
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                Vector4 parsedMargin = ParseMargins(child.Style, availableMain, viewportWidth, viewportHeight);
                float start = isRow ? parsedMargin.W : parsedMargin.X;
                float end = isRow ? parsedMargin.Y : parsedMargin.Z;
                if (float.IsNaN(start)) childMarginStart[i] = auto_size;
                if (float.IsNaN(end)) childMarginEnd[i] = auto_size;
                if (float.IsNaN(childMarginStart[i])) childMarginStart[i] = 0;
                if (float.IsNaN(childMarginEnd[i])) childMarginEnd[i] = 0;
            }
            float sum_outer = 0;
            for (int i = 0; i < normalChildren.Count; i++)
            {
                sum_outer += childBaseMain[i] + childMarginStart[i] + childMarginEnd[i];
            }
            float total_used = sum_outer + total_gap;
            float start_main = 0;
            float justify_spacing = 0;
            if (has_auto_main)
            {
                start_main = 0;
            }
            else
            {
                float extra_free = availableMain - sum_outer - total_gap;
                if (Style.JustifyContent == "space-between")
                {
                    if (normalChildren.Count > 1)
                    {
                        justify_spacing = extra_free / (normalChildren.Count - 1);
                    }
                }
                else if (Style.JustifyContent == "space-around")
                {
                    justify_spacing = extra_free / normalChildren.Count;
                    start_main = justify_spacing / 2;
                }
                else if (Style.JustifyContent == "space-evenly")
                {
                    justify_spacing = extra_free / (normalChildren.Count + 1);
                    start_main = justify_spacing;
                }
                else if (Style.JustifyContent == "center")
                {
                    start_main = extra_free / 2;
                }
                else if (Style.JustifyContent == "flex-end")
                {
                    start_main = extra_free;
                }
                else
                {
                    start_main = 0;
                }
            }
            if (float.IsNaN(start_main)) start_main = 0;
            if (float.IsNaN(justify_spacing)) justify_spacing = 0;
            float current_main = start_main;
            string alignItems = string.IsNullOrEmpty(Style.AlignItems) ? "stretch" : Style.AlignItems;
            for (int i = 0; i < normalChildren.Count; i++)
            {
                if (i > 0) current_main += gap + justify_spacing;
                HtmlElement child = normalChildren[i];
                float item_start = current_main + childMarginStart[i];
                float child_main = childBaseMain[i];
                string cross_str = isRow ? child.Style.HeightStr : child.Style.WidthStr;
                float child_cross_str = ParseSize(cross_str, availableCross, viewportWidth, viewportHeight);
                Vector4 pad_child = ParsePaddings(child.Style, 0, viewportWidth, viewportHeight);
                Vector4 border_child = ParseBorderWidths(child.Style, 0, viewportWidth, viewportHeight);
                float pad_cross_start = isRow ? pad_child.X : pad_child.W;
                float pad_cross_end = isRow ? pad_child.Z : pad_child.Y;
                float border_cross_start = isRow ? border_child.X : border_child.W;
                float border_cross_end = isRow ? border_child.Z : border_child.Y;
                Vector2 intrinsic = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                float child_cross;
                if (float.IsNaN(child_cross_str))
                {
                    child_cross = isRow ? intrinsic.Y : intrinsic.X;
                }
                else
                {
                    child_cross = child_cross_str;
                    if (child.Style.BoxSizing != "border-box")
                    {
                        child_cross += pad_cross_start + pad_cross_end + border_cross_start + border_cross_end;
                    }
                }
                float max_cross = ParseSize(isRow ? child.Style.MaxHeightStr : child.Style.MaxWidthStr, availableCross, viewportWidth, viewportHeight);
                if (!float.IsNaN(max_cross)) child_cross = Math.Min(child_cross, max_cross);
                float min_cross = ParseSize(isRow ? child.Style.MinHeightStr : child.Style.MinWidthStr, availableCross, viewportWidth, viewportHeight);
                if (!float.IsNaN(min_cross)) child_cross = Math.Max(child_cross, min_cross);
                Vector4 parsedMarginCross = ParseMargins(child.Style, availableCross, viewportWidth, viewportHeight);
                float m_cross_start = isRow ? parsedMarginCross.X : parsedMarginCross.W;
                float m_cross_end = isRow ? parsedMarginCross.Z : parsedMarginCross.Y;
                float c_m_cross_start = float.IsNaN(m_cross_start) ? 0 : m_cross_start;
                float c_m_cross_end = float.IsNaN(m_cross_end) ? 0 : m_cross_end;
                int num_auto_cross = 0;
                if (float.IsNaN(m_cross_start)) num_auto_cross++;
                if (float.IsNaN(m_cross_end)) num_auto_cross++;
                float free_cross = availableCross - child_cross - c_m_cross_start - c_m_cross_end;
                float auto_cross_size = (num_auto_cross > 0 && free_cross > 0) ? free_cross / num_auto_cross : 0;
                if (float.IsNaN(m_cross_start)) c_m_cross_start = auto_cross_size;
                if (float.IsNaN(m_cross_end)) c_m_cross_end = auto_cross_size;
                bool has_auto_cross = num_auto_cross > 0;
                float child_cross_for_align = child_cross + c_m_cross_start + c_m_cross_end;
                float offset_margin_box = 0;
                if (has_auto_cross)
                {
                    offset_margin_box = 0;
                }
                else
                {
                    if (alignItems == "center")
                    {
                        offset_margin_box = (availableCross - child_cross_for_align) / 2;
                    }
                    else if (alignItems == "flex-end")
                    {
                        offset_margin_box = availableCross - child_cross_for_align;
                    }
                    else if (alignItems == "stretch")
                    {
                        if (float.IsNaN(child_cross_str))
                        {
                            child_cross = availableCross - c_m_cross_start - c_m_cross_end;
                        }
                        offset_margin_box = 0;
                    }
                    else
                    {
                        offset_margin_box = 0;
                    }
                }
                if (float.IsNaN(offset_margin_box)) offset_margin_box = 0;
                float child_pos_cross = offset_margin_box + c_m_cross_start;
                float child_pos_x = ComputedContentX + (isRow ? item_start : child_pos_cross);
                float child_pos_y = ComputedContentY + (isRow ? child_pos_cross : item_start);
                float child_w = isRow ? child_main : child_cross;
                float child_h = isRow ? child_cross : child_main;
                float forced_width = float.NaN;
                float forced_height = float.NaN;
                if (alignItems == "stretch" && float.IsNaN(child_cross_str))
                {
                    float forced_cross = availableCross - c_m_cross_start - c_m_cross_end;
                    if (isRow)
                    {
                        forced_height = forced_cross;
                    }
                    else
                    {
                        forced_width = forced_cross;
                    }
                }
                child.ComputeLayout(child_pos_x, child_pos_y, child_w, child_h, viewportWidth, viewportHeight, textRenderer, fs, forced_width, forced_height);
                current_main += childBaseMain[i] + childMarginStart[i] + childMarginEnd[i];
                float computed_cross = isRow ? child.ComputedHeight : child.ComputedWidth;
                float allocated_cross = child_cross + c_m_cross_start + c_m_cross_end;
                if (computed_cross < allocated_cross - c_m_cross_start - c_m_cross_end)
                {
                    float diff = allocated_cross - computed_cross - c_m_cross_start - c_m_cross_end;
                    float adjust = 0;
                    if (alignItems == "center")
                    {
                        adjust = diff / 2;
                    }
                    else if (alignItems == "flex-end")
                    {
                        adjust = diff;
                    }
                    var pos = child.ComputedPosition;
                    if (isRow)
                    {
                        pos.Y += adjust;
                    }
                    else
                    {
                        pos.X += adjust;
                    }
                    child.ComputedPosition = pos;
                }
            }
            foreach (var child in positionedChildren)
            {
                child.ComputeLayout(ComputedContentX, ComputedContentY, ComputedContentWidth, ComputedContentHeight, viewportWidth, viewportHeight, textRenderer, fs);
            }
        }
        private void LayoutBlockChildren(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            float currentX = 0;
            float currentY = 0;
            float maxLineH = 0;
            float last_bottom = 0;
            List<HtmlElement> visibleChildren = Children.Where(c => c.GetEffectiveDisplay() != "none").ToList();
            List<HtmlElement> normalChildren = visibleChildren.Where(c => c.Style.Position != "absolute" && c.Style.Position != "fixed").ToList();
            List<HtmlElement> positionedChildren = visibleChildren.Where(c => c.Style.Position == "absolute" || c.Style.Position == "fixed").ToList();
            string textAlign = Style.TextAlign ?? "left";
            List<HtmlElement> currentLine = new List<HtmlElement>();
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                string childDisplay = child.GetEffectiveDisplay();
                bool isInline = childDisplay.StartsWith("inline");
                float childW = ParseSize(child.Style.WidthStr, ComputedContentWidth, viewportWidth, viewportHeight);
                float childH = ParseSize(child.Style.HeightStr, ComputedContentHeight, viewportWidth, viewportHeight);
                Vector4 parsedMargin = ParseMargins(child.Style, ComputedContentHeight, viewportWidth, viewportHeight);
                float m_top = parsedMargin.X;
                float m_bottom = parsedMargin.Z;
                float m_left = parsedMargin.W;
                float m_right = parsedMargin.Y;
                float c_m_top = float.IsNaN(m_top) ? 0 : m_top;
                float c_m_bottom = float.IsNaN(m_bottom) ? 0 : m_bottom;
                float c_m_left = float.IsNaN(m_left) ? 0 : m_left;
                float c_m_right = float.IsNaN(m_right) ? 0 : m_right;
                if (isInline)
                {
                    float availW = ComputedContentWidth - currentX - c_m_left - c_m_right;
                    if (float.IsNaN(childW))
                    {
                        if (child is TextElement && child.Style.WhiteSpace == "normal")
                        {
                            childW = availW;
                        }
                        else
                        {
                            childW = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X;
                        }
                    }
                    if (float.IsNaN(childH)) childH = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y;
                    float effW = childW + c_m_left + c_m_right;
                    if (currentX > 0 && effW > availW)
                    {
                        AlignCurrentLine(currentLine, textAlign, ComputedContentWidth);
                        currentLine.Clear();
                        currentY += maxLineH;
                        currentX = 0;
                        maxLineH = 0;
                    }
                    float child_pos_x = currentX + c_m_left;
                    float child_pos_y = currentY + c_m_top;
                    float forcedW = float.NaN;
                    float forcedH = float.NaN;
                    if (child is TextElement && child.Style.WhiteSpace == "normal")
                    {
                        forcedW = availW;
                    }
                    child.ComputeLayout(ComputedContentX + child_pos_x, ComputedContentY + child_pos_y, ComputedContentWidth, ComputedContentHeight, viewportWidth, viewportHeight, textRenderer, fs, forcedW, forcedH);
                    currentLine.Add(child);
                    currentX += child.ComputedWidth + c_m_left + c_m_right;
                    maxLineH = Math.Max(maxLineH, child.ComputedHeight + c_m_top + c_m_bottom);
                }
                else
                {
                    if (currentLine.Count > 0)
                    {
                        AlignCurrentLine(currentLine, textAlign, ComputedContentWidth);
                        currentLine.Clear();
                        currentY += maxLineH;
                        currentX = 0;
                        maxLineH = 0;
                    }
                    if (float.IsNaN(childW)) childW = ComputedContentWidth;
                    if (float.IsNaN(childH)) childH = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y;
                    float eff = Math.Max(last_bottom, c_m_top);
                    float child_pos_y = currentY + eff;
                    float child_pos_x = 0;
                    float free_side = ComputedContentWidth - childW - c_m_left - c_m_right;
                    if (float.IsNaN(free_side)) free_side = 0;
                    if (float.IsNaN(m_left)) c_m_left = 0;
                    if (float.IsNaN(m_right)) c_m_right = 0;
                    if (float.IsNaN(m_left) && float.IsNaN(m_right))
                    {
                        c_m_left = free_side / 2;
                        c_m_right = free_side / 2;
                    }
                    else if (float.IsNaN(m_left))
                    {
                        c_m_left = free_side;
                    }
                    else if (float.IsNaN(m_right))
                    {
                        c_m_right = free_side;
                    }
                    child_pos_x = c_m_left;
                    child.ComputeLayout(ComputedContentX + child_pos_x, ComputedContentY + child_pos_y, childW, childH, viewportWidth, viewportHeight, textRenderer, fs);
                    currentY = child_pos_y + child.ComputedHeight;
                    last_bottom = c_m_bottom;
                }
            }
            if (currentLine.Count > 0)
            {
                AlignCurrentLine(currentLine, textAlign, ComputedContentWidth);
            }
            if (currentX > 0)
            {
                currentY += maxLineH;
            }
            foreach (var child in positionedChildren)
            {
                child.ComputeLayout(ComputedContentX, ComputedContentY, ComputedContentWidth, ComputedContentHeight, viewportWidth, viewportHeight, textRenderer, fs);
            }
        }
        private void AlignCurrentLine(List<HtmlElement> line, string align, float containerW)
        {
            if (line.Count == 0) return;
            float lineW = 0;
            foreach (var child in line)
            {
                lineW += child.ComputedWidth + child.Style.Margin.W + child.Style.Margin.Y;
            }
            float offset = 0;
            if (align == "center")
            {
                offset = (containerW - lineW) / 2;
            }
            else if (align == "right")
            {
                offset = containerW - lineW;
            }
            if (offset > 0)
            {
                foreach (var child in line)
                {
                    child.ComputedPosition = new Vector2(child.ComputedPosition.X + offset, child.ComputedPosition.Y);
                    child.ComputedBackgroundX += offset;
                    child.ComputedContentX += offset;
                }
            }
        }
        public virtual Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            if (Style.Display == "none") return new Vector2(0, 0);
            float iw = 0;
            float ih = 0;
            Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            List<HtmlElement> visibleChildren = Children.Where(c => c.GetEffectiveDisplay() != "none").ToList();
            List<HtmlElement> normalChildren = visibleChildren.Where(c => c.Style.Position != "absolute" && c.Style.Position != "fixed").ToList();
            if (normalChildren.Count == 0 && visibleChildren.Count > 0)
            {
            }
            else if (visibleChildren.Count == 0)
            {
                if (this is TextElement text)
                {
                    var size = textRenderer.GetTextSize(text.Content, fs, Style.FontFamily ?? "Arial");
                    iw = size.X;
                    ih = size.Y;
                }
            }
            else
            {
                if (Style.Display == "flex")
                {
                    bool isRow = string.IsNullOrEmpty(Style.FlexDirection) || Style.FlexDirection == "row";
                    float gap = ParseSize(Style.GapStr, 0, viewportWidth, viewportHeight);
                    if (float.IsNaN(gap)) gap = 0;
                    int count = normalChildren.Count;
                    float totalGap = gap * (count - 1);
                    float sum_main = 0;
                    float max_cross = 0;
                    for (int i = 0; i < count; i++)
                    {
                        HtmlElement child = normalChildren[i];
                        Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                        Vector4 parsedMargin = ParseMargins(child.Style, 0, viewportWidth, viewportHeight);
                        float m_start = isRow ? parsedMargin.W : parsedMargin.X;
                        float m_end = isRow ? parsedMargin.Y : parsedMargin.Z;
                        float m_cross_start = isRow ? parsedMargin.X : parsedMargin.W;
                        float m_cross_end = isRow ? parsedMargin.Z : parsedMargin.Y;
                        m_start = float.IsNaN(m_start) ? 0 : m_start;
                        m_end = float.IsNaN(m_end) ? 0 : m_end;
                        m_cross_start = float.IsNaN(m_cross_start) ? 0 : m_cross_start;
                        m_cross_end = float.IsNaN(m_cross_end) ? 0 : m_cross_end;
                        float child_main = isRow ? childSize.X : childSize.Y;
                        float child_cross = isRow ? childSize.Y : childSize.X;
                        sum_main += child_main + m_start + m_end;
                        max_cross = Math.Max(max_cross, child_cross + m_cross_start + m_cross_end);
                    }
                    iw = isRow ? sum_main + totalGap : max_cross;
                    ih = isRow ? max_cross : sum_main + totalGap;
                }
                else
                {
                    float maxW = 0;
                    float currentH = 0;
                    float last_bottom = 0;
                    int count = normalChildren.Count;
                    bool inLine = false;
                    float lineW = 0;
                    float lineH = 0;
                    for (int i = 0; i < count; i++)
                    {
                        HtmlElement child = normalChildren[i];
                        string childDisplay = child.GetEffectiveDisplay();
                        bool isInline = childDisplay.StartsWith("inline");
                        Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                        Vector4 parsedMargin = ParseMargins(child.Style, 0, viewportWidth, viewportHeight);
                        float m_top = parsedMargin.X;
                        float m_bottom = parsedMargin.Z;
                        float m_left = parsedMargin.W;
                        float m_right = parsedMargin.Y;
                        m_top = float.IsNaN(m_top) ? 0 : m_top;
                        m_bottom = float.IsNaN(m_bottom) ? 0 : m_bottom;
                        m_left = float.IsNaN(m_left) ? 0 : m_left;
                        m_right = float.IsNaN(m_right) ? 0 : m_right;
                        if (isInline)
                        {
                            float childMinW = childSize.X + m_left + m_right;
                            float childHWithM = childSize.Y + m_top + m_bottom;
                            lineW += childMinW;
                            lineH = Math.Max(lineH, childHWithM);
                            inLine = true;
                        }
                        else
                        {
                            if (inLine)
                            {
                                currentH += lineH;
                                maxW = Math.Max(maxW, lineW);
                                lineW = 0;
                                lineH = 0;
                                inLine = false;
                            }
                            float eff = Math.Max(last_bottom, m_top);
                            currentH += eff + childSize.Y;
                            last_bottom = m_bottom;
                            float childMinW = childSize.X + m_left + m_right;
                            float childSpecW = float.NaN;
                            if (!string.IsNullOrEmpty(child.Style.WidthStr) && !child.Style.WidthStr.Trim().EndsWith("%"))
                            {
                                childSpecW = child.ParseSize(child.Style.WidthStr, 0, viewportWidth, viewportHeight);
                            }
                            if (!float.IsNaN(childSpecW))
                            {
                                string childBox = child.Style.BoxSizing;
                                Vector4 childPad = child.ParsePaddings(child.Style, 0, viewportWidth, viewportHeight);
                                Vector4 childBorder = child.ParseBorderWidths(child.Style, 0, viewportWidth, viewportHeight);
                                float specboxW;
                                if (childBox == "border-box")
                                {
                                    specboxW = childSpecW;
                                }
                                else
                                {
                                    specboxW = childSpecW + childPad.W + childPad.Y + childBorder.W + childBorder.Y;
                                }
                                childMinW = Math.Max(childSize.X + m_left + m_right, specboxW + m_left + m_right);
                            }
                            maxW = Math.Max(maxW, childMinW);
                        }
                    }
                    if (inLine)
                    {
                        currentH += lineH;
                        maxW = Math.Max(maxW, lineW);
                    }
                    currentH += last_bottom;
                    iw = maxW;
                    ih = currentH;
                }
            }
            iw += pad.W + pad.Y + borderW.W + borderW.Y;
            ih += pad.X + pad.Z + borderW.X + borderW.Z;
            float minBoxW = iw;
            float minBoxH = ih;
            float specifiedBoxW = float.NaN;
            float specifiedBoxH = float.NaN;
            string boxSizing = Style.BoxSizing;
            if (!string.IsNullOrEmpty(Style.WidthStr) && !Style.WidthStr.Trim().EndsWith("%"))
            {
                float spec = ParseSize(Style.WidthStr, 0, viewportWidth, viewportHeight);
                if (!float.IsNaN(spec))
                {
                    if (boxSizing == "border-box")
                    {
                        specifiedBoxW = spec;
                    }
                    else
                    {
                        specifiedBoxW = spec + pad.W + pad.Y + borderW.W + borderW.Y;
                    }
                }
            }
            if (!string.IsNullOrEmpty(Style.HeightStr) && !Style.HeightStr.Trim().EndsWith("%"))
            {
                float spec = ParseSize(Style.HeightStr, 0, viewportWidth, viewportHeight);
                if (!float.IsNaN(spec))
                {
                    if (boxSizing == "border-box")
                    {
                        specifiedBoxH = spec;
                    }
                    else
                    {
                        specifiedBoxH = spec + pad.X + pad.Z + borderW.X + borderW.Z;
                    }
                }
            }
            if (!float.IsNaN(specifiedBoxW))
            {
                iw = Math.Max(minBoxW, specifiedBoxW);
            }
            if (!float.IsNaN(specifiedBoxH))
            {
                ih = Math.Max(minBoxH, specifiedBoxH);
            }
            if (float.IsNaN(iw)) iw = 0;
            if (float.IsNaN(ih)) ih = 0;
            return new Vector2(iw, ih);
        }
        public virtual void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            CssStyle effectiveStyle = Style;
            if (Checked && PseudoStyles.TryGetValue("checked", out CssStyle checkedStyle))
            {
                effectiveStyle = checkedStyle;
            }
            if (IsFocused && PseudoStyles.TryGetValue("focus", out CssStyle focusStyle))
            {
                effectiveStyle = focusStyle;
            }
            if (IsHover && PseudoStyles.TryGetValue("hover", out CssStyle hover))
            {
                effectiveStyle = hover;
            }
            if (IsActive && PseudoStyles.TryGetValue("active", out CssStyle active))
            {
                effectiveStyle = active;
            }
            if (IsTarget && PseudoStyles.TryGetValue("target", out CssStyle targetStyle))
            {
                effectiveStyle = targetStyle;
            }
            if (effectiveStyle.Display == "none") return;
            Matrix4x4 localMatrix = parentMatrix * ComputedTransform;
            Vector4 borderTopC = effectiveStyle.BorderTopColor != Vector4.Zero ? effectiveStyle.BorderTopColor : effectiveStyle.BorderColor;
            Vector4 borderRightC = effectiveStyle.BorderRightColor != Vector4.Zero ? effectiveStyle.BorderRightColor : effectiveStyle.BorderColor;
            Vector4 borderBottomC = effectiveStyle.BorderBottomColor != Vector4.Zero ? effectiveStyle.BorderBottomColor : effectiveStyle.BorderColor;
            Vector4 borderLeftC = effectiveStyle.BorderLeftColor != Vector4.Zero ? effectiveStyle.BorderLeftColor : effectiveStyle.BorderColor;
            string borderTopS = string.IsNullOrEmpty(effectiveStyle.BorderTopStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderTopStyle;
            string borderRightS = string.IsNullOrEmpty(effectiveStyle.BorderRightStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderRightStyle;
            string borderBottomS = string.IsNullOrEmpty(effectiveStyle.BorderBottomStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderBottomStyle;
            string borderLeftS = string.IsNullOrEmpty(effectiveStyle.BorderLeftStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderLeftStyle;
            Vector4 borderW = this.BorderWidth;
            bool uniformBorder = borderW.X == borderW.Y && borderW.Y == borderW.Z && borderW.Z == borderW.W;
            bool uniformColor = borderTopC == borderRightC && borderRightC == borderBottomC && borderBottomC == borderLeftC;
            bool uniformStyle = borderTopS == borderRightS && borderRightS == borderBottomS && borderBottomS == borderLeftS && borderTopS != "none";
            bool hasUniformBorder = uniformBorder && uniformColor && uniformStyle && borderW.X > 0;
            Vector4 br = ParseSides(effectiveStyle.BorderRadiusStr, ComputedBackgroundWidth, viewportWidth, viewportHeight);
            float minRad = Math.Min(ComputedBackgroundWidth / 2, ComputedBackgroundHeight / 2);
            br.X = Math.Min(br.X, minRad);
            br.Y = Math.Min(br.Y, minRad);
            br.Z = Math.Min(br.Z, minRad);
            br.W = Math.Min(br.W, minRad);
            bool hasBg = effectiveStyle.BackgroundColor != Vector4.Zero || _bgRenderer != null;
            if (hasBg)
            {
                float[] bgNdc = GetNdcQuad(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, ComputedBackgroundHeight, localMatrix, viewportWidth, viewportHeight);
                float bw = 0f;
                Vector4 borderC = Vector4.Zero;
                if (br != Vector4.Zero && hasUniformBorder)
                {
                    bw = borderW.X;
                    borderC = borderTopC;
                }
                quadRenderer.DrawNdcQuad(bgNdc, effectiveStyle.BackgroundColor, br, new Vector2(ComputedBackgroundWidth, ComputedBackgroundHeight), bw, borderC);
            }
            if (_bgRenderer != null)
            {
                renderContext.Enable(renderContext.Enums.ScissorTest);
                int scissorY = (int)(viewportHeight - (ComputedBackgroundY + ComputedBackgroundHeight));
                renderContext.Scissor((int)ComputedBackgroundX, scissorY, (uint)ComputedBackgroundWidth, (uint)ComputedBackgroundHeight);
                _bgRenderer.Render(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, ComputedBackgroundHeight, viewportWidth, viewportHeight);
                renderContext.Disable(renderContext.Enums.ScissorTest);
            }
            bool drawSideBorders = br == Vector4.Zero || !hasUniformBorder;
            if (drawSideBorders)
            {
                if (borderTopS != "none" && borderTopC != Vector4.Zero && borderW.X > 0)
                {
                    float[] ndc = GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, borderW.X, localMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(ndc, borderTopC);
                }
                if (borderBottomS != "none" && borderBottomC != Vector4.Zero && borderW.Z > 0)
                {
                    float[] ndc = GetNdcQuad(ComputedPosition.X, ComputedPosition.Y + ComputedHeight - borderW.Z, ComputedWidth, borderW.Z, localMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(ndc, borderBottomC);
                }
                if (borderLeftS != "none" && borderLeftC != Vector4.Zero && borderW.W > 0)
                {
                    float[] ndc = GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, borderW.W, ComputedHeight, localMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(ndc, borderLeftC);
                }
                if (borderRightS != "none" && borderRightC != Vector4.Zero && borderW.Y > 0)
                {
                    float[] ndc = GetNdcQuad(ComputedPosition.X + ComputedWidth - borderW.Y, ComputedPosition.Y, borderW.Y, ComputedHeight, localMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(ndc, borderRightC);
                }
            }
            if (Style.Overflow == "hidden")
            {
                renderContext.Enable(renderContext.Enums.ScissorTest);
                int scissorY = (int)(viewportHeight - (ComputedContentY + ComputedContentHeight));
                renderContext.Scissor((int)ComputedContentX, scissorY, (uint)ComputedContentWidth, (uint)ComputedContentHeight);
            }
            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "option" && this is SelectElement sel && sel.IsOpen)
                {
                    continue; // Skip rendering options if parent select is open
                }
                child.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, localMatrix);
            }
            if (Style.Overflow == "hidden")
            {
                renderContext.Disable(renderContext.Enums.ScissorTest);
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
            else
            {
                return float.NaN;
            }
        }
        protected Vector4 ParsePaddings(CssStyle style, float parent, float vw, float vh)
        {
            string allStr = style.PaddingStr;
            Vector4 values = string.IsNullOrEmpty(allStr) ? Vector4.Zero : ParseSides(allStr, parent, vw, vh);
            string topStr = style.PaddingTopStr;
            string rightStr = style.PaddingRightStr;
            string bottomStr = style.PaddingBottomStr;
            string leftStr = style.PaddingLeftStr;
            if (!string.IsNullOrEmpty(topStr)) values.X = ParseSize(topStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(rightStr)) values.Y = ParseSize(rightStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(bottomStr)) values.Z = ParseSize(bottomStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(leftStr)) values.W = ParseSize(leftStr, parent, vw, vh);
            if (float.IsNaN(values.X)) values.X = 0;
            if (float.IsNaN(values.Y)) values.Y = 0;
            if (float.IsNaN(values.Z)) values.Z = 0;
            if (float.IsNaN(values.W)) values.W = 0;
            return values;
        }
        protected Vector4 ParseMargins(CssStyle style, float parent, float vw, float vh)
        {
            string allStr = style.MarginStr;
            Vector4 values = string.IsNullOrEmpty(allStr) ? Vector4.Zero : ParseSides(allStr, parent, vw, vh);
            return values;
        }
        protected Vector4 ParseBorderWidths(CssStyle style, float parent, float vw, float vh)
        {
            Vector4 values = string.IsNullOrEmpty(style.BorderWidthStr) ? Vector4.Zero : ParseSides(style.BorderWidthStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(style.BorderTopWidthStr)) values.X = ParseSize(style.BorderTopWidthStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(style.BorderRightWidthStr)) values.Y = ParseSize(style.BorderRightWidthStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(style.BorderBottomWidthStr)) values.Z = ParseSize(style.BorderBottomWidthStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(style.BorderLeftWidthStr)) values.W = ParseSize(style.BorderLeftWidthStr, parent, vw, vh);
            if (float.IsNaN(values.X)) values.X = 0;
            if (float.IsNaN(values.Y)) values.Y = 0;
            if (float.IsNaN(values.Z)) values.Z = 0;
            if (float.IsNaN(values.W)) values.W = 0;
            return values;
        }
        private Vector4 ParseSides(string s, float parent, float vw, float vh)
        {
            if (string.IsNullOrEmpty(s)) return Vector4.Zero;
            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float GetVal(int index, float defaultVal)
            {
                if (index < parts.Length)
                {
                    float val = ParseSize(parts[index], parent, vw, vh);
                    return float.IsNaN(val) ? defaultVal : val;
                }
                return defaultVal;
            }
            float val1 = GetVal(0, 0);
            float val2 = GetVal(1, val1);
            float val3 = GetVal(2, val1);
            float val4 = GetVal(3, val2);
            return new Vector4(val1, val2, val3, val4);
        }
        public virtual bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none") return false;
            float[] ndc = GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, ComputedHeight, ComputedFullTransform, viewportWidth, viewportHeight);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                float nx = ndc[k * 2];
                float ny = ndc[k * 2 + 1];
                minX = Math.Min(minX, nx);
                maxX = Math.Max(maxX, nx);
                minY = Math.Min(minY, ny);
                maxY = Math.Max(maxY, ny);
            }
            float mx = 2 * mousePos.X / viewportWidth - 1;
            float my = 1 - 2 * mousePos.Y / viewportHeight;
            if (mx < minX || mx > maxX || my < minY || my > maxY) return false;
            for (int ci = Children.Count - 1; ci >= 0; ci--)
            {
                if (Children[ci].HandleClick(mousePos, viewportWidth, viewportHeight)) return true;
            }
            return true;
        }
        public HtmlElement FindElementById(string id)
        {
            if (Attributes.GetValueOrDefault("id", "") == id) return this;
            foreach (var child in Children)
            {
                var found = child.FindElementById(id);
                if (found != null) return found;
            }
            return null;
        }
        private Matrix4x4 ComputeTransform(float viewportWidth, float viewportHeight)
        {
            if (string.IsNullOrEmpty(Style.Transform) || Style.Transform == "none") return Matrix4x4.Identity;
            Matrix4x4 mat = Matrix4x4.Identity;
            var matches = Regex.Matches(Style.Transform, @"(\w+)\((.+?)\)");
            foreach (Match m in matches)
            {
                string func = m.Groups[1].Value.ToLower();
                string args = m.Groups[2].Value;
                var argParts = args.Split(',').Select(a => a.Trim()).ToArray();
                Matrix4x4 fmat = Matrix4x4.Identity;
                switch (func)
                {
                    case "translate":
                        {
                            float tx = ParseSize(argParts[0], ComputedWidth, viewportWidth, viewportHeight);
                            float ty = argParts.Length > 1 ? ParseSize(argParts[1], ComputedHeight, viewportWidth, viewportHeight) : 0;
                            fmat = Matrix4x4.CreateTranslation(tx, ty, 0);
                            break;
                        }
                    case "translatex":
                        {
                            float tx = ParseSize(argParts[0], ComputedWidth, viewportWidth, viewportHeight);
                            fmat = Matrix4x4.CreateTranslation(tx, 0, 0);
                            break;
                        }
                    case "rotate":
                        {
                            float angle = ParseAngle(argParts[0]);
                            fmat = Matrix4x4.CreateRotationZ(angle);
                            break;
                        }
                    case "scale":
                        {
                            float sx = float.Parse(argParts[0]);
                            float sy = argParts.Length > 1 ? float.Parse(argParts[1]) : sx;
                            fmat = Matrix4x4.CreateScale(sx, sy, 1);
                            break;
                        }
                    case "skew":
                        {
                            float ax = ParseAngle(argParts[0]);
                            float ay = argParts.Length > 1 ? ParseAngle(argParts[1]) : 0;
                            fmat = new Matrix4x4(
                                1, MathF.Tan(ay), 0, 0,
                                MathF.Tan(ax), 1, 0, 0,
                                0, 0, 1, 0,
                                0, 0, 0, 1);
                            break;
                        }
                    case "matrix":
                        {
                            float a = float.Parse(argParts[0]);
                            float b = float.Parse(argParts[1]);
                            float c = float.Parse(argParts[2]);
                            float d = float.Parse(argParts[3]);
                            float tx = float.Parse(argParts[4]);
                            float ty = float.Parse(argParts[5]);
                            fmat = new Matrix4x4(
                                a, b, 0, 0,
                                c, d, 0, 0,
                                0, 0, 1, 0,
                                tx, ty, 0, 1);
                            break;
                        }
                }
                mat = mat * fmat;
            }
            Vector3 origin = new Vector3(ComputedWidth / 2, ComputedHeight / 2, 0);
            Matrix4x4 toOrigin = Matrix4x4.CreateTranslation(-origin);
            Matrix4x4 fromOrigin = Matrix4x4.CreateTranslation(origin);
            mat = fromOrigin * mat * toOrigin;
            return mat;
        }
        private float ParseAngle(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            float val;
            if (s.EndsWith("deg"))
            {
                val = float.Parse(s.Replace("deg", ""));
                return val * MathF.PI / 180;
            }
            else if (s.EndsWith("rad"))
            {
                val = float.Parse(s.Replace("rad", ""));
                return val;
            }
            else if (s.EndsWith("turn"))
            {
                val = float.Parse(s.Replace("turn", ""));
                return val * MathF.PI * 2;
            }
            else if (s.EndsWith("grad"))
            {
                val = float.Parse(s.Replace("grad", ""));
                return val * MathF.PI / 200;
            }
            val = float.Parse(s);
            return val * MathF.PI / 180; // default deg
        }
        public static float[] GetNdcQuad(float x, float y, float w, float h, Matrix4x4 trans, float vw, float vh)
        {
            Vector4 bl = Vector4.Transform(new Vector4(x, y + h, 0, 1), trans);
            Vector4 br = Vector4.Transform(new Vector4(x + w, y + h, 0, 1), trans);
            Vector4 tr = Vector4.Transform(new Vector4(x + w, y, 0, 1), trans);
            Vector4 tl = Vector4.Transform(new Vector4(x, y, 0, 1), trans);
            bl /= bl.W;
            br /= br.W;
            tr /= tr.W;
            tl /= tl.W;
            float blx = 2 * bl.X / vw - 1;
            float bly = 1 - 2 * bl.Y / vh;
            float brx = 2 * br.X / vw - 1;
            float bry = 1 - 2 * br.Y / vh;
            float trx = 2 * tr.X / vw - 1;
            float try_ = 1 - 2 * tr.Y / vh;
            float tlx = 2 * tl.X / vw - 1;
            float tly = 1 - 2 * tl.Y / vh;
            return new float[] { blx, bly, brx, bry, trx, try_, tlx, tly };
        }
    }
}