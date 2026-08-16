// Folder: SiegeEngine.Core.UI.Elements
// File: TableElement.cs
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
{
    public class TableElement : HtmlElement
    {
        public TableElement()
        {
            Tag = "table";
            Style.Display = "table";
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);

            // Collect rows and the thead/tbody/tfoot wrappers that own them
            var rowGroups = Children.Where(c =>
            {
                string t = c.Tag.ToLower();
                return t == "tr" || t == "thead" || t == "tbody" || t == "tfoot";
            }).ToList();

            var allRows = new List<HtmlElement>();
            var wrapperToRows = new Dictionary<HtmlElement, List<HtmlElement>>();

            foreach (var group in rowGroups)
            {
                if (group.Tag.ToLower() == "tr")
                {
                    allRows.Add(group);
                }
                else
                {
                    var groupRows = group.Children.Where(c => c.Tag.ToLower() == "tr").ToList();
                    allRows.AddRange(groupRows);
                    wrapperToRows[group] = groupRows;
                }
            }

            if (allRows.Count == 0) return;

            // Column count
            int colCount = 0;
            foreach (var row in allRows)
            {
                int n = row.Children.Count(c =>
                {
                    string t = c.Tag.ToLower();
                    return t == "td" || t == "th";
                });
                if (n > colCount) colCount = n;
            }
            if (colCount == 0) return;

            // Intrinsic min-content width per column
            var colWidths = new float[colCount];
            foreach (var row in allRows)
            {
                var cells = row.Children.Where(c =>
                {
                    string t = c.Tag.ToLower();
                    return t == "td" || t == "th";
                }).ToList();

                for (int i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    cell.Style.Display = "table-cell";
                    Vector2 intrinsic = cell.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, Style.FontSize);
                    if (intrinsic.X > colWidths[i])
                        colWidths[i] = intrinsic.X;
                }
            }

            // Usable content width the table already received from its parent
            float usable = ComputedContentWidth;
            if (float.IsNaN(usable) || usable <= 0f)
                usable = colWidths.Sum();

            float total = 0f;
            for (int i = 0; i < colCount; i++) total += colWidths[i];

            // Distribute any free space equally (original behaviour)
            if (total < usable - 0.5f)
            {
                float extra = usable - total;
                float per = extra / colCount;
                for (int i = 0; i < colCount; i++)
                    colWidths[i] += per;
            }

            // Force the sum to be exactly the usable width so the right edge
            // of the last column lands on the table’s content edge.
            // Any residual floating-point error is absorbed by the last column.
            float sumExceptLast = 0f;
            for (int i = 0; i < colCount - 1; i++)
                sumExceptLast += colWidths[i];
            if (colCount > 0)
                colWidths[colCount - 1] = Math.Max(0f, usable - sumExceptLast);

            float finalRowWidth = usable;

            // Layout every row
            float currentY = ComputedContentY;
            var rowHeights = new Dictionary<HtmlElement, float>();

            foreach (var row in allRows)
            {
                row.Style.Display = "table-row";
                float rowH = 0f;
                var cells = row.Children.Where(c =>
                {
                    string t = c.Tag.ToLower();
                    return t == "td" || t == "th";
                }).ToList();

                // Pass 1 – force widths, discover height
                float currentX = ComputedContentX;
                for (int i = 0; i < colCount; i++)
                {
                    if (i < cells.Count)
                    {
                        var cell = cells[i];
                        cell.Style.Display = "table-cell";
                        cell.ComputeLayout(currentX, currentY, colWidths[i], float.NaN,
                            viewportWidth, viewportHeight, textRenderer, Style.FontSize,
                            forcedWidth: colWidths[i], forcedHeight: float.NaN);
                        if (cell.ComputedHeight > rowH)
                            rowH = cell.ComputedHeight;
                    }
                    currentX += colWidths[i];
                }

                // Pass 2 – force uniform row height
                currentX = ComputedContentX;
                for (int i = 0; i < colCount; i++)
                {
                    if (i < cells.Count)
                    {
                        var cell = cells[i];
                        cell.ComputeLayout(currentX, currentY, colWidths[i], rowH,
                            viewportWidth, viewportHeight, textRenderer, Style.FontSize,
                            forcedWidth: colWidths[i], forcedHeight: rowH);
                    }
                    currentX += colWidths[i];
                }

                // Full-width row geometry (tr:hover, borders, etc.)
                row.ComputedWidth = finalRowWidth;
                row.ComputedBackgroundWidth = finalRowWidth;
                row.ComputedContentWidth = finalRowWidth;
                row.ComputedHeight = rowH;
                row.ComputedBackgroundHeight = rowH;
                row.ComputedContentHeight = rowH;
                row.ComputedPosition = new Vector2(ComputedContentX, currentY);
                row.ComputedBackgroundX = ComputedContentX;
                row.ComputedBackgroundY = currentY;
                row.ComputedContentX = ComputedContentX;
                row.ComputedContentY = currentY;

                rowHeights[row] = rowH;
                currentY += rowH;
            }

            // Correct thead / tbody / tfoot heights so the root content-height
            // (and therefore the scrollbar decision) stays accurate
            foreach (var kv in wrapperToRows)
            {
                var wrapper = kv.Key;
                float wrapperH = 0f;
                foreach (var r in kv.Value)
                    wrapperH += rowHeights.TryGetValue(r, out float h) ? h : 0f;

                wrapper.ComputedHeight = wrapperH;
                wrapper.ComputedBackgroundHeight = wrapperH;
                wrapper.ComputedContentHeight = wrapperH;
                wrapper.ComputedWidth = finalRowWidth;
                wrapper.ComputedBackgroundWidth = finalRowWidth;
                wrapper.ComputedContentWidth = finalRowWidth;
            }

            // Update the table’s own content height
            float contentH = currentY - ComputedContentY;
            ComputedContentHeight = contentH;
            Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = BorderWidth;
            ComputedHeight = contentH + pad.X + pad.Z + borderW.X + borderW.Z;
            ComputedBackgroundHeight = ComputedHeight;
        }

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            var rows = Children.Where(c => c.Tag.ToLower() == "tr").ToList();
            if (rows.Count == 0)
            {
                rows = Children
                    .Where(c =>
                    {
                        string t = c.Tag.ToLower();
                        return t == "thead" || t == "tbody" || t == "tfoot";
                    })
                    .SelectMany(g => g.Children.Where(c => c.Tag.ToLower() == "tr"))
                    .ToList();
            }
            if (rows.Count == 0)
                return base.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);

            int colCount = 0;
            foreach (var r in rows)
            {
                int n = r.Children.Count(c =>
                {
                    string t = c.Tag.ToLower();
                    return t == "td" || t == "th";
                });
                if (n > colCount) colCount = n;
            }
            if (colCount == 0)
                return base.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);

            var colMin = new float[colCount];
            float totalH = 0f;
            foreach (var row in rows)
            {
                float rowH = 0f;
                var cells = row.Children.Where(c =>
                {
                    string t = c.Tag.ToLower();
                    return t == "td" || t == "th";
                }).ToList();
                for (int i = 0; i < cells.Count && i < colCount; i++)
                {
                    Vector2 sz = cells[i].ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                    if (sz.X > colMin[i]) colMin[i] = sz.X;
                    if (sz.Y > rowH) rowH = sz.Y;
                }
                totalH += rowH;
            }
            return new Vector2(colMin.Sum(), totalH);
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }
    }
}