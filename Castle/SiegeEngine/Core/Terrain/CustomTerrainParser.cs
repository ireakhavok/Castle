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
            ushort numEntries = 12;
            bw.Write(numEntries);
            uint dataOffset = 8 + (uint)(numEntries * 12 + 4);
            WriteTiffTag(bw, 256, 4, 1, (uint)width);
            WriteTiffTag(bw, 257, 4, 1, (uint)height);
            WriteTiffTag(bw, 258, 3, 1, 32);
            WriteTiffTag(bw, 259, 3, 1, 1);
            WriteTiffTag(bw, 262, 3, 1, 1);
            WriteTiffTag(bw, 277, 3, 1, 1);
            WriteTiffTag(bw, 278, 4, 1, (uint)height);
            WriteTiffTag(bw, 273, 4, 1, dataOffset);
            WriteTiffTag(bw, 279, 4, 1, (uint)(width * height * 4));
            WriteTiffTag(bw, 339, 3, 1, 3);
            WriteTiffTag(bw, 65000, 11, 1, dataOffset + 8); // float scaleX
            WriteTiffTag(bw, 65001, 11, 1, dataOffset + 12); // float scaleZ
            bw.Write((uint)0);
            bw.Write(worldScaleX);
            bw.Write(worldScaleZ);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    bw.Write(heightmap[x, y]);
            Console.WriteLine($"[CustomTerrainParser] Saved {width}x{height} custom flat 32-bit float TIFF @ {worldScaleX:F2}m/cell X {worldScaleZ:F2}m/cell");
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
            return LoadHeightmapOnly(filePath, out width, out height, out minHeight, out maxHeight);
        }
        private static float[,] LoadHeightmapOnly(string filePath, out int width, out int height, out float minHeight, out float maxHeight)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < 8 || bytes[0] != 'I' || bytes[1] != 'I' || BitConverter.ToUInt16(bytes, 2) != 42)
                throw new Exception("Not a valid TIFF file");
            uint ifdOffset = BitConverter.ToUInt32(bytes, 4);
            ushort numEntries = BitConverter.ToUInt16(bytes, (int)ifdOffset);
            width = 0;
            height = 0;
            uint stripOffset = 0;
            for (uint i = 0; i < numEntries; i++)
            {
                uint entry = ifdOffset + 2 + i * 12;
                ushort tag = BitConverter.ToUInt16(bytes, (int)entry);
                ushort type = BitConverter.ToUInt16(bytes, (int)entry + 2);
                uint count = BitConverter.ToUInt32(bytes, (int)entry + 4);
                uint valueOrOffset = BitConverter.ToUInt32(bytes, (int)entry + 8);
                if (tag == 256) width = (int)(type == 3 ? BitConverter.ToUInt16(bytes, (int)entry + 8) : valueOrOffset);
                if (tag == 257) height = (int)(type == 3 ? BitConverter.ToUInt16(bytes, (int)entry + 8) : valueOrOffset);
                if (tag == 273) stripOffset = valueOrOffset;
            }
            if (width == 0 || height == 0 || stripOffset == 0)
                throw new Exception("Invalid custom TIFF structure");
            long expectedDataEnd = (long)stripOffset + (long)width * height * 4;
            if (expectedDataEnd > bytes.Length)
                throw new Exception($"Data offset {stripOffset} exceeds file size {bytes.Length} (expected data size {width * height * 4})");
            float[,] heightmap = new float[width, height];
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;
            int idx = (int)stripOffset;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = BitConverter.ToSingle(bytes, idx);
                    idx += 4;
                    if (float.IsNaN(h) || float.IsInfinity(h))
                        h = 0f;
                    else
                    {
                        minHeight = Math.Min(minHeight, h);
                        maxHeight = Math.Max(maxHeight, h);
                    }
                    heightmap[x, y] = h;
                }
            }
            Console.WriteLine($"[CustomTerrainParser] Loaded custom flat {width}x{height} terrain. Raw Min={minHeight:F1}m, Max={maxHeight:F1}m");
            return heightmap;
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
                        float val = BitConverter.ToSingle(bytes, (int)off);
                        if (tag == 65000) scaleX = val;
                        if (tag == 65001) scaleZ = val;
                    }
                }
                return scaleX > 0.001f && scaleZ > 0.001f;
            }
            catch
            {
                return false;
            }
        }
    }
}