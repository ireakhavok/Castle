// Folder: SiegeEngine/Core/Terrain
// File: CustomTerrainParser.cs
using System;
using System.IO;
namespace SiegeEngine.Core.Terrain
{
    public static class CustomTerrainParser
    {
        public static void SaveFloatTiff(string path, float[,] heightmap, float worldScaleX, float worldScaleZ)
        {
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);
            bw.Write((ushort)0x4949);
            bw.Write((ushort)42);
            bw.Write((uint)8);
            ushort numEntries = 14;
            bw.Write(numEntries);
            uint scaleDataOffset = 8 + (uint)(numEntries * 12 + 4);
            uint imageDataOffset = scaleDataOffset + 16;
            WriteTiffTag(bw, 256, 4, 1, (uint)width);
            WriteTiffTag(bw, 257, 4, 1, (uint)height);
            WriteTiffTag(bw, 258, 3, 1, 32);
            WriteTiffTag(bw, 259, 3, 1, 1);
            WriteTiffTag(bw, 262, 3, 1, 1);
            WriteTiffTag(bw, 277, 3, 1, 1);
            WriteTiffTag(bw, 278, 4, 1, (uint)height);
            WriteTiffTag(bw, 273, 4, 1, imageDataOffset);
            WriteTiffTag(bw, 279, 4, 1, (uint)(width * height * 4));
            WriteTiffTag(bw, 339, 3, 1, 3);
            WriteTiffTag(bw, 65000, 12, 1, scaleDataOffset);
            WriteTiffTag(bw, 65001, 12, 1, scaleDataOffset + 8);
            bw.Write((uint)0);
            bw.Write((double)worldScaleX);
            bw.Write((double)worldScaleZ);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    bw.Write(heightmap[x, y]);
            Console.WriteLine($"[CustomTerrainParser] Saved {width}x{height} custom flat 32-bit float TIFF @ {worldScaleX:F2}m/cell (private tags only - no geo tags, exact round-trip grid)");
        }
        private static void WriteTiffTag(BinaryWriter bw, ushort tag, ushort type, uint count, uint value)
        {
            bw.Write(tag);
            bw.Write(type);
            bw.Write(count);
            bw.Write(value);
        }
        public static float[,] Load(string filePath, out int width, out int height, out float minHeight, out float maxHeight, out float customScaleX, out float customScaleZ)
        {
            customScaleX = 1.0f;
            customScaleZ = 1.0f;
            TryGetCustomScale(filePath, out customScaleX, out customScaleZ);
            return GeoTiffParser.LoadUSGSDEM(filePath, out width, out height, out minHeight, out maxHeight);
        }
        public static bool TryGetCustomScale(string filePath, out float scaleX, out float scaleZ)
        {
            scaleX = 1.0f;
            scaleZ = 1.0f;
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                if (bytes.Length < 8 || bytes[0] != 'I' || bytes[1] != 'I') return false;
                uint ifdOffset = BitConverter.ToUInt32(bytes, 4);
                ushort numEntries = BitConverter.ToUInt16(bytes, (int)ifdOffset);
                for (uint i = 0; i < numEntries; i++)
                {
                    uint entry = ifdOffset + 2 + i * 12;
                    ushort tag = BitConverter.ToUInt16(bytes, (int)entry);
                    if (tag == 65000 || tag == 65001)
                    {
                        uint off = BitConverter.ToUInt32(bytes, (int)entry + 8);
                        double val = BitConverter.ToDouble(bytes, (int)off);
                        if (tag == 65000) scaleX = (float)val;
                        if (tag == 65001) scaleZ = (float)val;
                    }
                }
                return scaleX > 0 && scaleZ > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}