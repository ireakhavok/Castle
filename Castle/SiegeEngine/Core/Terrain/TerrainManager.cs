// Folder: SiegeEngine/Core/Terrain
// File: TerrainManager.cs
using System;
using System.IO;

namespace SiegeEngine.Core.Terrain
{
    public static class TerrainManager
    {
        public enum TerrainType
        {
            USGSDEM,
            CustomFlat,
            Unknown
        }

        public static TerrainType DetectType(string filePath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                if (bytes.Length < 8 || bytes[0] != 'I' || bytes[1] != 'I') return TerrainType.Unknown;

                uint ifdOffset = BitConverter.ToUInt32(bytes, 4);
                ushort numEntries = BitConverter.ToUInt16(bytes, (int)ifdOffset);

                for (uint i = 0; i < numEntries; i++)
                {
                    uint entry = ifdOffset + 2 + i * 12;
                    ushort tag = BitConverter.ToUInt16(bytes, (int)entry);
                    if (tag == 65000 || tag == 65001)
                    {
                        return TerrainType.CustomFlat;
                    }
                }
                return TerrainType.USGSDEM;
            }
            catch
            {
                return TerrainType.Unknown;
            }
        }

        public static float[,] LoadTerrain(string filePath, out int width, out int height, out float minHeight, out float maxHeight, out bool isCustomFlat, out float customScaleX, out float customScaleZ)
        {
            isCustomFlat = false;
            customScaleX = 1.0f;
            customScaleZ = 1.0f;
            width = height = 0;
            minHeight = maxHeight = 0;

            TerrainType type = DetectType(filePath);

            if (type == TerrainType.CustomFlat)
            {
                isCustomFlat = true;
                TerrainParser.TryGetCustomScale(filePath, out customScaleX, out customScaleZ);
            }

            return TerrainParser.LoadUSGSDEM(filePath, out width, out height, out minHeight, out maxHeight);
        }
    }
}