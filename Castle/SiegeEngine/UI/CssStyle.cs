using System.Numerics;

namespace SiegeEngine.UI
{
    public class CssStyle
    {
        public string Position { get; set; } = "relative"; // absolute, relative
        public string LeftStr { get; set; } = "0";
        public string TopStr { get; set; } = "0";
        public string WidthStr { get; set; } = "auto";
        public string HeightStr { get; set; } = "auto";
        public string Background { get; set; } = "";
        public Vector4 BackgroundColor { get; set; } = Vector4.Zero;
        public string Color { get; set; } = "#ffffff";
        public Vector4 TextColor { get; set; } = new Vector4(1, 1, 1, 1);
        public string FontSizeStr { get; set; } = "12px";
        public float FontSize { get; set; } = 12f;
        public string Display { get; set; } = "block";
        public string FlexDirection { get; set; } = "row";
        public string AlignItems { get; set; } = "stretch";
        public string JustifyContent { get; set; } = "flex-start";
        public string PaddingStr { get; set; } = "0";
        public float Padding { get; set; } = 0;
    }
}