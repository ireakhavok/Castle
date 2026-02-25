using System.Numerics;

namespace ToolChest
{
    public enum BrushShape
    {
        Circle,
        Square,
        GaussianCircle,
        GaussianSquare
    }

    public enum BrushMode
    {
        Raise,
        Lower,
        Smooth,
        Flatten,
        Noise,
        Sharpen
    }

    public class Brush
    {
        public BrushShape Shape { get; set; } = BrushShape.GaussianCircle;
        public BrushMode Mode { get; set; } = BrushMode.Raise;
        public float Size { get; set; } = 10f;
        public float Intensity { get; set; } = 1f;
    }
}