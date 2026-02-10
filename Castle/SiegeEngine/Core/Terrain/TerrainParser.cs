using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace SiegeEngine.Core.Terrain
{
    public class TiffTag
    {
        public ushort Tag { get; set; }
        public ushort Type { get; set; }
        public uint Count { get; set; }
        public uint ValueOrOffset { get; set; }
        public object Value { get; set; } // Resolved value if multi or offset
    }

    public class TiffIFD
    {
        public uint Offset { get; set; }
        public List<TiffTag> Tags { get; set; } = new List<TiffTag>();
    }

    public class TiffFile
    {
        public byte[] Bytes { get; set; }
        public TiffIFD Ifd { get; set; }
        public uint ImageWidth { get; set; }
        public uint ImageHeight { get; set; }
        public uint BitsPerSample { get; set; }
        public uint Compression { get; set; }
        public uint SampleFormat { get; set; }
        public uint RowsPerStrip { get; set; }
        public uint TileWidth { get; set; }
        public uint TileLength { get; set; }
        public uint Predictor { get; set; }
        public uint FillOrder { get; set; } = 1; // Default
        public List<uint> BlockOffsets { get; set; } = new List<uint>();
        public List<uint> BlockByteCounts { get; set; } = new List<uint>();
        public ushort BlockOffsetsType { get; set; }
        public ushort BlockByteCountsType { get; set; }
        public uint NumBlocks { get; set; }
    }

    public static class TerrainParser
    {
        private class LzwDecompressor
        {
            private byte[] input;
            private int position = 0;
            private ulong bitBuffer = 0UL;
            private int bitsInBuffer = 0;
            private Dictionary<int, List<byte>> dictionary = new Dictionary<int, List<byte>>();
            private int codeSize = 9;
            private int clearCode = 256;
            private int eoiCode = 257;

            public LzwDecompressor(byte[] compressedData)
            {
                input = compressedData;
            }

            private int GetNextCode()
            {
                while (bitsInBuffer < codeSize)
                {
                    if (position >= input.Length) return eoiCode;
                    bitBuffer = (bitBuffer << 8) | input[position++];
                    bitsInBuffer += 8;
                }
                int code = (int)(bitBuffer >> (bitsInBuffer - codeSize));
                bitsInBuffer -= codeSize;
                bitBuffer &= ((1UL << bitsInBuffer) - 1UL);
                return code;
            }

            public byte[] Decompress(bool isFirstBlock)
            {
                List<byte> output = new List<byte>();
                dictionary.Clear();
                for (int i = 0; i < 256; i++)
                {
                    dictionary[i] = new List<byte> { (byte)i };
                }
                int oldCode = -1;
                int nextCode = 258;
                codeSize = 9;
                List<int> codes = new List<int>();
                int code = GetNextCode();
                codes.Add(code);
                while (code != eoiCode)
                {
                    if (code == clearCode)
                    {
                        dictionary.Clear();
                        for (int i = 0; i < 256; i++)
                        {
                            dictionary[i] = new List<byte> { (byte)i };
                        }
                        codeSize = 9;
                        nextCode = 258;
                        oldCode = -1;
                        code = GetNextCode();
                        codes.Add(code);
                        if (code == eoiCode) break;
                        output.Add((byte)code);
                        oldCode = code;
                        continue;
                    }
                    List<byte> entry;
                    if (code < 256)
                    {
                        entry = new List<byte> { (byte)code };
                    }
                    else if (dictionary.ContainsKey(code))
                    {
                        entry = dictionary[code];
                    }
                    else if (code == nextCode)
                    {
                        entry = new List<byte>(dictionary[oldCode]);
                        entry.Add(dictionary[oldCode][0]);
                    }
                    else
                    {
                        Console.WriteLine($"[TerrainParser] Invalid LZW code: {code} > {nextCode}");
                        break;
                    }
                    output.AddRange(entry);
                    if (oldCode != -1)
                    {
                        List<byte> newEntry = new List<byte>(dictionary[oldCode]);
                        newEntry.Add(entry[0]);
                        if (nextCode == (1 << codeSize) - 1 && codeSize < 12)
                        {
                            codeSize++;
                        }
                        dictionary[nextCode] = newEntry;
                        nextCode++;
                    }
                    oldCode = code;
                    code = GetNextCode();
                    codes.Add(code);
                }
                if (isFirstBlock)
                {
                    Console.WriteLine($"[TerrainParser] First 10 codes in first block: {string.Join(", ", codes.GetRange(0, Math.Min(10, codes.Count)))}");
                }
                return output.ToArray();
            }
        }

        public static float[,] LoadUSGSDEM(string filePath, out int width, out int height, out float minHeight, out float maxHeight)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("TIFF file not found", filePath);
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < 8 || bytes[0] != 'I' || bytes[1] != 'I' || BitConverter.ToUInt16(bytes, 2) != 42)
            {
                throw new Exception("Not a valid TIFF file");
            }
            TiffFile tiffFile = new TiffFile { Bytes = bytes };
            tiffFile.Ifd = new TiffIFD();
            tiffFile.Ifd.Offset = BitConverter.ToUInt32(bytes, 4);
            uint numEntries = BitConverter.ToUInt16(bytes, (int)tiffFile.Ifd.Offset);
            Console.WriteLine($"[TerrainParser] IFD at {tiffFile.Ifd.Offset}, {numEntries} entries");

            for (uint i = 0; i < numEntries; i++)
            {
                uint entryOffset = tiffFile.Ifd.Offset + 2 + i * 12;
                TiffTag tag = new TiffTag();
                tag.Tag = BitConverter.ToUInt16(bytes, (int)entryOffset);
                tag.Type = BitConverter.ToUInt16(bytes, (int)entryOffset + 2);
                tag.Count = BitConverter.ToUInt32(bytes, (int)entryOffset + 4);
                tag.ValueOrOffset = BitConverter.ToUInt32(bytes, (int)entryOffset + 8);

                if (tag.Count == 1)
                {
                    uint value = tag.Type == 3 ? (ushort)tag.ValueOrOffset : tag.ValueOrOffset;
                    tag.Value = value;
                    Console.WriteLine($"[TerrainParser] Tag {tag.Tag}: value {value} type {tag.Type} count {tag.Count}");
                }
                else
                {
                    Console.WriteLine($"[TerrainParser] Tag {tag.Tag} (multi {tag.Count}): type {tag.Type} offset {tag.ValueOrOffset}");
                    uint valSize = tag.Type switch
                    {
                        1 or 6 => 1,
                        2 => 1,
                        3 or 8 => 2,
                        4 or 9 or 11 => 4,
                        5 or 10 or 12 => 8,
                        _ => throw new Exception($"Unknown type {tag.Type}")
                    };
                    uint dataSize = valSize * tag.Count;
                    if (dataSize > bytes.Length - tag.ValueOrOffset) throw new Exception("Data out of bounds");
                    List<object> values = new List<object>();
                    for (uint j = 0; j < tag.Count; j++)
                    {
                        uint off = tag.ValueOrOffset + j * valSize;
                        object val = tag.Type switch
                        {
                            1 => bytes[off],
                            2 => (char)bytes[off],
                            3 => BitConverter.ToUInt16(bytes, (int)off),
                            4 => BitConverter.ToUInt32(bytes, (int)off),
                            5 => (BitConverter.ToUInt32(bytes, (int)off), BitConverter.ToUInt32(bytes, (int)off + 4)),
                            6 => (sbyte)bytes[off],
                            8 => BitConverter.ToInt16(bytes, (int)off),
                            9 => BitConverter.ToInt32(bytes, (int)off),
                            10 => (BitConverter.ToInt32(bytes, (int)off), BitConverter.ToInt32(bytes, (int)off + 4)),
                            11 => BitConverter.ToSingle(bytes, (int)off),
                            12 => BitConverter.ToDouble(bytes, (int)off),
                            _ => null
                        };
                        values.Add(val);
                    }
                    if (tag.Type == 2)
                    {
                        string str = new string(values.ConvertAll(c => (char)c).ToArray()).TrimEnd('\0');
                        tag.Value = str;
                    }
                    else
                    {
                        tag.Value = values;
                    }
                }

                tiffFile.Ifd.Tags.Add(tag);

                if (tag.Count == 1)
                {
                    uint tagValue = Convert.ToUInt32(tag.Value);
                    switch (tag.Tag)
                    {
                        case 256: tiffFile.ImageWidth = tagValue; break;
                        case 257: tiffFile.ImageHeight = tagValue; break;
                        case 258: tiffFile.BitsPerSample = tagValue; break;
                        case 259: tiffFile.Compression = tagValue; break;
                        case 339: tiffFile.SampleFormat = tagValue; break;
                        case 278: tiffFile.RowsPerStrip = tagValue; break;
                        case 317: tiffFile.Predictor = tagValue; break;
                        case 322: tiffFile.TileWidth = tagValue; break;
                        case 323: tiffFile.TileLength = tagValue; break;
                        case 266: tiffFile.FillOrder = tagValue; break;
                    }
                }
                else
                {
                    switch (tag.Tag)
                    {
                        case 273 or 324:
                            tiffFile.BlockOffsets = ((List<object>)tag.Value).ConvertAll(o => Convert.ToUInt32(o));
                            tiffFile.BlockOffsetsType = tag.Type;
                            tiffFile.NumBlocks = tag.Count;
                            break;
                        case 279 or 325:
                            tiffFile.BlockByteCounts = ((List<object>)tag.Value).ConvertAll(o => Convert.ToUInt32(o));
                            tiffFile.BlockByteCountsType = tag.Type;
                            break;
                    }
                }
            }

            bool isTiled = tiffFile.TileWidth > 0;
            if (tiffFile.SampleFormat != 3 || tiffFile.BitsPerSample != 32)
            {
                throw new Exception($"Unsupported format: SampleFormat={tiffFile.SampleFormat}, BitsPerSample={tiffFile.BitsPerSample}");
            }
            if (tiffFile.NumBlocks == 0)
            {
                throw new Exception("No tiles or strips found");
            }

            List<byte> fullRawData = new List<byte>((int)(tiffFile.ImageWidth * tiffFile.ImageHeight * 4));
            int expectedBlockSize = (int)(tiffFile.TileWidth * tiffFile.TileLength * (tiffFile.BitsPerSample / 8));
            for (uint i = 0; i < tiffFile.NumBlocks; i++)
            {
                uint offset = tiffFile.BlockOffsets[(int)i];
                uint byteCount = tiffFile.BlockByteCounts[(int)i];
                if (offset + byteCount > bytes.Length) throw new Exception("Data out of bounds");
                byte[] blockData = new byte[byteCount];
                Array.Copy(bytes, (int)offset, blockData, 0, (int)byteCount);
                if (i == 0)
                {
                    Console.WriteLine($"[TerrainParser] First block compressed first 4 bytes: {BitConverter.ToString(blockData, 0, Math.Min(4, blockData.Length))}");
                }
                byte[] decompressed;
                if (tiffFile.Compression == 5)
                {
                    decompressed = new LzwDecompressor(blockData).Decompress(i == 0);
                }
                else if (tiffFile.Compression == 1)
                {
                    decompressed = blockData;
                }
                else
                {
                    throw new Exception($"Unsupported compression: {tiffFile.Compression}");
                }
                Console.WriteLine($"[TerrainParser] Block {i} decompressed length: {decompressed.Length} (expected {expectedBlockSize})");
                if (decompressed.Length < expectedBlockSize)
                {
                    Array.Resize(ref decompressed, expectedBlockSize);
                }
                // Apply predictor if necessary
                if (tiffFile.Predictor == 3)
                {
                    int rowLength = isTiled ? (int)tiffFile.TileWidth * 4 : (int)tiffFile.ImageWidth * 4;
                    int numRows = isTiled ? (int)tiffFile.TileLength : (int)tiffFile.RowsPerStrip;
                    for (int r = 0; r < numRows; r++)
                    {
                        int rowOff = r * rowLength;
                        for (int c = 4; c < rowLength; c += 4)
                        {
                            float prev = BitConverter.ToSingle(decompressed, rowOff + c - 4);
                            float delta = BitConverter.ToSingle(decompressed, rowOff + c);
                            byte[] newV = BitConverter.GetBytes(prev + delta);
                            Array.Copy(newV, 0, decompressed, rowOff + c, 4);
                        }
                    }
                }
                fullRawData.AddRange(decompressed);
            }
            byte[] rawData = fullRawData.ToArray();
            width = (int)tiffFile.ImageWidth;
            height = (int)tiffFile.ImageHeight;
            float[,] heightmap = new float[width, height];
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;
            int idx = 0;
            if (isTiled)
            {
                uint numTilesX = (tiffFile.ImageWidth + tiffFile.TileWidth - 1) / tiffFile.TileWidth;
                uint numTilesY = (tiffFile.ImageHeight + tiffFile.TileLength - 1) / tiffFile.TileLength;
                int rowLength = (int)tiffFile.TileWidth * 4;
                for (uint tileY = 0; tileY < numTilesY; tileY++)
                {
                    for (uint tileX = 0; tileX < numTilesX; tileX++)
                    {
                        int thisTileW = (int)Math.Min(tiffFile.TileWidth, tiffFile.ImageWidth - tileX * tiffFile.TileWidth);
                        int thisTileH = (int)Math.Min(tiffFile.TileLength, tiffFile.ImageHeight - tileY * tiffFile.TileLength);
                        for (int row = 0; row < thisTileH; row++)
                        {
                            for (int col = 0; col < thisTileW; col++)
                            {
                                float h = BitConverter.ToSingle(rawData, idx);
                                idx += 4;
                                if (float.IsNaN(h) || h <= -999999f || h < -10000f)
                                {
                                    h = 0f;
                                }
                                else
                                {
                                    if (h < minHeight) minHeight = h;
                                    if (h > maxHeight) maxHeight = h;
                                }
                                heightmap[(int)(tileX * tiffFile.TileWidth + col), (int)(tileY * tiffFile.TileLength + row)] = h;
                            }
                            idx += (int)(tiffFile.TileWidth - thisTileW) * 4; // skip padding if any
                        }
                        idx += (int)(tiffFile.TileLength - thisTileH) * rowLength; // skip padding rows
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float h = BitConverter.ToSingle(rawData, idx);
                        idx += 4;
                        if (float.IsNaN(h) || h <= -999999f || h < -10000f)
                        {
                            h = 0f;
                        }
                        else
                        {
                            if (h < minHeight) minHeight = h;
                            if (h > maxHeight) maxHeight = h;
                        }
                        heightmap[x, y] = h;
                    }
                }
            }
            Console.WriteLine($"[TerrainParser] Loaded {width}x{height} USGS DEM. Raw Min={minHeight:F1}m, Max={maxHeight:F1}m");
            // Optional: Save as PNG for debug
            bool debugPng = true;
            if (debugPng)
            {
                string pngPath = Path.ChangeExtension(filePath, ".png");
                using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                float range = maxHeight - minHeight;
                if (range == 0) range = 1;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float norm = (heightmap[x, y] - minHeight) / range;
                        byte val = (byte)(norm * 255);
                        bmp.SetPixel(x, y, Color.FromArgb(val, val, val));
                    }
                }
                bmp.Save(pngPath, ImageFormat.Png);
                Console.WriteLine($"[TerrainParser] Saved debug PNG: {pngPath}");
            }
            return heightmap;
        }
    }
}