// Folder: SiegeEngine.Core.Terrain
// File: TerrainParser.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SiegeEngine.Core.Terrain
{
    public static class TerrainParser
    {
        public static float[,] LoadUSGSDEM(string filePath, out int width, out int height, out float minHeight, out float maxHeight)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("TIFF file not found", filePath);

            using var bmp = new Bitmap(filePath);
            width = bmp.Width;
            height = bmp.Height;

            float[,] heightmap = new float[width, height];
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;

            var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bmp.PixelFormat);

            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = y * bmpData.Stride + x * 2; // USGS DEMs are typically 16-bit grayscale

                        ushort raw = (ushort)(ptr[offset] | (ptr[offset + 1] << 8));

                        // USGS 1/3 arc-second DEMs often use signed 16-bit with NoData = -9999 or similar
                        short elevation = (short)raw;
                        float h = (elevation == -9999) ? 0f : elevation;

                        heightmap[x, y] = h;

                        if (h < minHeight) minHeight = h;
                        if (h > maxHeight) maxHeight = h;
                    }
                }
            }

            bmp.UnlockBits(bmpData);

            Console.WriteLine($"[TerrainParser] Loaded {width}x{height} USGS DEM. Raw Min={minHeight:F1}m, Max={maxHeight:F1}m");
            return heightmap;
        }
    }
}