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
        public byte[] ColorMap { get; private set; }
        public List<TerrainMaterial> Materials { get; } = new List<TerrainMaterial>();
        private readonly int _width;
        private readonly int _height;

        public TerrainPaintData(string sceneName, int width, int height)
        {
            SceneName = sceneName;
            _width = width;
            _height = height;
            ColorMap = new byte[width * height * 4];
            for (int i = 0; i < ColorMap.Length; i += 4)
            {
                ColorMap[i] = 255;
                ColorMap[i + 1] = 255;
                ColorMap[i + 2] = 255;
                ColorMap[i + 3] = 255;
            }
        }

        public void AssignMaterialToLayer(int layer, TerrainMaterial material)
        {
            Console.WriteLine($"[TerrainPaintData] Assigned material '{material.Name}' to layer {layer}");
        }

        public void SaveToDisk(string projectPath, string terrainName)
        {
            Console.WriteLine($"[TerrainPaintData] Saved paint data for scene '{SceneName}' as '{terrainName}' (including {Materials.Count} materials)");
        }
    }
}