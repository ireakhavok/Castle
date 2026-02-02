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

        public static int[] ParseRawArrayAsInt(byte[] raw)
        {
            if (raw.Length < 12)
            {
                Console.WriteLine("Raw too short for array header");
                return null;
            }
            using (MemoryStream ms = new MemoryStream(raw))
            using (BinaryReader r = new BinaryReader(ms))
            {
                uint arrayLen = r.ReadUInt32();
                uint encoding = r.ReadUInt32();
                uint compressedLen = r.ReadUInt32();
                byte[] data;
                if (encoding == 0)
                {
                    if (raw.Length - 12 < arrayLen * 4)
                    {
                        Console.WriteLine("Raw size too small for uncompressed int array");
                        return null;
                    }
                    data = r.ReadBytes((int)(arrayLen * 4));
                }
                else if (encoding == 1)
                {
                    if (raw.Length - 12 < compressedLen)
                    {
                        Console.WriteLine("Raw size too small for compressed data");
                        return null;
                    }
                    byte[] compressed = r.ReadBytes((int)compressedLen);
                    data = DecompressData(compressed, (int)arrayLen * 4);
                    if (data == null) return null;
                }
                else
                {
                    Console.WriteLine($"Unknown encoding {encoding} for raw array");
                    return null;
                }
                int[] kv = new int[arrayLen];
                Buffer.BlockCopy(data, 0, kv, 0, data.Length);
                return kv;
            }
        }
        public static double[] ParseRawArrayAsDouble(byte[] raw)
        {
            if (raw.Length < 12)
            {
                Console.WriteLine("Raw too short for array header");
                return null;
            }
            using (MemoryStream ms = new MemoryStream(raw))
            using (BinaryReader r = new BinaryReader(ms))
            {
                uint arrayLen = r.ReadUInt32();
                uint encoding = r.ReadUInt32();
                uint compressedLen = r.ReadUInt32();
                byte[] data;
                if (encoding == 0)
                {
                    if (raw.Length - 12 < arrayLen * 8)
                    {
                        Console.WriteLine("Raw size too small for uncompressed double array");
                        return null;
                    }
                    data = r.ReadBytes((int)(arrayLen * 8));
                }
                else if (encoding == 1)
                {
                    if (raw.Length - 12 < compressedLen)
                    {
                        Console.WriteLine("Raw size too small for compressed data");
                        return null;
                    }
                    byte[] compressed = r.ReadBytes((int)compressedLen);
                    data = DecompressData(compressed, (int)arrayLen * 8);
                    if (data == null) return null;
                }
                else
                {
                    Console.WriteLine($"Unknown encoding {encoding} for raw array");
                    return null;
                }
                double[] kv = new double[arrayLen];
                Buffer.BlockCopy(data, 0, kv, 0, data.Length);
                return kv;
            }
        }
        public static byte[] DecompressData(byte[] compressed, int expectedLen)
        {
            try
            {
                using (var ms = new MemoryStream(compressed))
                using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                using (var decomMs = new MemoryStream())
                {
                    deflate.CopyTo(decomMs);
                    byte[] decompressed = decomMs.ToArray();
                    if (decompressed.Length != expectedLen)
                    {
                        FBXParserBase.Log($"Decompression error: Expected {expectedLen} bytes, got {decompressed.Length}");
                        return null;
                    }
                    return decompressed;
                }
            }
            catch (Exception ex)
            {
                FBXParserBase.Log($"Decompression exception: {ex.Message}");
                return null;
            }
        }
        public static float GetValueAtTime(long[] times, float[] values, long time, float defaultVal)
        {
            if (times == null || times.Length == 0 || values == null || values.Length == 0) return defaultVal;
            int len = Math.Min(times.Length, values.Length);
            int idx = Array.BinarySearch(times, 0, len, time);
            if (idx >= 0) return values[idx];
            idx = ~idx;
            if (idx == 0) return values[0];
            if (idx == len) return values[len - 1];
            long t0 = times[idx - 1];
            long t1 = times[idx];
            float v0 = values[idx - 1];
            float v1 = values[idx];
            float factor = (float)(time - t0) / (t1 - t0);
            return v0 + factor * (v1 - v0);
        }
        public static float[] ParseKeyValues(BaseNode keyValueNode, int expectedLength)
        {
            if (keyValueNode == null) return null;
            var prop = keyValueNode.properties[0];
            char typeCode = prop.TypeCode;
            float[] keyValues = null;
            if (typeCode == 'f')
            {
                keyValues = (float[])prop.Value;
            }
            else if (typeCode == 'd')
            {
                double[] dvals = (double[])prop.Value;
                keyValues = dvals.Select(d => (float)d).ToArray();
            }
            else if (typeCode == 'R')
            {
                byte[] raw = (byte[])prop.Value;
                if (raw.Length % 4 == 0)
                {
                    int actualLength = raw.Length / 4;
                    keyValues = new float[actualLength];
                    Buffer.BlockCopy(raw, 0, keyValues, 0, raw.Length);
                    if (actualLength != expectedLength)
                        Console.WriteLine($"Warning: Key value length {actualLength} vs expected {expectedLength}, using {actualLength}");
                }
                else if (raw.Length % 8 == 0)
                {
                    int actualLength = raw.Length / 8;
                    double[] dvals = new double[actualLength];
                    Buffer.BlockCopy(raw, 0, dvals, 0, raw.Length);
                    keyValues = dvals.Select(d => (float)d).ToArray();
                    if (actualLength != expectedLength)
                        Console.WriteLine($"Warning: Key value length {actualLength} vs expected {expectedLength}, using {actualLength}");
                }
                else
                {
                    Console.WriteLine($"Unexpected raw length {raw.Length} for expected {expectedLength}, skipping");
                    return null;
                }
            }
            else
            {
                Console.WriteLine($"Unexpected type for KeyValueFloat: {typeCode}");
                return null;
            }
            return keyValues;
        }
        public static float GetPropertyFloat(object value)
        {
            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is int i) return i;
            if (value is long l) return (float)l;
            if (value is string s) return float.Parse(s, CultureInfo.InvariantCulture);
            throw new FormatException($"Invalid property value type for float: {value?.GetType()}");
        }
        public static int GetPropertyInt(object value)
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is float f) return (int)f;
            if (value is double d) return (int)d;
            if (value is string s) return int.Parse(s, CultureInfo.InvariantCulture);
            throw new FormatException($"Invalid property value type for int: {value?.GetType()}");
        }
        public static double GetPropertyDouble(object value)
        {
            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is string s) return double.Parse(s, CultureInfo.InvariantCulture);
            throw new FormatException($"Invalid property value type for double: {value?.GetType()}");
        }
    }
}