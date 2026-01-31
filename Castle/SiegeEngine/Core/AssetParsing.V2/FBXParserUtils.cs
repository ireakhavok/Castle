// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXParserUtils.cs
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SiegeEngine.Core.AssetObjects;
using System.Numerics;


namespace SiegeEngine.Core.AssetParsing.V2
{
    public static class FBXParserUtils
    {
        public static Matrix4x4 CreateMatrixFromArray_LoadFromColumnMajor(double[] vals)
        {
            return new Matrix4x4(
                (float)vals[0], (float)vals[4], (float)vals[8], (float)vals[12],
                (float)vals[1], (float)vals[5], (float)vals[9], (float)vals[13],
                (float)vals[2], (float)vals[6], (float)vals[10], (float)vals[14],
                (float)vals[3], (float)vals[7], (float)vals[11], (float)vals[15]);
        }
        public static Matrix4x4 CreateMatrixFromArray(double[] vals)
        {
            return new Matrix4x4(
                (float)vals[0], (float)vals[1], (float)vals[2], (float)vals[3],
                (float)vals[4], (float)vals[5], (float)vals[6], (float)vals[7],
                (float)vals[8], (float)vals[9], (float)vals[10], (float)vals[11],
                (float)vals[12], (float)vals[13], (float)vals[14], (float)vals[15]);
        }
        public static void PrintMatrix(Matrix4x4 m)
        {
            FBXParserBase.Log($"({m.M11:F4}, {m.M12:F4}, {m.M13:F4}, {m.M14:F4})");
            FBXParserBase.Log($"({m.M21:F4}, {m.M22:F4}, {m.M23:F4}, {m.M24:F4})");
            FBXParserBase.Log($"({m.M31:F4}, {m.M32:F4}, {m.M33:F4}, {m.M34:F4})");
            FBXParserBase.Log($"({m.M41:F4}, {m.M42:F4}, {m.M43:F4}, {m.M44:F4})");
        }
    }
}