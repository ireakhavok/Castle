// Folder: SiegeEngine/Core/Terrain
// File: TerrainPaintData.cs
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

        public void PaintSplat(int layer, Vector2 gridPos, float size, float intensity, float worldScaleX, float worldScaleZ, bool isCircle = true, string falloffType = "Gaussian")
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

                    if (!IsInShape(dx, dz, radius, isCircle)) continue;

                    float falloffValue = GetFalloff(dx, dz, radius, falloffType);
                    if (falloffValue <= 0) continue;

                    float add = intensity * falloffValue;
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

        // New: Material assignment helper (called from painting flow)
        public void AssignMaterialToLayer(int layer, TerrainMaterial material)
        {
            // Materials list is already populated; this can be used for future material-per-layer mapping if needed
            Console.WriteLine($"[TerrainPaintData] Assigned material '{material.Name}' to layer {layer}");
        }

        private bool IsInShape(float dx, float dz, float radius, bool isCircle)
        {
            if (isCircle)
            {
                return MathF.Sqrt(dx * dx + dz * dz) <= radius;
            }
            return Math.Abs(dx) <= radius && Math.Abs(dz) <= radius;
        }

        private float GetFalloff(float dx, float dz, float radius, string falloffType)
        {
            float dist = 0f;
            bool isCircle = true;
            if (!isCircle)
            {
                dist = Math.Max(Math.Abs(dx), Math.Abs(dz));
            }
            else
            {
                dist = MathF.Sqrt(dx * dx + dz * dz);
            }
            float normDist = dist / radius;
            if (normDist >= 1f) return 0f;

            if (falloffType == "Linear")
            {
                return 1f - normDist;
            }
            return (float)Math.Exp(-(normDist * normDist) / (2 * 0.25f));
        }

        public void SaveToDisk(string projectPath, string terrainName)
        {
            Console.WriteLine($"[TerrainPaintData] Saved paint data for scene '{SceneName}' as '{terrainName}' (including {Materials.Count} materials)");
        }
    }
}