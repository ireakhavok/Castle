// Folder: SiegeEngine.UI
// File: CssStyle.cs
using System.Numerics;
namespace SiegeEngine.UI
{
    public class CssStyle
    {
        public string Position { get; set; } = "static";
        public string LeftStr { get; set; }
        public string TopStr { get; set; }
        public string WidthStr { get; set; }
        public string HeightStr { get; set; }
        public string MinWidthStr { get; set; }
        public string MinHeightStr { get; set; }
        public string MaxWidthStr { get; set; }
        public string MaxHeightStr { get; set; }
        public string Background { get; set; }
        public Vector4 BackgroundColor { get; set; }
        public string Color { get; set; }
        public Vector4 TextColor { get; set; }
        public string FontSizeStr { get; set; }
        public float FontSize { get; set; }
        public string FontFamily { get; set; }
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
        public string BoxSizing { get; set; }
        public CssStyle Clone()
        {
            return new CssStyle
            {
                Position = Position,
                LeftStr = LeftStr,
                TopStr = TopStr,
                WidthStr = WidthStr,
                HeightStr = HeightStr,
                MinWidthStr = MinWidthStr,
                MinHeightStr = MinHeightStr,
                MaxWidthStr = MaxWidthStr,
                MaxHeightStr = MaxHeightStr,
                Background = Background,
                BackgroundColor = BackgroundColor,
                Color = Color,
                TextColor = TextColor,
                FontSizeStr = FontSizeStr,
                FontSize = FontSize,
                FontFamily = FontFamily,
                Display = Display,
                FlexDirection = FlexDirection,
                AlignItems = AlignItems,
                JustifyContent = JustifyContent,
                Flex = Flex,
                PaddingStr = PaddingStr,
                PaddingTopStr = PaddingTopStr,
                PaddingRightStr = PaddingRightStr,
                PaddingBottomStr = PaddingBottomStr,
                PaddingLeftStr = PaddingLeftStr,
                MarginStr = MarginStr,
                Margin = Margin,
                GapStr = GapStr,
                TextAlign = TextAlign,
                WhiteSpace = WhiteSpace,
                TextTransform = TextTransform,
                BorderWidthStr = BorderWidthStr,
                BorderStyle = BorderStyle,
                BorderColor = BorderColor,
                BorderTopWidthStr = BorderTopWidthStr,
                BorderTopStyle = BorderTopStyle,
                BorderTopColor = BorderTopColor,
                BorderRightWidthStr = BorderRightWidthStr,
                BorderRightStyle = BorderRightStyle,
                BorderRightColor = BorderRightColor,
                BorderBottomWidthStr = BorderBottomWidthStr,
                BorderBottomStyle = BorderBottomStyle,
                BorderBottomColor = BorderBottomColor,
                BorderLeftWidthStr = BorderLeftWidthStr,
                BorderLeftStyle = BorderLeftStyle,
                BorderLeftColor = BorderLeftColor,
                BoxSizing = BoxSizing
            };
        }
    }
}