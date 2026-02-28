// Folder: SiegeEngine.Core.UI
// File: NavElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public class NavElement : HtmlElement
    {
        public NavElement()
        {
            Tag = "nav";
            Style.Display = "flex";
            Style.BackgroundColor = new Vector4(0.12f, 0.12f, 0.15f, 1.0f);
            Style.Color = "#ddd";
            Style.HeightStr = "20px";
            Style.AlignItems = "center";
            Style.JustifyContent = "flex-start";
            Style.PaddingStr = "0 8px";
            Style.FontSizeStr = "13px";
            Style.BorderBottomWidthStr = "1px";
            Style.BorderBottomStyle = "solid";
            Style.BorderBottomColor = new Vector4(0.27f, 0.27f, 0.27f, 1.0f);
            Style.Position = "relative";
        }

        public void SetupIDEMenu()
        {
            Children.Clear();

            AddMenuItem("File", "CastleBuilder.File.NewProject");
            AddMenuItem("Edit", "CastleBuilder.Edit.Undo");
            AddMenuItem("View", "ToolChest.AssetBrowserPanel.Open");
            AddMenuItem("Castle", "MapRoom.TerrainCreatorPanel.OpenBlank");
            AddMenuItem("Tools", "ToolChest.AssetBrowserPanel.Open");
            AddMenuItem("Window", "CastleBuilder.Window.Properties");
            AddMenuItem("Help", "CastleBuilder.Help.About");
        }

        private void AddMenuItem(string label, string dataHook)
        {
            var item = new HtmlElement
            {
                Tag = "div",
                Style = {
                    Display = "inline-block",
                    PaddingStr = "0 12px",
                    HeightStr = "100%"
                }
            };
            item.Children.Add(new TextElement { Content = label });
            item.Attributes["data-hook"] = dataHook;
            Children.Add(item);
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            forcedHeight = 20f;
            if (float.IsNaN(forcedWidth)) forcedWidth = parentWidth;
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }
    }
}