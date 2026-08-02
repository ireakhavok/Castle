// Folder: SiegeEngine.Core.UI.Elements
// File: TableElement.cs
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
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

            // Find rows (and keep the wrappers so we can correct their heights)
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
                int rowCols = row.Children.Count(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th");
                colCount = Math.Max(colCount, rowCols);
            }
            if (colCount == 0) return;

            // Intrinsic column widths
            var colWidths = new float[colCount];
            foreach (var row in allRows)
            {
                var cells = row.Children.Where(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th").ToList();
                for (int i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    cell.Style.Display = "table-cell";
                    var intrinsic = cell.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, Style.FontSize);
                    colWidths[i] = Math.Max(colWidths[i], intrinsic.X);
                }
            }

            bool collapse = Style.BorderCollapse == "collapse";
            float tableBorderW = collapse ? 0 : BorderWidth.W + BorderWidth.Y;

            float totalW = colWidths.Sum() + tableBorderW;
            if (!float.IsNaN(ComputedContentWidth) && ComputedContentWidth > totalW)
            {
                float extra = ComputedContentWidth - totalW;
                float perCol = extra / colCount;
                for (int i = 0; i < colCount; i++)
                    colWidths[i] += perCol;
            }

            float finalRowWidth = colWidths.Sum();

            // Layout every row
            float currentY = ComputedContentY;
            var rowHeights = new Dictionary<HtmlElement, float>();

            foreach (var row in allRows)
            {
                row.Style.Display = "table-row";
                float rowH = 0;
                var cells = row.Children.Where(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th").ToList();
                float currentX = ComputedContentX;

                // Pass 1 – force widths, measure height
                for (int i = 0; i < colCount; i++)
                {
                    if (i < cells.Count)
                    {
                        var cell = cells[i];
                        cell.Style.Display = "table-cell";
                        cell.ComputeLayout(currentX, currentY, colWidths[i], float.NaN,
                            viewportWidth, viewportHeight, textRenderer, Style.FontSize,
                            forcedWidth: colWidths[i], forcedHeight: float.NaN);
                        rowH = Math.Max(rowH, cell.ComputedHeight);
                    }
                    currentX += colWidths[i];
                }

                // Pass 2 – force uniform height
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

                // Full-width row geometry
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
            // calculation (and therefore the scrollbar decision) is accurate
            foreach (var kv in wrapperToRows)
            {
                var wrapper = kv.Key;
                float wrapperH = 0f;
                foreach (var r in kv.Value)
                    wrapperH += rowHeights.TryGetValue(r, out float h) ? h : 0f;

                wrapper.ComputedHeight = wrapperH;
                wrapper.ComputedBackgroundHeight = wrapperH;
                wrapper.ComputedContentHeight = wrapperH;
                // Keep the wrapper’s X geometry full-width as well
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

            int colCount = rows.Max(r => r.Children.Count(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th"));
            if (colCount == 0)
                return base.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);

            var colMinW = new float[colCount];
            float h = 0f;
            foreach (var row in rows)
            {
                float rowH = 0f;
                var cells = row.Children.Where(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th").ToList();
                for (int i = 0; i < cells.Count && i < colCount; i++)
                {
                    var cellSize = cells[i].ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                    colMinW[i] = Math.Max(colMinW[i], cellSize.X);
                    rowH = Math.Max(rowH, cellSize.Y);
                }
                h += rowH;
            }
            return new Vector2(colMinW.Sum(), h);
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }
    }
}