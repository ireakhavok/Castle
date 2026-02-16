// Folder: SiegeEngine.Core
// File: AssetParsing.V2/NodeParser.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using SiegeEngine.Core.AssetObjects;

namespace SiegeEngine.Core.AssetParsing
{
    public static class NodeParser
    {
        public static void ParseNodes(BinaryReader reader, uint version, long fileLength, long parentEndOffset, FBXNode parentNode, FBXFileForest forest)
        {
            int nullRecordSize = version >= 7500 ? 25 : 13;
            while (reader.BaseStream.Position < parentEndOffset && reader.BaseStream.Position < fileLength)
            {
                if (reader.BaseStream.Position + nullRecordSize > fileLength)
                    break;
                long nodeStart = reader.BaseStream.Position;
                try
                {
                    long endOffset;
                    long numProperties;
                    long propertyListLen;
                    if (version >= 7500)
                    {
                        endOffset = (long)reader.ReadUInt64();
                        numProperties = (long)reader.ReadUInt64();
                        propertyListLen = (long)reader.ReadUInt64();
                    }
                    else
                    {
                        endOffset = reader.ReadUInt32();
                        numProperties = reader.ReadUInt32();
                        propertyListLen = reader.ReadUInt32();
                    }
                    byte tempNameLen = reader.ReadByte();
                    string tempName = "";
                    if (tempNameLen > 0 && tempNameLen <= 255 && reader.BaseStream.Position + tempNameLen <= reader.BaseStream.Length)
                    {
                        byte[] nameBytes = reader.ReadBytes(tempNameLen);
                        tempName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0').Split('\0')[0];
                    }
                    if (endOffset == 0 && numProperties == 0 && propertyListLen == 0 && tempNameLen == 0)
                    {
                        if (parentNode == null)
                        {
                            Console.WriteLine("End of content found, hex dump disabled.");
                            break;
                        }
                        else
                        {
                            reader.BaseStream.Seek(nodeStart + nullRecordSize, SeekOrigin.Begin);
                            continue;
                        }
                    }
                    string nodeName = tempName;
                    long propStart = reader.BaseStream.Position;
                    long propEnd = propStart + propertyListLen;
                    FBXNode newNode = new FBXNode
                    {
                        endOffset = endOffset,
                        numProperties = numProperties,
                        propertyListLen = propertyListLen,
                        nameLen = tempNameLen,
                        Name = nodeName,
                        Parent = parentNode
                    };
                    if (parentNode != null)
                    {
                        parentNode.children.Add(newNode);
                    }
                    else
                    {
                        forest.TreeList.Add(newNode);
                    }
                    ParseProperties(reader, newNode, propStart, propEnd, numProperties, version);
                    if (endOffset != 0 && endOffset <= fileLength && reader.BaseStream.Position < endOffset)
                    {
                        ParseNodes(reader, version, fileLength, endOffset, newNode, forest);
                    }
                    if (reader.BaseStream.Position != endOffset)
                    {
                        reader.BaseStream.Seek(endOffset, SeekOrigin.Begin);
                    }
                }
                catch (Exception ex)
                {
                    FBXParserBase.Log($"FBXParser: Error reading node at position {nodeStart}: {ex.Message}");
                    reader.BaseStream.Seek(nodeStart + nullRecordSize, SeekOrigin.Begin);
                    continue;
                }
            }
        }
        private static void ParseProperties(BinaryReader reader, FBXNode node, long propStart, long propEnd, long numProperties, uint version)
        {
            reader.BaseStream.Seek(propStart, SeekOrigin.Begin);
            for (long i = 0; i < numProperties && reader.BaseStream.Position < propEnd; i++)
            {
                PropertyNode prop = ParseProperty(reader);
                if (prop != null)
                {
                    node.properties.Add(prop);
                }
                else
                {
                    // Removed the Console.WriteLine("pause");
                }
            }
            reader.BaseStream.Seek(propEnd, SeekOrigin.Begin);
        }
        private static PropertyNode ParseProperty(BinaryReader reader)
        {
            long startPos = reader.BaseStream.Position;
            try
            {
                char typeCode = reader.ReadChar();
                PropertyNode prop = new PropertyNode { TypeCode = typeCode };
                switch (typeCode)
                {
                    case 'C':
                        prop.Value = reader.ReadByte() != 0;
                        break;
                    case 'Y':
                    case 'H':
                        prop.Value = reader.ReadInt16();
                        break;
                    case 'I':
                        prop.Value = reader.ReadInt32();
                        break;
                    case 'F':
                        prop.Value = reader.ReadSingle();
                        break;
                    case 'D':
                        prop.Value = reader.ReadDouble();
                        break;
                    case 'L':
                        prop.Value = reader.ReadInt64();
                        break;
                    case 'K':
                        prop.Value = reader.ReadBytes(8);
                        break;
                    case 'S':
                    case 'R':
                    case 'a':
                    case 'n':
                    case 's':
                    case 'r':
                        uint len = reader.ReadUInt32();
                        long maxLen = reader.BaseStream.Length - reader.BaseStream.Position;
                        if (len >= 0 && len <= maxLen && len < int.MaxValue)
                        {
                            byte[] data = reader.ReadBytes((int)len);
                            if (typeCode == 'S')
                            {
                                prop.Value = Encoding.ASCII.GetString(data).TrimEnd('\0');
                            }
                            else
                            {
                                prop.Value = data;
                            }
                        }
                        else
                        {
                            FBXParserBase.Log($"Property parse error: Invalid length {len} at {startPos}");
                            return null;
                        }
                        break;
                    case 'f':
                    case 'd':
                    case 'l':
                    case 'i':
                    case 'b':
                        uint arrayLen = reader.ReadUInt32();
                        uint encoding = reader.ReadUInt32();
                        uint compressedLen = reader.ReadUInt32();
                        long dataLen = encoding == 0 ? arrayLen * GetTypeSize(typeCode) : compressedLen;
                        long maxDataLen = reader.BaseStream.Length - reader.BaseStream.Position;
                        if (dataLen < 0 || dataLen > maxDataLen || dataLen > int.MaxValue)
                        {
                            FBXParserBase.Log($"Property parse error: Invalid dataLen {dataLen} at {startPos}");
                            return null;
                        }
                        byte[] rawData;
                        if (encoding == 1)
                        {
                            byte[] compressed = reader.ReadBytes((int)compressedLen);
                            rawData = DecompressData(compressed, (int)arrayLen * GetTypeSize(typeCode));
                            if (rawData == null)
                            {
                                return null;
                            }
                        }
                        else
                        {
                            rawData = reader.ReadBytes((int)dataLen);
                        }
                        prop.Value = ConvertRawToArray(rawData, typeCode, arrayLen);
                        break;
                    case 'c':
                        prop.Value = reader.ReadBytes(12);
                        break;
                    case 'e':
                        prop.Value = reader.ReadInt32();
                        break;
                    case 'p':
                        uint propLen = reader.ReadUInt32();
                        long maxPropLen = reader.BaseStream.Length - reader.BaseStream.Position;
                        if (propLen >= 0 && propLen <= maxPropLen && propLen < int.MaxValue)
                        {
                            prop.Value = reader.ReadBytes((int)propLen);
                        }
                        else
                        {
                            FBXParserBase.Log($"Property parse error: Invalid propLen {propLen} at {startPos}");
                            return null;
                        }
                        break;
                    case 'T':
                    case 'U':
                    case 'V':
                        prop.Value = reader.ReadBytes(24);
                        break;
                    default:
                        FBXParserBase.Log($"Unknown property type '{typeCode}' at {startPos}, skipping");
                        return null;
                }
                return prop;
            }
            catch (Exception ex)
            {
                FBXParserBase.Log($"Property parse exception at {startPos}: {ex.Message}");
                return null;
            }
        }
        private static int GetTypeSize(char typeCode)
        {
            switch (typeCode)
            {
                case 'f':
                case 'i':
                case 'b': return 4;
                case 'd':
                case 'l': return 8;
                default: return 0;
            }
        }
        private static byte[] DecompressData(byte[] compressed, int expectedLen)
        {
            try
            {
                using (var ms = new MemoryStream(compressed))
                {
                    // Check for zlib header
                    if (compressed.Length >= 2 && compressed[0] == 0x78 && (compressed[1] == 0x01 || compressed[1] == 0x5E || compressed[1] == 0x9C || compressed[1] == 0xDA))
                    {
                        ms.Seek(2, SeekOrigin.Begin); // Skip zlib header
                    }
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
            }
            catch (Exception ex)
            {
                FBXParserBase.Log($"Decompression exception: {ex.Message}");
                return null;
            }
        }
        private static object ConvertRawToArray(byte[] rawData, char typeCode, uint arrayLen)
        {
            switch (typeCode)
            {
                case 'f':
                    float[] fArr = new float[arrayLen];
                    Buffer.BlockCopy(rawData, 0, fArr, 0, (int)arrayLen * 4);
                    return fArr;
                case 'd':
                    double[] dArr = new double[arrayLen];
                    Buffer.BlockCopy(rawData, 0, dArr, 0, (int)arrayLen * 8);
                    return dArr;
                case 'l':
                    long[] lArr = new long[arrayLen];
                    Buffer.BlockCopy(rawData, 0, lArr, 0, (int)arrayLen * 8);
                    return lArr;
                case 'i':
                    int[] iArr = new int[arrayLen];
                    Buffer.BlockCopy(rawData, 0, iArr, 0, (int)arrayLen * 4);
                    return iArr;
                case 'b':
                    bool[] bArr = new bool[arrayLen];
                    for (uint k = 0; k < arrayLen; k++)
                    {
                        bArr[k] = rawData[k] != 0;
                    }
                    return bArr;
                default:
                    return null;
            }
        }
    }
}