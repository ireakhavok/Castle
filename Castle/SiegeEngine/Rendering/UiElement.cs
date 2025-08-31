using System.Drawing;
using System.Numerics;

namespace SiegeEngine.Rendering
{
    public class UiElement
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public Rectangle Bounds { get; set; }
        public Vector4 Color { get; set; }
        public bool IsButton { get; set; }
    }
}