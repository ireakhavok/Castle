// Folder: ToolChest
// File: Brush.cs
using System;
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
        public void Apply(ref float[,] heightmap, Vector2 gridPos, float worldScaleX, float worldScaleZ)
        {
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);
            int centerX = (int)Math.Clamp(gridPos.X / worldScaleX, 0, width - 1);
            int centerZ = (int)Math.Clamp(gridPos.Y / worldScaleZ, 0, height - 1);
            float radiusInCells = Size / Math.Max(worldScaleX, worldScaleZ);
            for (int x = Math.Max(0, centerX - (int)radiusInCells - 1); x < Math.Min(width, centerX + (int)radiusInCells + 1); x++)
            {
                for (int z = Math.Max(0, centerZ - (int)radiusInCells - 1); z < Math.Min(height, centerZ + (int)radiusInCells + 1); z++)
                {
                    float dx = x - centerX;
                    float dz = z - centerZ;
                    float dist = MathF.Sqrt(dx * dx + dz * dz);
                    if (dist > radiusInCells) continue;
                    float falloff = 1f - (dist / radiusInCells);
                    float delta = Intensity * falloff * 1f; // even stronger
                    if (Mode == BrushMode.Raise)
                        heightmap[x, z] += delta;
                    else if (Mode == BrushMode.Lower)
                        heightmap[x, z] -= delta;
                }
            }
        }
    }
}