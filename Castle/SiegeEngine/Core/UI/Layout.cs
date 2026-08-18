// Folder: SiegeEngine.Core.UI
// File: layout.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Linq;
using System.Text.RegularExpressions;
using SiegeEngine.Core.GPU.Renderers;
namespace SiegeEngine.Core.UI
{
    public partial class HtmlElement
    {
        public virtual float GetFullContentExtentForParent()
        {
            return (_needsVerticalScrollbar && _contentFullHeight > 0f) ? _contentFullHeight : ComputedHeight;
        }
        public virtual void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            string id = Attributes.GetValueOrDefault("id", "no-id");
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
            float fs = HtmlLayoutUtils.ParseSize(effectiveStyle.FontSizeStr, parentFs, viewportWidth, viewportHeight);
            if (float.IsNaN(fs)) fs = parentFs;
            Style.FontSize = fs;
            float left = HtmlLayoutUtils.ParseSize(effectiveStyle.LeftStr, refWidth, viewportWidth, viewportHeight);
            float top = HtmlLayoutUtils.ParseSize(effectiveStyle.TopStr, refHeight, viewportWidth, viewportHeight);
            float right = HtmlLayoutUtils.ParseSize(effectiveStyle.RightStr, refWidth, viewportWidth, viewportHeight);
            float bottom = HtmlLayoutUtils.ParseSize(effectiveStyle.BottomStr, refHeight, viewportWidth, viewportHeight);
            float w = HtmlLayoutUtils.ParseSize(effectiveStyle.WidthStr, refWidth, viewportWidth, viewportHeight);
            float h = HtmlLayoutUtils.ParseSize(effectiveStyle.HeightStr, refHeight, viewportWidth, viewportHeight);
            string overflow = effectiveStyle.Overflow ?? "";
            string overflowY = effectiveStyle.OverflowY ?? "";
            bool hasVerticalOverflow = (overflow == "auto" || overflow == "scroll" || overflowY == "auto" || overflowY == "scroll");
            if (hasVerticalOverflow)
            {
                h = refHeight;
            }
            bool isBlockOrFlexOrGridOrTable = effectiveStyle.Display == "block" || effectiveStyle.Display == "flex" || effectiveStyle.Display == "grid" || effectiveStyle.Display == "table";
            bool isStaticOrRelative = string.IsNullOrEmpty(effectiveStyle.Position) || effectiveStyle.Position == "static" || effectiveStyle.Position == "relative";
            if (isBlockOrFlexOrGridOrTable && isStaticOrRelative && float.IsNaN(w))
            {
                w = refWidth;
            }
            float minW = HtmlLayoutUtils.ParseSize(effectiveStyle.MinWidthStr, refWidth, viewportWidth, viewportHeight);
            float minH = HtmlLayoutUtils.ParseSize(effectiveStyle.MinHeightStr, refHeight, viewportWidth, viewportHeight);
            float maxW = HtmlLayoutUtils.ParseSize(effectiveStyle.MaxWidthStr, refWidth, viewportWidth, viewportHeight);
            float maxH = HtmlLayoutUtils.ParseSize(effectiveStyle.MaxHeightStr, refHeight, viewportWidth, viewportHeight);
            Vector4 pad = HtmlLayoutUtils.ParsePaddings(effectiveStyle, refWidth, viewportWidth, viewportHeight);
            Vector4 margin = HtmlLayoutUtils.ParseMargins(effectiveStyle, refWidth, viewportWidth, viewportHeight);
            Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(effectiveStyle, refWidth, viewportWidth, viewportHeight);
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
            string boxSizing = effectiveStyle.BoxSizing;
            float boxW, boxH, contentW, contentH;
            if (boxSizing == "border-box")
            {
                boxW = w;
                boxH = h;
                contentW = boxW - pad.W - pad.Y - borderW.W - borderW.Y;
                contentH = boxH - pad.X - pad.Z - borderW.X - borderW.Z;
            }
            else
            {
                contentW = w;
                contentH = h;
                boxW = contentW + pad.W + pad.Y + borderW.W + borderW.Y;
                boxH = contentH + pad.X + pad.Z + borderW.X + borderW.Z;
            }
            if (!float.IsNaN(minW)) boxW = Math.Max(boxW, minW);
            if (!float.IsNaN(minH)) boxH = Math.Max(boxH, minH);
            if (!float.IsNaN(maxW)) boxW = Math.Min(boxW, maxW);
            if (!float.IsNaN(maxH)) boxH = Math.Min(boxH, maxH);
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
            BorderWidth = borderW;
            Style.Margin = margin;
            if (Children.Count > 0)
            {
                if (effectiveStyle.Display == "flex")
                {
                    LayoutFlexChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
                else if (effectiveStyle.Display == "grid")
                {
                    LayoutGridChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
                else
                {
                    LayoutBlockChildren(viewportWidth, viewportHeight, textRenderer, fs);
                }
            }
            if (hasVerticalOverflow)
            {
                _contentFullHeight = 0f;
                foreach (var child in Children)
                {
                    if (child.GetEffectiveDisplay() != "none")
                    {
                        float childBottom = child.ComputedPosition.Y + child.ComputedHeight - ComputedContentY;
                        _contentFullHeight = Math.Max(_contentFullHeight, childBottom);
                    }
                }
            }
            else
            {
                _contentFullHeight = 0f;
                foreach (var child in Children)
                {
                    if (child.GetEffectiveDisplay() != "none")
                    {
                        float childBottom = child.ComputedPosition.Y + child.GetFullContentExtentForParent() - ComputedContentY;
                        _contentFullHeight = Math.Max(_contentFullHeight, childBottom);
                    }
                }
            }
            _needsVerticalScrollbar = hasVerticalOverflow && _contentFullHeight > ComputedContentHeight + 0.1f;
            if (_needsVerticalScrollbar)
            {
                ScrollOffsetY = Math.Clamp(ScrollOffsetY, 0, _contentFullHeight - ComputedContentHeight);
            }
            else
            {
                ScrollOffsetY = 0f;
            }
            ComputedTransform = HtmlLayoutUtils.ComputeTransform(this, viewportWidth, viewportHeight);
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
            float gap = HtmlLayoutUtils.ParseSize(Style.GapStr, availableMain, viewportWidth, viewportHeight);
            if (float.IsNaN(gap)) gap = 0;
            List<float> childBaseMain = new List<float>();
            List<float> childGrow = new List<float>();
            List<float> childShrink = new List<float>();
            float totalGrow = 0;
            float totalShrink = 0;
            float totalBaseMain = 0;
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                float grow = child.Style.FlexGrow;
                if (grow == 0 && !string.IsNullOrEmpty(child.Style.Flex))
                {
                    var flexParts = child.Style.Flex.Split(' ');
                    if (flexParts.Length > 0) float.TryParse(flexParts[0], out grow);
                }
                childGrow.Add(grow);
                childShrink.Add(1f);
                totalGrow += grow;
                string main_str_raw = isRow ? child.Style.WidthStr : child.Style.HeightStr;
                float mainStr = HtmlLayoutUtils.ParseSize(main_str_raw, availableMain, viewportWidth, viewportHeight);
                float min_main = HtmlLayoutUtils.ParseSize(isRow ? child.Style.MinWidthStr : child.Style.MinHeightStr, availableMain, viewportWidth, viewportHeight);
                float max_main = HtmlLayoutUtils.ParseSize(isRow ? child.Style.MaxWidthStr : child.Style.MaxHeightStr, availableMain, viewportWidth, viewportHeight);
                if (!float.IsNaN(mainStr))
                {
                    if (!float.IsNaN(min_main)) mainStr = Math.Max(mainStr, min_main);
                    if (!float.IsNaN(max_main)) mainStr = Math.Min(mainStr, max_main);
                }
                Vector4 pad = HtmlLayoutUtils.ParsePaddings(child.Style, 0, viewportWidth, viewportHeight);
                Vector4 border_w = HtmlLayoutUtils.ParseBorderWidths(child.Style, 0, viewportWidth, viewportHeight);
                float pad_start = isRow ? pad.W : pad.X;
                float pad_end = isRow ? pad.Y : pad.Z;
                float border_start = isRow ? border_w.W : border_w.X;
                float border_end = isRow ? border_w.Y : border_w.Z;
                float baseMain;
                if (float.IsNaN(mainStr))
                {
                    baseMain = (grow > 0) ? 0f : (isRow ? child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X : child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y);
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
                totalShrink += 1f * baseMain;
            }
            float totalGap = gap * (normalChildren.Count - 1);
            float free = availableMain - totalBaseMain - totalGap;
            if (free > 0)
            {
                if (totalGrow > 0)
                {
                    for (int i = 0; i < normalChildren.Count; i++)
                    {
                        float extra = free / totalGrow * childGrow[i];
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
                        float reduce = Math.Abs(free) / totalShrink * (childShrink[i] * childBaseMain[i]);
                        childBaseMain[i] = Math.Max(0, childBaseMain[i] - reduce);
                    }
                }
            }
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                float min_main = HtmlLayoutUtils.ParseSize(isRow ? child.Style.MinWidthStr : child.Style.MinHeightStr, availableMain, viewportWidth, viewportHeight);
                float max_main = HtmlLayoutUtils.ParseSize(isRow ? child.Style.MaxWidthStr : child.Style.MaxHeightStr, availableMain, viewportWidth, viewportHeight);
                if (!float.IsNaN(min_main)) childBaseMain[i] = Math.Max(childBaseMain[i], min_main);
                if (!float.IsNaN(max_main)) childBaseMain[i] = Math.Min(childBaseMain[i], max_main);
            }
            float sum_border_boxes = 0;
            float sum_fixed_margins = 0;
            int num_auto_main = 0;
            List<float> childMarginStart = new List<float>();
            List<float> childMarginEnd = new List<float>();
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                Vector4 parsedMargin = HtmlLayoutUtils.ParseMargins(child.Style, availableMain, viewportWidth, viewportHeight);
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
            float auto_size = num_auto_main > 0 && free > 0 ? free / num_auto_main : 0;
            bool has_auto_main = num_auto_main > 0;
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                Vector4 parsedMargin = HtmlLayoutUtils.ParseMargins(child.Style, availableMain, viewportWidth, viewportHeight);
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
            float extra_free = availableMain - sum_outer - total_gap;
            string justify = Style.JustifyContent ?? "flex-start";
            if (justify == "center")
            {
                start_main = extra_free / 2;
            }
            else if (justify == "flex-end")
            {
                start_main = extra_free;
            }
            else if (justify == "space-between" && normalChildren.Count > 1)
            {
                justify_spacing = extra_free / (normalChildren.Count - 1);
            }
            else if (justify == "space-around")
            {
                justify_spacing = extra_free / normalChildren.Count;
                start_main = justify_spacing / 2;
            }
            else
            {
                start_main = 0;
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
                float child_cross_str = HtmlLayoutUtils.ParseSize(cross_str, availableCross, viewportWidth, viewportHeight);
                Vector4 pad_child = HtmlLayoutUtils.ParsePaddings(child.Style, 0, viewportWidth, viewportHeight);
                Vector4 border_child = HtmlLayoutUtils.ParseBorderWidths(child.Style, 0, viewportWidth, viewportHeight);
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
                float max_cross = HtmlLayoutUtils.ParseSize(isRow ? child.Style.MaxHeightStr : child.Style.MaxWidthStr, availableCross, viewportWidth, viewportHeight);
                if (!float.IsNaN(max_cross)) child_cross = Math.Min(child_cross, max_cross);
                float min_cross = HtmlLayoutUtils.ParseSize(isRow ? child.Style.MinHeightStr : child.Style.MinWidthStr, availableCross, viewportWidth, viewportHeight);
                if (!float.IsNaN(min_cross)) child_cross = Math.Max(child_cross, min_cross);
                Vector4 parsedMarginCross = HtmlLayoutUtils.ParseMargins(child.Style, availableCross, viewportWidth, viewportHeight);
                float m_cross_start = isRow ? parsedMarginCross.X : parsedMarginCross.W;
                float m_cross_end = isRow ? parsedMarginCross.Z : parsedMarginCross.Y;
                float c_m_cross_start = float.IsNaN(m_cross_start) ? 0 : m_cross_start;
                float c_m_cross_end = float.IsNaN(m_cross_end) ? 0 : m_cross_end;
                int num_auto_cross = 0;
                if (float.IsNaN(m_cross_start)) num_auto_cross++;
                if (float.IsNaN(m_cross_end)) num_auto_cross++;
                float free_cross = availableCross - child_cross - c_m_cross_start - c_m_cross_end;
                float auto_cross_size = num_auto_cross > 0 && free_cross > 0 ? free_cross / num_auto_cross : 0;
                if (float.IsNaN(m_cross_start)) c_m_cross_start = auto_cross_size;
                if (float.IsNaN(m_cross_end)) c_m_cross_end = auto_cross_size;
                bool has_auto_cross = num_auto_cross > 0;
                float child_cross_for_align = child_cross + c_m_cross_start + c_m_cross_end;
                float offset = 0;
                if (has_auto_cross)
                {
                    offset = 0;
                }
                else
                {
                    if (alignItems == "center")
                    {
                        offset = (availableCross - child_cross_for_align) / 2;
                    }
                    else if (alignItems == "flex-end")
                    {
                        offset = availableCross - child_cross_for_align;
                    }
                    else if (alignItems == "stretch")
                    {
                        if (float.IsNaN(child_cross_str))
                        {
                            child_cross = availableCross - c_m_cross_start - c_m_cross_end;
                        }
                        offset = 0;
                    }
                    else
                    {
                        offset = 0;
                    }
                }
                if (float.IsNaN(offset)) offset = 0;
                float child_pos_cross = offset + c_m_cross_start;
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
                float childMainExtent = (!isRow && child._needsVerticalScrollbar && child._contentFullHeight > 0f) ? child._contentFullHeight : childBaseMain[i];
                current_main += childMainExtent + childMarginStart[i] + childMarginEnd[i];
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
        private void LayoutGridChildren(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            List<HtmlElement> visibleChildren = Children.Where(c => c.GetEffectiveDisplay() != "none").ToList();
            if (visibleChildren.Count == 0) return;
            string columnsStr = Style.GridTemplateColumnsStr;
            string rowsStr = Style.GridTemplateRowsStr;
            if (string.IsNullOrEmpty(columnsStr) && string.IsNullOrEmpty(rowsStr))
            {
                LayoutBlockChildren(viewportWidth, viewportHeight, textRenderer, fs);
                return;
            }
            string gapStr = Style.GapStr;
            string[] gapDefs = string.IsNullOrEmpty(gapStr) ? new string[0] : gapStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float rowGap = gapDefs.Length > 0 ? HtmlLayoutUtils.ParseSize(gapDefs[0], ComputedContentWidth, viewportWidth, viewportHeight) : 0f;
            float colGap = gapDefs.Length > 1 ? HtmlLayoutUtils.ParseSize(gapDefs[1], ComputedContentWidth, viewportWidth, viewportHeight) : rowGap;
            List<float> trackWidths = new List<float>();
            float totalNonGrowableCols = 0f;
            float totalFrCols = 0f;
            List<bool> isAutoCol = new List<bool>();
            List<float> frValuesCol = new List<float>();
            if (!string.IsNullOrEmpty(columnsStr))
            {
                Match repeatMatch = Regex.Match(columnsStr, @"\s*repeat\s*\(\s*(\d+)\s*,\s*(.*?)\s*\)");
                List<string> colDefsList = new List<string>();
                if (repeatMatch.Success)
                {
                    int repeatNum = int.Parse(repeatMatch.Groups[1].Value);
                    string repeatUnit = repeatMatch.Groups[2].Value;
                    for (int k = 0; k < repeatNum; k++)
                    {
                        colDefsList.Add(repeatUnit);
                    }
                }
                else
                {
                    colDefsList = columnsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                foreach (string def in colDefsList)
                {
                    if (def == "auto")
                    {
                        trackWidths.Add(0f);
                        isAutoCol.Add(true);
                        frValuesCol.Add(0f);
                    }
                    else if (def.EndsWith("fr"))
                    {
                        string frStr = def.Replace("fr", "").Trim();
                        float frValue = string.IsNullOrEmpty(frStr) ? 1f : float.Parse(frStr);
                        totalFrCols += frValue;
                        trackWidths.Add(0f);
                        isAutoCol.Add(false);
                        frValuesCol.Add(frValue);
                    }
                    else
                    {
                        float fixedW = HtmlLayoutUtils.ParseSize(def, ComputedContentWidth, viewportWidth, viewportHeight);
                        if (float.IsNaN(fixedW)) fixedW = 0f;
                        trackWidths.Add(fixedW);
                        isAutoCol.Add(false);
                        frValuesCol.Add(0f);
                    }
                }
            }
            else
            {
                trackWidths.Add(0f);
                isAutoCol.Add(true);
                frValuesCol.Add(0f);
            }
            List<float> trackHeights = new List<float>();
            float totalNonGrowableRows = 0f;
            float totalFrRows = 0f;
            List<bool> isAutoRow = new List<bool>();
            List<float> frValuesRow = new List<float>();
            if (!string.IsNullOrEmpty(rowsStr))
            {
                Match repeatMatch = Regex.Match(rowsStr, @"\s*repeat\s*\(\s*(\d+)\s*,\s*(.*?)\s*\)");
                List<string> rowDefsList = new List<string>();
                if (repeatMatch.Success)
                {
                    int repeatNum = int.Parse(repeatMatch.Groups[1].Value);
                    string repeatUnit = repeatMatch.Groups[2].Value;
                    for (int k = 0; k < repeatNum; k++)
                    {
                        rowDefsList.Add(repeatUnit);
                    }
                }
                else
                {
                    rowDefsList = rowsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                foreach (string def in rowDefsList)
                {
                    if (def == "auto")
                    {
                        trackHeights.Add(0f);
                        isAutoRow.Add(true);
                        frValuesRow.Add(0f);
                    }
                    else if (def.EndsWith("fr"))
                    {
                        string frStr = def.Replace("fr", "").Trim();
                        float frValue = string.IsNullOrEmpty(frStr) ? 1f : float.Parse(frStr);
                        totalFrRows += frValue;
                        trackHeights.Add(0f);
                        isAutoRow.Add(false);
                        frValuesRow.Add(frValue);
                    }
                    else
                    {
                        float fixedH = HtmlLayoutUtils.ParseSize(def, ComputedContentHeight, viewportWidth, viewportHeight);
                        if (float.IsNaN(fixedH)) fixedH = 0f;
                        trackHeights.Add(fixedH);
                        isAutoRow.Add(false);
                        frValuesRow.Add(0f);
                    }
                }
            }
            else
            {
                trackHeights.Add(0f);
                isAutoRow.Add(true);
                frValuesRow.Add(0f);
            }
            string autoFlow = Style.GridAutoFlow.ToLower();
            bool isColumnFlow = autoFlow.StartsWith("column");
            int majorTracks = isColumnFlow ? trackHeights.Count : trackWidths.Count;
            int minorTracks = isColumnFlow ? trackWidths.Count : trackHeights.Count;
            int numChildren = visibleChildren.Count;
            if (majorTracks == 0) majorTracks = 1;
            int neededMinor = (numChildren + majorTracks - 1) / majorTracks;
            while (minorTracks < neededMinor)
            {
                if (isColumnFlow)
                {
                    trackWidths.Add(0f);
                    isAutoCol.Add(true);
                    frValuesCol.Add(0f);
                }
                else
                {
                    trackHeights.Add(0f);
                    isAutoRow.Add(true);
                    frValuesRow.Add(0f);
                }
                minorTracks++;
            }
            List<List<HtmlElement>> gridCells = new List<List<HtmlElement>>();
            for (int r = 0; r < trackHeights.Count; r++)
            {
                gridCells.Add(new List<HtmlElement>(new HtmlElement[trackWidths.Count]));
            }
            for (int i = 0; i < numChildren; i++)
            {
                int row, col;
                if (isColumnFlow)
                {
                    row = i % trackHeights.Count;
                    col = i / trackHeights.Count;
                }
                else
                {
                    row = i / trackWidths.Count;
                    col = i % trackWidths.Count;
                }
                while (row >= trackHeights.Count)
                {
                    trackHeights.Add(0f);
                    isAutoRow.Add(true);
                    frValuesRow.Add(0f);
                    gridCells.Add(new List<HtmlElement>(new HtmlElement[trackWidths.Count]));
                }
                while (col >= trackWidths.Count)
                {
                    trackWidths.Add(0f);
                    isAutoCol.Add(true);
                    frValuesCol.Add(0f);
                    for (int r = 0; r < gridCells.Count; r++)
                    {
                        gridCells[r].Add(null);
                    }
                }
                gridCells[row][col] = visibleChildren[i];
            }
            float[] minTrackW = new float[trackWidths.Count];
            for (int col = 0; col < trackWidths.Count; col++)
            {
                float maxInCol = 0f;
                for (int row = 0; row < trackHeights.Count; row++)
                {
                    HtmlElement child = gridCells[row][col];
                    if (child != null)
                    {
                        Vector2 intrinsic = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                        Vector4 childMargin = HtmlLayoutUtils.ParseMargins(child.Style, 0, viewportWidth, viewportHeight);
                        float m_left = float.IsNaN(childMargin.W) ? 0 : childMargin.W;
                        float m_right = float.IsNaN(childMargin.Y) ? 0 : childMargin.Y;
                        maxInCol = Math.Max(maxInCol, intrinsic.X + m_left + m_right);
                    }
                }
                minTrackW[col] = maxInCol;
            }
            for (int col = 0; col < trackWidths.Count; col++)
            {
                if (isAutoCol[col])
                {
                    trackWidths[col] = minTrackW[col];
                    totalNonGrowableCols += trackWidths[col];
                }
                else if (frValuesCol[col] > 0)
                {
                    trackWidths[col] = 0f;
                }
                else
                {
                    trackWidths[col] = Math.Max(trackWidths[col], minTrackW[col]);
                    totalNonGrowableCols += trackWidths[col];
                }
            }
            float remainingSpaceCols = Math.Max(0f, ComputedContentWidth - totalNonGrowableCols - colGap * Math.Max(0, trackWidths.Count - 1));
            float frUnitCols = totalFrCols > 0 ? remainingSpaceCols / totalFrCols : 0f;
            for (int col = 0; col < trackWidths.Count; col++)
            {
                if (frValuesCol[col] > 0)
                {
                    trackWidths[col] += frValuesCol[col] * frUnitCols;
                    trackWidths[col] = Math.Max(trackWidths[col], minTrackW[col]);
                }
            }
            float[] minTrackH = new float[trackHeights.Count];
            for (int row = 0; row < trackHeights.Count; row++)
            {
                float maxInRow = 0f;
                for (int col = 0; col < trackWidths.Count; col++)
                {
                    HtmlElement child = gridCells[row][col];
                    if (child != null)
                    {
                        Vector2 intrinsic = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                        Vector4 childMargin = HtmlLayoutUtils.ParseMargins(child.Style, 0, viewportWidth, viewportHeight);
                        float m_top = float.IsNaN(childMargin.X) ? 0 : childMargin.X;
                        float m_bottom = float.IsNaN(childMargin.Z) ? 0 : childMargin.Z;
                        maxInRow = Math.Max(maxInRow, intrinsic.Y + m_top + m_bottom);
                    }
                }
                minTrackH[row] = maxInRow;
            }
            for (int row = 0; row < trackHeights.Count; row++)
            {
                if (isAutoRow[row])
                {
                    trackHeights[row] = minTrackH[row];
                    totalNonGrowableRows += trackHeights[row];
                }
                else if (frValuesRow[row] > 0)
                {
                    trackHeights[row] = 0f;
                }
                else
                {
                    trackHeights[row] = Math.Max(trackHeights[row], minTrackH[row]);
                    totalNonGrowableRows += trackHeights[row];
                }
            }
            float remainingSpaceRows = Math.Max(0f, ComputedContentHeight - totalNonGrowableRows - rowGap * Math.Max(0, trackHeights.Count - 1));
            float frUnitRows = totalFrRows > 0 ? remainingSpaceRows / totalFrRows : 0f;
            for (int row = 0; row < trackHeights.Count; row++)
            {
                if (frValuesRow[row] > 0)
                {
                    trackHeights[row] += frValuesRow[row] * frUnitRows;
                    trackHeights[row] = Math.Max(trackHeights[row], minTrackH[row]);
                }
            }
            float currentY = ComputedContentY;
            for (int row = 0; row < trackHeights.Count; row++)
            {
                float trackH = trackHeights[row];
                float currentX = ComputedContentX;
                for (int col = 0; col < trackWidths.Count; col++)
                {
                    HtmlElement child = gridCells[row][col];
                    if (child != null)
                    {
                        float trackW = trackWidths[col];
                        Vector4 childMargin = HtmlLayoutUtils.ParseMargins(child.Style, trackW, viewportWidth, viewportHeight);
                        float m_left = float.IsNaN(childMargin.W) ? 0 : childMargin.W;
                        float m_right = float.IsNaN(childMargin.Y) ? 0 : childMargin.Y;
                        float m_top = float.IsNaN(childMargin.X) ? 0 : childMargin.X;
                        float m_bottom = float.IsNaN(childMargin.Z) ? 0 : childMargin.Z;
                        float forcedW = trackW - m_left - m_right;
                        float forcedH = trackH - m_top - m_bottom;
                        float posX = currentX + m_left;
                        float posY = currentY + m_top;
                        child.ComputeLayout(posX, posY, forcedW, forcedH, viewportWidth, viewportHeight, textRenderer, fs, forcedW, forcedH);
                    }
                    currentX += trackWidths[col] + colGap;
                }
                currentY += trackHeights[row] + rowGap;
            }
            ComputedContentHeight = currentY - ComputedContentY - rowGap;
            float maxChildBottom = 0f;
            foreach (var child in visibleChildren)
            {
                maxChildBottom = Math.Max(maxChildBottom, child.ComputedPosition.Y + child.ComputedHeight - ComputedContentY);
            }
            ComputedContentHeight = Math.Max(ComputedContentHeight, maxChildBottom);
            Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            ComputedHeight = ComputedContentHeight + pad.X + pad.Z;
            ComputedBackgroundHeight = ComputedHeight;
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
            List<List<HtmlElement>> lines = new List<List<HtmlElement>>();
            List<HtmlElement> currentLine = new List<HtmlElement>();
            for (int i = 0; i < normalChildren.Count; i++)
            {
                HtmlElement child = normalChildren[i];
                string childDisplay = child.GetEffectiveDisplay();
                bool isInline = childDisplay.StartsWith("inline");
                float childW = HtmlLayoutUtils.ParseSize(child.Style.WidthStr, ComputedContentWidth, viewportWidth, viewportHeight);
                float childH = HtmlLayoutUtils.ParseSize(child.Style.HeightStr, ComputedContentHeight, viewportWidth, viewportHeight);
                Vector4 parsedMargin = HtmlLayoutUtils.ParseMargins(child.Style, ComputedContentHeight, viewportWidth, viewportHeight);
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
                    if (float.IsNaN(childW)) childW = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).X;
                    if (float.IsNaN(childH)) childH = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs).Y;
                    float availW = ComputedContentWidth - currentX - c_m_left - c_m_right;
                    float effW = childW + c_m_left + c_m_right;
                    if (currentX > 0 && effW > availW)
                    {
                        lines.Add(currentLine.ToList());
                        currentY += maxLineH;
                        currentX = 0;
                        maxLineH = 0;
                        currentLine.Clear();
                    }
                    float child_pos_x = currentX + c_m_left;
                    float child_pos_y = currentY + c_m_top;
                    child.ComputeLayout(ComputedContentX + child_pos_x, ComputedContentY + child_pos_y, childW, childH, viewportWidth, viewportHeight, textRenderer, fs);
                    currentX += child.ComputedWidth + c_m_left + c_m_right;
                    maxLineH = Math.Max(maxLineH, child.ComputedHeight + c_m_top + c_m_bottom);
                    currentLine.Add(child);
                }
                else
                {
                    if (currentX > 0)
                    {
                        lines.Add(currentLine.ToList());
                        currentY += maxLineH;
                        currentX = 0;
                        maxLineH = 0;
                        currentLine.Clear();
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
            if (currentX > 0)
            {
                lines.Add(currentLine.ToList());
                currentY += maxLineH;
            }
            string textAlign = string.IsNullOrEmpty(Style.TextAlign) ? "left" : Style.TextAlign;
            if (textAlign != "left")
            {
                foreach (var line in lines)
                {
                    if (line.Count == 0) continue;
                    HtmlElement first = line[0];
                    float startX = first.ComputedPosition.X - first.Style.Margin.W;
                    HtmlElement last = line[line.Count - 1];
                    float endX = last.ComputedPosition.X + last.ComputedWidth + last.Style.Margin.Y;
                    float lineW = endX - startX;
                    float offset = 0;
                    if (textAlign == "center")
                    {
                        offset = (ComputedContentWidth - lineW) / 2;
                    }
                    else if (textAlign == "right")
                    {
                        offset = ComputedContentWidth - lineW;
                    }
                    if (offset > 0)
                    {
                        foreach (var c in line)
                        {
                            ShiftX(c, offset);
                        }
                    }
                }
            }
            foreach (var child in positionedChildren)
            {
                child.ComputeLayout(ComputedContentX, ComputedContentY, ComputedContentWidth, ComputedContentHeight, viewportWidth, viewportHeight, textRenderer, fs);
            }
        }
        private void ShiftX(HtmlElement e, float off)
        {
            e.ComputedPosition = new Vector2(e.ComputedPosition.X + off, e.ComputedPosition.Y);
            e.ComputedBackgroundX += off;
            e.ComputedContentX += off;
            foreach (var ch in e.Children) ShiftX(ch, off);
        }
        public virtual Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            string id = Attributes.GetValueOrDefault("id", "no-id");
            if (!_intrinsicDirty && _cachedViewportWidth == viewportWidth && _cachedViewportHeight == viewportHeight && _cachedFs == fs)
            {
                return _cachedIntrinsicSize;
            }
            if (Style.Display == "none") return new Vector2(0, 0);
            float iw = 0;
            float ih = 0;
            Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            List<HtmlElement> visibleChildren = Children.Where(c => c.GetEffectiveDisplay() != "none").ToList();
            List<HtmlElement> normalChildren = visibleChildren.Where(c => c.Style.Position != "absolute" && c.Style.Position != "fixed").ToList();
            if (visibleChildren.Count == 0)
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
                    float gap = HtmlLayoutUtils.ParseSize(Style.GapStr, 0, viewportWidth, viewportHeight);
                    if (float.IsNaN(gap)) gap = 0;
                    int count = normalChildren.Count;
                    float totalGap = gap * (count - 1);
                    float sum_main = 0;
                    float max_cross = 0;
                    for (int i = 0; i < count; i++)
                    {
                        HtmlElement child = normalChildren[i];
                        Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                        Vector4 parsedMargin = HtmlLayoutUtils.ParseMargins(child.Style, 0, viewportWidth, viewportHeight);
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
                else if (Style.Display == "grid")
                {
                    string columnsStr = Style.GridTemplateColumnsStr;
                    string rowsStr = Style.GridTemplateRowsStr;
                    string gapStr = Style.GapStr;
                    string[] gapDefs = string.IsNullOrEmpty(gapStr) ? new string[0] : gapStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float rowGap = gapDefs.Length > 0 ? HtmlLayoutUtils.ParseSize(gapDefs[0], 0, viewportWidth, viewportHeight) : 0f;
                    float colGap = gapDefs.Length > 1 ? HtmlLayoutUtils.ParseSize(gapDefs[1], 0, viewportWidth, viewportHeight) : rowGap;
                    List<float> trackWidthsIntrinsic = new List<float>();
                    List<float> frValuesColIntrinsic = new List<float>();
                    List<string> colDefsList = new List<string>();
                    if (!string.IsNullOrEmpty(columnsStr))
                    {
                        Match repeatMatch = Regex.Match(columnsStr, @"\s*repeat\s*\(\s*(\d+)\s*,\s*(.*?)\s*\)");
                        if (repeatMatch.Success)
                        {
                            int repeatNum = int.Parse(repeatMatch.Groups[1].Value);
                            string repeatUnit = repeatMatch.Groups[2].Value;
                            for (int k = 0; k < repeatNum; k++)
                            {
                                colDefsList.Add(repeatUnit);
                            }
                        }
                        else
                        {
                            colDefsList = columnsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        }
                        foreach (string def in colDefsList)
                        {
                            if (def.EndsWith("fr"))
                            {
                                string frStr = def.Replace("fr", "").Trim();
                                float frValue = string.IsNullOrEmpty(frStr) ? 1f : float.Parse(frStr);
                                trackWidthsIntrinsic.Add(0f);
                                frValuesColIntrinsic.Add(frValue);
                            }
                            else
                            {
                                float w = HtmlLayoutUtils.ParseSize(def, 0, viewportWidth, viewportHeight);
                                trackWidthsIntrinsic.Add(float.IsNaN(w) ? 0f : w);
                                frValuesColIntrinsic.Add(0f);
                            }
                        }
                    }
                    else
                    {
                        trackWidthsIntrinsic.Add(0f);
                        frValuesColIntrinsic.Add(0f);
                    }
                    List<float> trackHeightsIntrinsic = new List<float>();
                    List<float> frValuesRowIntrinsic = new List<float>();
                    List<string> rowDefsList = new List<string>();
                    if (!string.IsNullOrEmpty(rowsStr))
                    {
                        Match repeatMatch = Regex.Match(rowsStr, @"\s*repeat\s*\(\s*(\d+)\s*,\s*(.*?)\s*\)");
                        if (repeatMatch.Success)
                        {
                            int repeatNum = int.Parse(repeatMatch.Groups[1].Value);
                            string repeatUnit = repeatMatch.Groups[2].Value;
                            for (int k = 0; k < repeatNum; k++)
                            {
                                rowDefsList.Add(repeatUnit);
                            }
                        }
                        else
                        {
                            rowDefsList = rowsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        }
                        foreach (string def in rowDefsList)
                        {
                            if (def.EndsWith("fr"))
                            {
                                string frStr = def.Replace("fr", "").Trim();
                                float frValue = string.IsNullOrEmpty(frStr) ? 1f : float.Parse(frStr);
                                trackHeightsIntrinsic.Add(0f);
                                frValuesRowIntrinsic.Add(frValue);
                            }
                            else
                            {
                                float h = HtmlLayoutUtils.ParseSize(def, 0, viewportWidth, viewportHeight);
                                trackHeightsIntrinsic.Add(float.IsNaN(h) ? 0f : h);
                                frValuesRowIntrinsic.Add(0f);
                            }
                        }
                    }
                    else
                    {
                        trackHeightsIntrinsic.Add(0f);
                        frValuesRowIntrinsic.Add(0f);
                    }
                    string autoFlow = Style.GridAutoFlow.ToLower();
                    bool isColumnFlow = autoFlow.StartsWith("column");
                    int numCols = trackWidthsIntrinsic.Count;
                    int numRows = trackHeightsIntrinsic.Count;
                    if (isColumnFlow)
                    {
                        numRows = Math.Max(numRows, (visibleChildren.Count + numCols - 1) / numCols);
                    }
                    else
                    {
                        numCols = Math.Max(numCols, (visibleChildren.Count + numRows - 1) / numRows);
                    }
                    float[] maxColW = new float[numCols];
                    float[] maxRowH = new float[numRows];
                    for (int i = 0; i < visibleChildren.Count; i++)
                    {
                        int row, col;
                        if (isColumnFlow)
                        {
                            row = i % numRows;
                            col = i / numRows;
                        }
                        else
                        {
                            row = i / numCols;
                            col = i % numCols;
                        }
                        HtmlElement child = visibleChildren[i];
                        Vector2 childSize = child.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                        Vector4 childMargin = HtmlLayoutUtils.ParseMargins(child.Style, 0, viewportWidth, viewportHeight);
                        float m_left = float.IsNaN(childMargin.W) ? 0 : childMargin.W;
                        float m_right = float.IsNaN(childMargin.Y) ? 0 : childMargin.Y;
                        float m_top = float.IsNaN(childMargin.X) ? 0 : childMargin.X;
                        float m_bottom = float.IsNaN(childMargin.Z) ? 0 : childMargin.Z;
                        maxRowH[row] = Math.Max(maxRowH[row], childSize.Y + m_top + m_bottom);
                        maxColW[col] = Math.Max(maxColW[col], childSize.X + m_left + m_right);
                    }
                    float sumW = 0f;
                    for (int col = 0; col < numCols; col++)
                    {
                        sumW += maxColW[col];
                    }
                    sumW += colGap * (numCols - 1);
                    float sumH = 0f;
                    for (int row = 0; row < numRows; row++)
                    {
                        sumH += maxRowH[row];
                    }
                    sumH += rowGap * (numRows - 1);
                    iw = sumW;
                    ih = sumH;
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
                        Vector4 parsedMargin = HtmlLayoutUtils.ParseMargins(child.Style, 0, viewportWidth, viewportHeight);
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
                                childSpecW = HtmlLayoutUtils.ParseSize(child.Style.WidthStr, 0, viewportWidth, viewportHeight);
                            }
                            if (!float.IsNaN(childSpecW))
                            {
                                string childBox = child.Style.BoxSizing;
                                Vector4 childPad = HtmlLayoutUtils.ParsePaddings(child.Style, 0, viewportWidth, viewportHeight);
                                Vector4 childBorder = HtmlLayoutUtils.ParseBorderWidths(child.Style, 0, viewportWidth, viewportHeight);
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
                float spec = HtmlLayoutUtils.ParseSize(Style.WidthStr, 0, viewportWidth, viewportHeight);
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
                float spec = HtmlLayoutUtils.ParseSize(Style.HeightStr, 0, viewportWidth, viewportHeight);
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
            _cachedIntrinsicSize = new Vector2(iw, ih);
            _cachedViewportWidth = viewportWidth;
            _cachedViewportHeight = viewportHeight;
            _cachedFs = fs;
            _intrinsicDirty = false;
            return _cachedIntrinsicSize;
        }
    }
}