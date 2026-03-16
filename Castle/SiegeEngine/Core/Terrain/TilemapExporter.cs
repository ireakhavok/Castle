// Folder: SiegeEngine/Core/Terrain
// File: TilemapExporter.cs
using System;
using System.IO;
using System.Numerics;
using System.Drawing;
using System.Drawing.Imaging;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;

namespace SiegeEngine.Core.Terrain
{
    public static class TilemapExporter
    {
        public static void ExportToMesh(float[,] heightmap, float thresholdGrass, float thresholdRock, string outputFbxPath, string atlasPngPath)
        {
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);
            // Generate atlas.png with simple colors for water/grass/rock
            using var atlas = new Bitmap(3, 1);
            atlas.SetPixel(0, 0, Color.Blue); // Water
            atlas.SetPixel(1, 0, Color.Green); // Grass
            atlas.SetPixel(2, 0, Color.Gray); // Rock
            atlas.Save(atlasPngPath, ImageFormat.Png);
            // Create flat mesh with UVs based on thresholds
            var meshData = new MeshData();
            for (int x = 0; x < width - 1; x++)
            {
                for (int y = 0; y < height - 1; y++)
                {
                    float h = heightmap[x, y];
                    float u = h < thresholdGrass ? 0f : h < thresholdRock ? 0.333f : 0.666f;
                    uint baseIndex = (uint)meshData.Vertices.Count;
                    meshData.Vertices.Add(new FBXVertex { Position = new Vector3(x, y, 0), UV = new Vector2(u, 0) });
                    meshData.Vertices.Add(new FBXVertex { Position = new Vector3(x + 1, y, 0), UV = new Vector2(u + 0.333f, 0) });
                    meshData.Vertices.Add(new FBXVertex { Position = new Vector3(x, y + 1, 0), UV = new Vector2(u, 1) });
                    meshData.Vertices.Add(new FBXVertex { Position = new Vector3(x + 1, y + 1, 0), UV = new Vector2(u + 0.333f, 1) });
                    meshData.Indices.Add(baseIndex);
                    meshData.Indices.Add(baseIndex + 1);
                    meshData.Indices.Add(baseIndex + 2);
                    meshData.Indices.Add(baseIndex + 1);
                    meshData.Indices.Add(baseIndex + 3);
                    meshData.Indices.Add(baseIndex + 2);
                }
            }
            // Export to FBX
            var fbxModel = new FBXModel();
            fbxModel.Meshes.Add(meshData);
            FBXParser.Export(fbxModel, outputFbxPath);
            Console.WriteLine($"Exported tilemap mesh to {outputFbxPath} with atlas {atlasPngPath}");
        }
    }
}