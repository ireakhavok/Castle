// Folder: SiegeEngine/Core/Terrain
// File: TerrainTextureParser.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.IO;

namespace SiegeEngine.Core.Terrain
{
    public static class TerrainTextureParser
    {
        public static uint LoadColorTexture(IRenderContext renderContext, string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"[TerrainTextureParser] File not found: {path}");
                return 0;
            }

            Console.WriteLine($"[TerrainTextureParser] Loading color texture: {path}");
            var (textureId, _) = TextureLoader.LoadTexture(renderContext, path);

            if (textureId != 0)
            {
                Console.WriteLine($"[TerrainTextureParser] Successfully loaded texture ID: {textureId}");
            }
            else
            {
                Console.WriteLine($"[TerrainTextureParser] Failed to load texture");
            }

            return textureId;
        }
    }
}