// Folder: SiegeEngine/Core/Terrain
// File: TerrainTextureParser.cs
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using System;
using System.Drawing;
using System.IO;

namespace SiegeEngine.Core.Terrain
{
    public static class TerrainTextureParser
    {
        public static GpuHandle LoadColorTexture(IRenderContext renderContext, string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Console.WriteLine($"[TerrainTextureParser] File not found or null path: {path}");
                return default;
            }

            Console.WriteLine($"[TerrainTextureParser] Loading color texture: {path}");
            var (textureId, _) = TextureLoader.LoadTexture(renderContext, path);

            if (textureId.IsValid)
            {
                Console.WriteLine($"[TerrainTextureParser] Successfully loaded texture ID: {textureId.Id}");
            }
            else
            {
                Console.WriteLine($"[TerrainTextureParser] Failed to load texture");
            }

            return textureId;
        }

        // Creates a GPU texture from an in-memory Bitmap (used for lazy color layer creation during paint)
        // The bitmap is now high-res (4096x4096) for native PNG quality
        public static GpuHandle CreateColorTexture(IRenderContext renderContext, Bitmap bitmap)
        {
            if (bitmap == null) return default;
            Console.WriteLine($"[TerrainTextureParser] CreateColorTexture from Bitmap {bitmap.Width}x{bitmap.Height}");
            // Use normal Linear + mipmap filtering (smooth native quality like 2D sprites)
            var (textureId, _) = TextureLoader.LoadTextureFromBitmap(renderContext, bitmap, crispPaintMode: false);
            Console.WriteLine($"[TerrainTextureParser] Created color texture ID: {textureId.Id}");
            return textureId;
        }
    }
}
