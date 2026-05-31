// Folder: ToolChest
// File: Brush.cs
using System;
using System.Numerics;

namespace ToolChest
{
    public enum BrushShape { Circle, Square }
    public enum BrushFalloff { Linear, Gaussian }
    public enum BrushMode { Raise, Lower, Smooth, Flatten, Noise, Sharpen, Paint }

    public class Brush
    {
        public BrushShape Shape { get; set; } = BrushShape.Circle;
        public BrushFalloff Falloff { get; set; } = BrushFalloff.Gaussian;
        public BrushMode Mode { get; set; } = BrushMode.Raise;
        public int PaintLayer { get; set; } = 0;
        public float Size { get; set; } = 10f;
        public float Intensity { get; set; } = 1f;
        public string MaterialPath { get; set; } = string.Empty;

        public void Apply(ref float[,] heightmap, Vector2 gridPos, float worldScaleX, float worldScaleZ)
        {
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);
            int centerX = (int)Math.Clamp(gridPos.X / worldScaleX, 0, width - 1);
            int centerZ = (int)Math.Clamp(gridPos.Y / worldScaleZ, 0, height - 1);
            float radiusInCells = Size / Math.Max(worldScaleX, worldScaleZ);
            int minX = Math.Max(0, centerX - (int)radiusInCells - 1);
            int maxX = Math.Min(width, centerX + (int)radiusInCells + 1);
            int minZ = Math.Max(0, centerZ - (int)radiusInCells - 1);
            int maxZ = Math.Min(height, centerZ + (int)radiusInCells + 1);
            Random rand = new Random();

            if (Mode == BrushMode.Paint)
            {
                return; // painting is now handled directly in TerrainCreatorScene
            }

            if (Mode == BrushMode.Flatten || Mode == BrushMode.Smooth || Mode == BrushMode.Sharpen)
            {
                float avgHeight = 0f;
                int count = 0;
                for (int x = minX; x < maxX; x++)
                {
                    for (int z = minZ; z < maxZ; z++)
                    {
                        avgHeight += heightmap[x, z];
                        count++;
                    }
                }
                avgHeight /= count;
                for (int x = minX; x < maxX; x++)
                {
                    for (int z = minZ; z < maxZ; z++)
                    {
                        float dx = x - centerX;
                        float dz = z - centerZ;
                        if (!IsInShape(dx, dz, radiusInCells)) continue;
                        float falloff = GetFalloff(dx, dz, radiusInCells);
                        float current = heightmap[x, z];
                        float delta = 0f;
                        if (Mode == BrushMode.Flatten)
                        {
                            delta = (avgHeight - current) * Intensity * falloff;
                        }
                        else if (Mode == BrushMode.Smooth)
                        {
                            float neighborAvg = GetNeighborAverage(heightmap, x, z, width, height);
                            delta = (neighborAvg - current) * Intensity * falloff * 0.5f;
                        }
                        else if (Mode == BrushMode.Sharpen)
                        {
                            float neighborAvg = GetNeighborAverage(heightmap, x, z, width, height);
                            delta = (current - neighborAvg) * Intensity * falloff;
                        }
                        heightmap[x, z] += delta;
                    }
                }
            }
            else
            {
                for (int x = minX; x < maxX; x++)
                {
                    for (int z = minZ; z < maxZ; z++)
                    {
                        float dx = x - centerX;
                        float dz = z - centerZ;
                        if (!IsInShape(dx, dz, radiusInCells)) continue;
                        float falloff = GetFalloff(dx, dz, radiusInCells);
                        float delta = Intensity * falloff;
                        if (Mode == BrushMode.Raise) heightmap[x, z] += delta;
                        else if (Mode == BrushMode.Lower) heightmap[x, z] -= delta;
                        else if (Mode == BrushMode.Noise) heightmap[x, z] += (float)(rand.NextDouble() * 2 - 1) * delta;
                    }
                }
            }
        }

        private bool IsInShape(float dx, float dz, float radius)
        {
            switch (Shape)
            {
                case BrushShape.Circle: return MathF.Sqrt(dx * dx + dz * dz) <= radius;
                case BrushShape.Square: return Math.Abs(dx) <= radius && Math.Abs(dz) <= radius;
                default: return false;
            }
        }

        private float GetFalloff(float dx, float dz, float radius)
        {
            float dist = 0f;
            switch (Shape)
            {
                case BrushShape.Circle: dist = MathF.Sqrt(dx * dx + dz * dz); break;
                case BrushShape.Square: dist = Math.Max(Math.Abs(dx), Math.Abs(dz)); break;
            }
            float normDist = dist / radius;
            if (normDist >= 1f) return 0f;
            switch (Falloff)
            {
                case BrushFalloff.Linear: return 1f - normDist;
                case BrushFalloff.Gaussian: return (float)Math.Exp(-(normDist * normDist) / (2 * 0.25f));
                default: return 1f;
            }
        }

        private float GetNeighborAverage(float[,] heightmap, int x, int z, int width, int height)
        {
            float sum = 0f;
            int count = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = x + dx;
                    int nz = z + dz;
                    if (nx >= 0 && nx < width && nz >= 0 && nz < height)
                    {
                        sum += heightmap[nx, nz];
                        count++;
                    }
                }
            }
            return count > 0 ? sum / count : heightmap[x, z];
        }
    }
}