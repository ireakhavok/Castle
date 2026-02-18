// Folder: SiegeEngine.Core.UI
// File: CssStyle.cs
using System;
using System.Numerics;
namespace SiegeEngine.Core.UI
{
    public class CssStyle
    {
        public string Position { get; set; } = "static";
        public string LeftStr { get; set; }
        public string TopStr { get; set; }
        public string RightStr { get; set; }
        public string BottomStr { get; set; }
        public string WidthStr { get; set; }
        public string HeightStr { get; set; }
        public string MinWidthStr { get; set; }
        public string MinHeightStr { get; set; }
        public string MaxWidthStr { get; set; }
        public string MaxHeightStr { get; set; }
        public string Background { get; set; }
        public Vector4 BackgroundColor { get; set; }
        public string BackgroundImage { get; set; }
        public string Color { get; set; }
        public Vector4 TextColor { get; set; }
        public string FontSizeStr { get; set; }
        public float FontSize { get; set; }
        public string FontFamily { get; set; }
        public string FontWeight { get; set; }
        public string Display { get; set; } = "block";
        public string FlexDirection { get; set; }
        public string AlignItems { get; set; }
        public string JustifyContent { get; set; }
        public string Flex { get; set; }
        public string PaddingStr { get; set; }
        public string PaddingTopStr { get; set; }
        public string PaddingRightStr { get; set; }
        public string PaddingBottomStr { get; set; }
        public string PaddingLeftStr { get; set; }
        public string MarginStr { get; set; }
        public Vector4 Margin { get; set; }
        public string GapStr { get; set; }
        public string TextAlign { get; set; }
        public string WhiteSpace { get; set; }
        public string TextTransform { get; set; }
        public string BorderWidthStr { get; set; }
        public string BorderStyle { get; set; }
        public Vector4 BorderColor { get; set; }
        public string BorderTopWidthStr { get; set; }
        public string BorderTopStyle { get; set; }
        public Vector4 BorderTopColor { get; set; }
        public string BorderRightWidthStr { get; set; }
        public string BorderRightStyle { get; set; }
        public Vector4 BorderRightColor { get; set; }
        public string BorderBottomWidthStr { get; set; }
        public string BorderBottomStyle { get; set; }
        public Vector4 BorderBottomColor { get; set; }
        public string BorderLeftWidthStr { get; set; }
        public string BorderLeftStyle { get; set; }
        public Vector4 BorderLeftColor { get; set; }
        public string BorderCollapse { get; set; }
        public string BorderSpacing { get; set; }
        public string BoxSizing { get; set; } = "content-box";
        public string Transform { get; set; }
        public string ListStyleType { get; set; }
        public string BorderRadiusStr { get; set; }
        public Vector4 BorderRadius { get; set; }
        public string Overflow { get; set; }
        public string OverflowY { get; set; }
        public CssStyle Clone()
        {
            return (CssStyle)MemberwiseClone();
        }
        public void SetProperty(string key, string val)
        {
            switch (key.ToLower())
            {
                case "display":
                    Display = val;
                    break;
                case "position":
                    Position = val;
                    break;
                case "left":
                    LeftStr = val;
                    break;
                case "top":
                    TopStr = val;
                    break;
                case "right":
                    RightStr = val;
                    break;
                case "bottom":
                    BottomStr = val;
                    break;
                case "width":
                    WidthStr = val;
                    break;
                case "height":
                    HeightStr = val;
                    break;
                case "min-width":
                    MinWidthStr = val;
                    break;
                case "min-height":
                    MinHeightStr = val;
                    break;
                case "max-width":
                    MaxWidthStr = val;
                    break;
                case "max-height":
                    MaxHeightStr = val;
                    break;
                case "background":
                case "background-color":
                    Background = val;
                    BackgroundColor = new CssParser().ParseColor(val);
                    break;
                case "background-image":
                    BackgroundImage = val;
                    break;
                case "color":
                    Color = val;
                    TextColor = new CssParser().ParseColor(val);
                    break;
                case "font-size":
                    FontSizeStr = val;
                    break;
                case "font-family":
                    FontFamily = val;
                    break;
                case "font-weight":
                    FontWeight = val;
                    break;
                case "flex-direction":
                    FlexDirection = val;
                    break;
                case "align-items":
                    AlignItems = val;
                    break;
                case "justify-content":
                    JustifyContent = val;
                    break;
                case "flex":
                    Flex = val;
                    break;
                case "padding":
                    PaddingStr = val;
                    break;
                case "padding-top":
                    PaddingTopStr = val;
                    break;
                case "padding-right":
                    PaddingRightStr = val;
                    break;
                case "padding-bottom":
                    PaddingBottomStr = val;
                    break;
                case "padding-left":
                    PaddingLeftStr = val;
                    break;
                case "margin":
                    MarginStr = val;
                    break;
                case "gap":
                    GapStr = val;
                    break;
                case "text-align":
                    TextAlign = val;
                    break;
                case "white-space":
                    WhiteSpace = val;
                    break;
                case "text-transform":
                    TextTransform = val;
                    break;
                case "border-width":
                    BorderWidthStr = val;
                    break;
                case "border-style":
                    BorderStyle = val;
                    break;
                case "border-color":
                    BorderColor = new CssParser().ParseColor(val);
                    break;
                case "border-top-width":
                    BorderTopWidthStr = val;
                    break;
                case "border-top-style":
                    BorderTopStyle = val;
                    break;
                case "border-top-color":
                    BorderTopColor = new CssParser().ParseColor(val);
                    break;
                case "border-right-width":
                    BorderRightWidthStr = val;
                    break;
                case "border-right-style":
                    BorderRightStyle = val;
                    break;
                case "border-right-color":
                    BorderRightColor = new CssParser().ParseColor(val);
                    break;
                case "border-bottom-width":
                    BorderBottomWidthStr = val;
                    break;
                case "border-bottom-style":
                    BorderBottomStyle = val;
                    break;
                case "border-bottom-color":
                    BorderBottomColor = new CssParser().ParseColor(val);
                    break;
                case "border-left-width":
                    BorderLeftWidthStr = val;
                    break;
                case "border-left-style":
                    BorderLeftStyle = val;
                    break;
                case "border-left-color":
                    BorderLeftColor = new CssParser().ParseColor(val);
                    break;
                case "border-collapse":
                    BorderCollapse = val;
                    break;
                case "border-spacing":
                    BorderSpacing = val;
                    break;
                case "box-sizing":
                    BoxSizing = val;
                    break;
                case "transform":
                    Transform = val;
                    break;
                case "list-style-type":
                    ListStyleType = val;
                    break;
                case "border-radius":
                    BorderRadiusStr = val;
                    break;
                case "overflow":
                    Overflow = val;
                    break;
                case "overflow-y":
                    OverflowY = val;
                    break;
                default:
                    Console.WriteLine($"Unsupported CSS property: {key}");
                    break;
            }
        }
    }
}