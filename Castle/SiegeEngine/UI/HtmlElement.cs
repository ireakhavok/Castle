// Folder: SiegeEngine.UI
// File: HtmlElement.cs
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
        public Vector4 BorderWidth { get; set; }
        public bool IsHover { get; set; }
        public bool IsActive { get; set; }
        public bool Checked { get; set; }
        public bool IsTarget { get; set; }

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
            if (float.IsNaN(left)) left = 0;
            float top = ParseSize(effectiveStyle.TopStr, refHeight, viewportWidth, viewportHeight);
            if (float.IsNaN(top)) top = 0;

            if (effectiveStyle.Position == "static")
            {
                left = 0;
                top = 0;
            }

            float boxX = baseX + left;
            float boxY = baseY + top;

            float w = ParseSize(effectiveStyle.WidthStr, refWidth, viewportWidth, viewportHeight);
            float h = ParseSize(effectiveStyle.HeightStr, refHeight, viewportWidth, viewportHeight);

            if ((string.IsNullOrEmpty(effectiveStyle.Display) || effectiveStyle.Display == "block" || effectiveStyle.Display == "flex") && float.IsNaN(w))
            {
                w = refWidth;
            }

            float minW = ParseSize(effectiveStyle.MinWidthStr, refWidth, viewportWidth, viewportHeight);
            float minH = ParseSize(effectiveStyle.MinHeightStr, refHeight, viewportWidth, viewportHeight);
            float maxW = ParseSize(effectiveStyle.MaxWidthStr, refWidth, viewportWidth, viewportHeight);
            float maxH = ParseSize(effectiveStyle.MaxHeightStr, refHeight, viewportWidth, viewportHeight);

            Vector4 pad = ParsePaddings(effectiveStyle, refWidth, viewportWidth, viewportHeight);
            Vector4 margin = ParsePaddings(effectiveStyle, refWidth, viewportWidth, viewportHeight, isMargin: true);
            Vector4 borderW = ParseBorderWidths(effectiveStyle, refWidth, viewportWidth, viewportHeight);

            if (Parent == null)
            {
                if (float.IsNaN(w)) w = viewportWidth;
                if (float.IsNaN(h)) h = viewportHeight;
            }

            float boxW, boxH, contentW, contentH;

            Vector2 intrinsic = new Vector2(0, 0);
            if (float.IsNaN(w) || float.IsNaN(h))
            {
                intrinsic = ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
            }

            if (float.IsNaN(w))
            {
                if (effectiveStyle.BoxSizing == "border-box")
                {
                    w = intrinsic.X;
                }
                else
                {
                    w = intrinsic.X - pad.W - pad.Y - borderW.W - borderW.Y;
                }
            }

            if (float.IsNaN(h))
            {
                if (effectiveStyle.BoxSizing == "border-box")
                {
                    h = intrinsic.Y;
                }
                else
                {
                    h = intrinsic.Y - pad.X - pad.Z - borderW.X - borderW.Z;
                }
            }

            if (!float.IsNaN(forcedWidth)) w = forcedWidth;
            if (!float.IsNaN(forcedHeight)) h = forcedHeight;

            if (!float.IsNaN(minW)) w = Math.Max(w, minW);
            if (!float.IsNaN(minH)) h = Math.Max(h, minH);
            if (!float.IsNaN(maxW)) w = Math.Min(w, maxW);
            if (!float.IsNaN(maxH)) h = Math.Min(h, maxH);

            if (float.IsNaN(w)) w = 0;
            if (float.IsNaN(h)) h = 0;

            if (effectiveStyle.BoxSizing == "border-box")
            {
                boxW = w;
                boxH = h;
                contentW = w - pad.W - pad.Y - borderW.W - borderW.Y;
                contentH = h - pad.X - pad.Z - borderW.X - borderW.Z;
            }
            else
            {
                contentW = w;
                contentH = h;
                boxW = w + pad.W + pad.Y + borderW.W + borderW.Y;
                boxH = h + pad.X + pad.Z + borderW.X + borderW.Z;
            }

            if (float.IsNaN(boxW)) boxW = 0;
            if (float.IsNaN(boxH)) boxH = 0;
            if (float.IsNaN(contentW)) contentW = 0;
            if (float.IsNaN(contentH)) contentH = 0;

            ComputedWidth = boxW;
            ComputedHeight = boxH;
            ComputedContentWidth = contentW;
            ComputedContentHeight = contentH;

            if (effectiveStyle.Position != "absolute" && effectiveStyle.Position != "fixed")
            {
                boxX -= margin.W;
                boxY -= margin.X;
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
        }

        private void LayoutFlexChildren(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            List<HtmlElement> visibleChildren = Children.Where(c => c.Style.Display != "none").ToList();
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
                float start = isRow ? child.Style.Margin.W : child.Style.Margin.X;
                float end = isRow ? child.Style.Margin.Y : child.Style.Margin.Z;
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
                float start = isRow ? child.Style.Margin.W : child.Style.Margin.X;
                float end = isRow ? child.Style.Margin.Y : child.Style.Margin.Z;
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
            float spacing = gap;

            if (has_auto_main)
            {
                start_main = 0;
            }
            else
            {
                if (Style.JustifyContent == "space-between")
                {
                    if (normalChildren.Count > 1)
                    {
                        spacing = (availableMain - sum_outer) / (normalChildren.Count - 1);
                    }
                    else spacing = 0;
                }
                else if (Style.JustifyContent == "space-around")
                {
                    spacing = (availableMain - sum_outer) / normalChildren.Count;
                    start_main = spacing / 2;
                }
                else if (Style.JustifyContent == "space-evenly")
                {
                    spacing = (availableMain - sum_outer) / (normalChildren.Count + 1);
                    start_main = spacing;
                }
                else if (Style.JustifyContent == "center")
                {
                    start_main = (availableMain - total_used) / 2;
                }
                else if (Style.JustifyContent == "flex-end")
                {
                    start_main = availableMain - total_used;
                }
                else
                {
                    start_main = 0;
                }
            }

            if (float.IsNaN(start_main)) start_main = 0;
            if (float.IsNaN(spacing)) spacing = 0;

            float current_main = start_main;
            string alignItems = string.IsNullOrEmpty(Style.AlignItems) ? "stretch" : Style.AlignItems;

            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                if (i > 0) current_main += spacing;

                float item_start = current_main + childMarginStart[i];
                float child_main = childBaseMain[i];

                string cross_str_raw = isRow ? child.Style.HeightStr : child.Style.WidthStr;
                float child_cross_str = ParseSize(cross_str_raw, availableCross, viewportWidth, viewportHeight);

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

                float m_cross_start = isRow ? child.Style.Margin.X : child.Style.Margin.W;
                float m_cross_end = isRow ? child.Style.Margin.Z : child.Style.Margin.Y;
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

                current_main += child_main + childMarginEnd[i];

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
            float currentY = 0;
            float last_bottom = 0;

            List<HtmlElement> visibleChildren = Children.Where(c => c.Style.Display != "none").ToList();
            List<HtmlElement> normalChildren = visibleChildren.Where(c => c.Style.Position != "absolute" && c.Style.Position != "fixed").ToList();
            List<HtmlElement> positionedChildren = visibleChildren.Where(c => c.Style.Position == "absolute" || c.Style.Position == "fixed").ToList();

            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];

                float childW = ParseSize(child.Style.WidthStr, ComputedContentWidth, viewportWidth, viewportHeight);

                Vector4 child_pad = ParsePaddings(child.Style, ComputedContentWidth, viewportWidth, viewportHeight);
                Vector4 child_border = ParseBorderWidths(child.Style, ComputedContentWidth, viewportWidth, viewportHeight);

                float child_outer_w;
                if (float.IsNaN(childW))
                {
                    child_outer_w = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X;
                }
                else
                {
                    if (child.Style.BoxSizing == "border-box")
                    {
                        child_outer_w = childW;
                    }
                    else
                    {
                        child_outer_w = childW + child_pad.W + child_pad.Y + child_border.W + child_border.Y;
                    }
                }

                float childH = ParseSize(child.Style.HeightStr, ComputedContentHeight, viewportWidth, viewportHeight);
                if (float.IsNaN(childH)) childH = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y;

                float m_top = child.Style.Margin.X;
                float m_bottom = child.Style.Margin.Z;
                float m_left = child.Style.Margin.W;
                float m_right = child.Style.Margin.Y;

                float c_m_top = float.IsNaN(m_top) ? 0 : m_top;
                float c_m_bottom = float.IsNaN(m_bottom) ? 0 : m_bottom;
                float c_m_left = float.IsNaN(m_left) ? 0 : m_left;
                float c_m_right = float.IsNaN(m_right) ? 0 : m_right;

                float eff = Math.Max(last_bottom, c_m_top);
                float child_pos_y = currentY + eff;

                float child_pos_x = 0;
                float free_side = ComputedContentWidth - child_outer_w - c_m_left - c_m_right;

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

            foreach (var child in positionedChildren)
            {
                child.ComputeLayout(ComputedContentX, ComputedContentY, ComputedContentWidth, ComputedContentHeight, viewportWidth, viewportHeight, textRenderer, fs);
            }
        }

        protected virtual Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            if (Style.Display == "none") return new Vector2(0, 0);

            float iw = 0;
            float ih = 0;

            Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);

            List<HtmlElement> visibleChildren = Children.Where(c => c.Style.Display != "none").ToList();
            List<HtmlElement> normalChildren = visibleChildren.Where(c => c.Style.Position != "absolute" && c.Style.Position != "fixed").ToList();

            if (normalChildren.Count == 0 && visibleChildren.Count > 0)
            {
                // If all children are positioned, intrinsic size is 0 + pads + borders
                iw += pad.W + pad.Y + borderW.W + borderW.Y;
                ih += pad.X + pad.Z + borderW.X + borderW.Z;
                return new Vector2(iw, ih);
            }
            else if (visibleChildren.Count == 0)
            {
                if (this is TextElement text)
                {
                    var size = textRenderer.GetTextSize(text.Content, fs);
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

                        float m_start = isRow ? child.Style.Margin.W : child.Style.Margin.X;
                        float m_end = isRow ? child.Style.Margin.Y : child.Style.Margin.Z;
                        float m_cross_start = isRow ? child.Style.Margin.X : child.Style.Margin.W;
                        float m_cross_end = isRow ? child.Style.Margin.Z : child.Style.Margin.Y;

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

                    for (int i = 0; i < count; i++)
                    {
                        HtmlElement child = normalChildren[i];
                        Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);

                        float m_top = child.Style.Margin.X;
                        float m_bottom = child.Style.Margin.Z;
                        float m_left = child.Style.Margin.W;
                        float m_right = child.Style.Margin.Y;

                        m_top = float.IsNaN(m_top) ? 0 : m_top;
                        m_bottom = float.IsNaN(m_bottom) ? 0 : m_bottom;
                        m_left = float.IsNaN(m_left) ? 0 : m_left;
                        m_right = float.IsNaN(m_right) ? 0 : m_right;

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
                            float specBoxW;
                            if (childBox == "border-box")
                            {
                                specBoxW = childSpecW;
                            }
                            else
                            {
                                specBoxW = childSpecW + childPad.W + childPad.Y + childBorder.W + childBorder.Y;
                            }
                            childMinW = Math.Max(childSize.X + m_left + m_right, specBoxW + m_left + m_right);
                        }

                        maxW = Math.Max(maxW, childMinW);
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
            if (IsTarget && PseudoStyles.TryGetValue("target", out CssStyle targetStyle))
            {
                effectiveStyle = targetStyle;
            }

            if (effectiveStyle.BackgroundColor != Vector4.Zero)
            {
                quadRenderer.DrawQuad(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, ComputedBackgroundHeight, effectiveStyle.BackgroundColor, viewportWidth, viewportHeight);
            }

            Vector4 borderTopC = effectiveStyle.BorderTopColor != Vector4.Zero ? effectiveStyle.BorderTopColor : effectiveStyle.BorderColor;
            Vector4 borderRightC = effectiveStyle.BorderRightColor != Vector4.Zero ? effectiveStyle.BorderRightColor : effectiveStyle.BorderColor;
            Vector4 borderBottomC = effectiveStyle.BorderBottomColor != Vector4.Zero ? effectiveStyle.BorderBottomColor : effectiveStyle.BorderColor;
            Vector4 borderLeftC = effectiveStyle.BorderLeftColor != Vector4.Zero ? effectiveStyle.BorderLeftColor : effectiveStyle.BorderColor;

            string borderTopS = string.IsNullOrEmpty(effectiveStyle.BorderTopStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderTopStyle;
            string borderRightS = string.IsNullOrEmpty(effectiveStyle.BorderRightStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderRightStyle;
            string borderBottomS = string.IsNullOrEmpty(effectiveStyle.BorderBottomStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderBottomStyle;
            string borderLeftS = string.IsNullOrEmpty(effectiveStyle.BorderLeftStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderLeftStyle;

            Vector4 borderW = this.BorderWidth;

            if (borderTopS != "none" && borderTopC != Vector4.Zero && borderW.X > 0)
            {
                quadRenderer.DrawQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, borderW.X, borderTopC, viewportWidth, viewportHeight);
            }
            if (borderBottomS != "none" && borderBottomC != Vector4.Zero && borderW.Z > 0)
            {
                quadRenderer.DrawQuad(ComputedPosition.X, ComputedPosition.Y + ComputedHeight - borderW.Z, ComputedWidth, borderW.Z, borderBottomC, viewportWidth, viewportHeight);
            }
            if (borderLeftS != "none" && borderLeftC != Vector4.Zero && borderW.W > 0)
            {
                quadRenderer.DrawQuad(ComputedPosition.X, ComputedPosition.Y, borderW.W, ComputedHeight, borderLeftC, viewportWidth, viewportHeight);
            }
            if (borderRightS != "none" && borderRightC != Vector4.Zero && borderW.Y > 0)
            {
                quadRenderer.DrawQuad(ComputedPosition.X + ComputedWidth - borderW.Y, ComputedPosition.Y, borderW.Y, ComputedHeight, borderRightC, viewportWidth, viewportHeight);
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

        protected Vector4 ParsePaddings(CssStyle style, float parent, float vw, float vh, bool isMargin = false)
        {
            string allStr = isMargin ? style.MarginStr : style.PaddingStr;
            Vector4 values = string.IsNullOrEmpty(allStr) ? Vector4.Zero : ParseSides(allStr, parent, vw, vh);

            string topStr = isMargin ? null : style.PaddingTopStr; // margin no individual in this
            string rightStr = isMargin ? null : style.PaddingRightStr;
            string bottomStr = isMargin ? null : style.PaddingBottomStr;
            string leftStr = isMargin ? null : style.PaddingLeftStr;

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