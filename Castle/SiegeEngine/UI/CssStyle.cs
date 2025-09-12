using System.Numerics;
namespace SiegeEngine.UI
{
    public class CssStyle
    {
        public string Position { get; set; } = "relative";
        public string LeftStr { get; set; }
        public string TopStr { get; set; }
        public string WidthStr { get; set; }
        public string HeightStr { get; set; }
        public string MaxWidthStr { get; set; }
        public string Background { get; set; }
        public Vector4 BackgroundColor { get; set; }
        public string Color { get; set; }
        public Vector4 TextColor { get; set; }
        public string FontSizeStr { get; set; }
        public float FontSize { get; set; }
        public string Display { get; set; } = "block";
        public string FlexDirection { get; set; } = "row";
        public string AlignItems { get; set; }
        public string JustifyContent { get; set; }
        public string PaddingStr { get; set; }
        public string MarginStr { get; set; }
        public Vector4 Margin { get; set; }
        public string GapStr { get; set; }
        public string TextAlign { get; set; } = "left";
        public string WhiteSpace { get; set; } = "normal";
        public string TextTransform { get; set; }
        public string BorderWidthStr { get; set; }
        public float BorderWidth { get; set; }
        public string BorderStyle { get; set; } = "none";
        public Vector4 BorderColor { get; set; }
        public string BoxSizing { get; set; } = "content-box";
    }
}