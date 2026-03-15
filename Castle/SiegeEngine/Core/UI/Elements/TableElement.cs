// Folder: SiegeEngine.UI
// File: TableElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
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

            // Find rows
            var rows = Children.Where(c => c.Tag.ToLower() == "tr" || c.Tag.ToLower() == "thead" || c.Tag.ToLower() == "tbody" || c.Tag.ToLower() == "tfoot").ToList();
            var allRows = new List<HtmlElement>();
            foreach (var rowGroup in rows)
            {
                if (rowGroup.Tag.ToLower() != "tr")
                {
                    allRows.AddRange(rowGroup.Children.Where(c => c.Tag.ToLower() == "tr"));
                }
                else
                {
                    allRows.Add(rowGroup);
                }
            }

            if (allRows.Count == 0) return;

            // Determine column count
            int colCount = 0;
            foreach (var row in allRows)
            {
                int rowCols = row.Children.Count(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th");
                colCount = Math.Max(colCount, rowCols);
            }

            // Compute intrinsic widths for each column
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

            // If border-collapse, adjust for borders
            bool collapse = Style.BorderCollapse == "collapse";
            float tableBorderW = collapse ? 0 : BorderWidth.W + BorderWidth.Y;

            // Total width
            float totalW = colWidths.Sum() + tableBorderW;
            if (!float.IsNaN(ComputedContentWidth) && ComputedContentWidth > totalW)
            {
                // Distribute extra width
                float extra = ComputedContentWidth - totalW;
                float perCol = extra / colCount;
                for (int i = 0; i < colCount; i++)
                {
                    colWidths[i] += perCol;
                }
            }

            // Layout rows
            float currentY = ComputedContentY;
            foreach (var row in allRows)
            {
                row.Style.Display = "table-row";
                float rowH = 0;
                var cells = row.Children.Where(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th").ToList();
                float currentX = ComputedContentX;
                for (int i = 0; i < colCount; i++)
                {
                    HtmlElement cell = i < cells.Count ? cells[i] : null;
                    if (cell != null)
                    {
                        cell.ComputeLayout(currentX, currentY, colWidths[i], float.NaN, viewportWidth, viewportHeight, textRenderer, Style.FontSize);
                        rowH = Math.Max(rowH, cell.ComputedHeight);
                    }
                    currentX += colWidths[i];
                }
                // Set row height and adjust cells
                row.ComputedHeight = rowH;
                foreach (var cell in cells)
                {
                    cell.ComputedHeight = rowH;
                }
                currentY += rowH;
            }
        }

        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            var rows = Children.Where(c => c.Tag.ToLower() == "tr").ToList();
            if (rows.Count == 0) return base.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);

            float maxW = 0;
            float h = 0;
            int colCount = rows.Max(r => r.Children.Count(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th"));

            var colMinW = new float[colCount];

            foreach (var row in rows)
            {
                float rowH = 0;
                var cells = row.Children.Where(c => c.Tag.ToLower() == "td" || c.Tag.ToLower() == "th").ToList();
                for (int i = 0; i < cells.Count; i++)
                {
                    var cellSize = cells[i].ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
                    colMinW[i] = Math.Max(colMinW[i], cellSize.X);
                    rowH = Math.Max(rowH, cellSize.Y);
                }
                h += rowH;
            }
            maxW = colMinW.Sum();

            return new Vector2(maxW, h);
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }
    }
}