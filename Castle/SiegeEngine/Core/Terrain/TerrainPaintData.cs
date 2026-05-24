// NEW FILE: SiegeEngine.Core.Terrain/TerrainPaintData.cs
using SiegeEngine.Core.Definitions;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Terrain
{
    public class TerrainPaintData
    {
        public string SceneName { get; }
        public float[,,] SplatWeights { get; private set; }
        public byte[] ColorMap { get; private set; }
        public List<TerrainMaterial> Materials { get; } = new List<TerrainMaterial>();

        private readonly int _width;
        private readonly int _height;

        public TerrainPaintData(string sceneName, int width, int height)
        {
            SceneName = sceneName;
            _width = width;
            _height = height;

            SplatWeights = new float[width, height, 4];
            ColorMap = new byte[width * height * 4];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    SplatWeights[x, z, 0] = 1f;
                }
            }
            for (int i = 0; i < ColorMap.Length; i += 4)
            {
                ColorMap[i] = 255;
                ColorMap[i + 1] = 255;
                ColorMap[i + 2] = 255;
                ColorMap[i + 3] = 255;
            }

            Materials.Add(new TerrainMaterial
            {
                Name = "Default",
                AlbedoPath = "Assets/Materials/default_albedo.png",
                NormalPath = "Assets/Materials/default_normal.png",
                Roughness = 0.8f
            });
        }

        public void PaintSplat(int layer, Vector2 gridPos, float size, float intensity, float worldScaleX, float worldScaleZ)
        {
            int centerX = (int)Math.Clamp(gridPos.X / worldScaleX, 0, _width - 1);
            int centerZ = (int)Math.Clamp(gridPos.Y / worldScaleZ, 0, _height - 1);
            float radius = size / Math.Max(worldScaleX, worldScaleZ);

            for (int x = 0; x < _width; x++)
            {
                for (int z = 0; z < _height; z++)
                {
                    float dx = x - centerX;
                    float dz = z - centerZ;
                    if (dx * dx + dz * dz > radius * radius) continue;

                    float falloff = 1f - MathF.Sqrt(dx * dx + dz * dz) / radius;
                    if (falloff <= 0) continue;

                    float add = intensity * falloff;
                    SplatWeights[x, z, layer] += add;

                    float total = 0f;
                    for (int l = 0; l < 4; l++) total += SplatWeights[x, z, l];
                    if (total > 0)
                    {
                        for (int l = 0; l < 4; l++) SplatWeights[x, z, l] /= total;
                    }
                }
            }
        }

        public void PaintColor(Vector2 gridPos, byte r, byte g, byte b, byte a, float size, float intensity, float worldScaleX, float worldScaleZ)
        {
            int centerX = (int)Math.Clamp(gridPos.X / worldScaleX, 0, _width - 1);
            int centerZ = (int)Math.Clamp(gridPos.Y / worldScaleZ, 0, _height - 1);
            float radius = size / Math.Max(worldScaleX, worldScaleZ);

            for (int x = 0; x < _width; x++)
            {
                for (int z = 0; z < _height; z++)
                {
                    float dx = x - centerX;
                    float dz = z - centerZ;
                    if (dx * dx + dz * dz > radius * radius) continue;

                    float falloff = intensity * (1f - MathF.Sqrt(dx * dx + dz * dz) / radius);
                    if (falloff <= 0) continue;

                    int idx = (x + z * _width) * 4;
                    ColorMap[idx] = (byte)Math.Clamp(ColorMap[idx] * (1 - falloff) + r * falloff, 0, 255);
                    ColorMap[idx + 1] = (byte)Math.Clamp(ColorMap[idx + 1] * (1 - falloff) + g * falloff, 0, 255);
                    ColorMap[idx + 2] = (byte)Math.Clamp(ColorMap[idx + 2] * (1 - falloff) + b * falloff, 0, 255);
                    ColorMap[idx + 3] = (byte)Math.Clamp(ColorMap[idx + 3] * (1 - falloff) + a * falloff, 0, 255);
                }
            }
        }

        public void SaveToDisk(string projectPath, string terrainName)
        {
        }
    }
}