using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
namespace SiegeEngine.Core.Terrain
{
    public static class TerrainParser
    {
        private class LzwDecompressor
        {
            private byte[] input;
            private int position = 0;
            private int bitBuffer = 0;
            private int bitsInBuffer = 0;
            private List<List<byte>> dictionary = new List<List<byte>>();
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
                int code = bitBuffer >> (bitsInBuffer - codeSize);
                bitsInBuffer -= codeSize;
                bitBuffer &= (1 << bitsInBuffer) - 1;
                return code;
            }

            public byte[] Decompress()
            {
                List<byte> output = new List<byte>();
                dictionary.Clear();
                for (int i = 0; i < 256; i++)
                {
                    dictionary.Add(new List<byte> { (byte)i });
                }
                int code = GetNextCode();
                if (code == clearCode) code = GetNextCode();
                if (code == eoiCode) return output.ToArray();
                List<byte> entry = dictionary[code];
                output.AddRange(entry);
                int oldCode = code;
                while ((code = GetNextCode()) != eoiCode)
                {
                    if (code == clearCode)
                    {
                        dictionary.Clear();
                        for (int i = 0; i < 256; i++)
                        {
                            dictionary.Add(new List<byte> { (byte)i });
                        }
                        codeSize = 9;
                        code = GetNextCode();
                        if (code == eoiCode) break;
                        entry = dictionary[code];
                        output.AddRange(entry);
                        oldCode = code;
                        continue;
                    }
                    if (code < dictionary.Count)
                    {
                        entry = dictionary[code];
                    }
                    else
                    {
                        entry = new List<byte>(dictionary[oldCode]);
                        entry.Add(dictionary[oldCode][0]);
                    }
                    output.AddRange(entry);
                    List<byte> newEntry = new List<byte>(dictionary[oldCode]);
                    newEntry.Add(entry[0]);
                    dictionary.Add(newEntry);
                    if (dictionary.Count == (1 << codeSize) && codeSize < 12)
                    {
                        codeSize++;
                    }
                    oldCode = code;
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
            uint ifdOffset = BitConverter.ToUInt32(bytes, 4);
            uint numEntries = BitConverter.ToUInt16(bytes, (int)ifdOffset);
            Console.WriteLine($"[TerrainParser] IFD at {ifdOffset}, {numEntries} entries");
            uint imageWidth = 0;
            uint imageHeight = 0;
            uint bitsPerSample = 0;
            uint compression = 0;
            uint sampleFormat = 0;
            uint rowsPerStrip = 0;
            uint tileWidth = 0;
            uint tileLength = 0;
            uint predictor = 1;
            uint offsetsOffset = 0;
            uint byteCountsOffset = 0;
            ushort offsetsType = 0;
            ushort byteCountsType = 0;
            uint numBlocks = 0;
            for (uint i = 0; i < numEntries; i++)
            {
                uint entryOffset = ifdOffset + 2 + i * 12;
                ushort tag = BitConverter.ToUInt16(bytes, (int)entryOffset);
                ushort type = BitConverter.ToUInt16(bytes, (int)entryOffset + 2);
                uint count = BitConverter.ToUInt32(bytes, (int)entryOffset + 4);
                uint valOffset = BitConverter.ToUInt32(bytes, (int)entryOffset + 8);
                uint value = 0;
                if (count == 1)
                {
                    value = type == 3 ? (ushort)valOffset : valOffset;
                }
                else
                {
                    if (tag == 273 || tag == 324) // StripOffsets or TileOffsets
                    {
                        offsetsOffset = valOffset;
                        offsetsType = type;
                        numBlocks = count;
                    }
                    else if (tag == 279 || tag == 325) // StripByteCounts or TileByteCounts
                    {
                        byteCountsOffset = valOffset;
                        byteCountsType = type;
                    }
                    Console.WriteLine($"[TerrainParser] Tag {tag} (multi {count}): type {type} offset {valOffset}");
                    continue;
                }
                Console.WriteLine($"[TerrainParser] Tag {tag}: value {value} type {type} count {count}");
                switch (tag)
                {
                    case 256: imageWidth = value; break;
                    case 257: imageHeight = value; break;
                    case 258: bitsPerSample = value; break;
                    case 259: compression = value; break;
                    case 339: sampleFormat = value; break;
                    case 278: rowsPerStrip = value; break;
                    case 317: predictor = value; break;
                    case 322: tileWidth = value; break;
                    case 323: tileLength = value; break;
                }
            }
            bool isTiled = tileWidth > 0;
            if (sampleFormat != 3 || bitsPerSample != 32)
            {
                throw new Exception($"Unsupported format: SampleFormat={sampleFormat}, BitsPerSample={bitsPerSample}");
            }
            if (numBlocks == 0)
            {
                throw new Exception("No tiles or strips found");
            }
            List<uint> offsets = new List<uint>();
            uint entrySize = (uint)(offsetsType == 3 ? 2 : 4);
            for (uint j = 0; j < numBlocks; j++)
            {
                uint off = offsetsOffset + j * entrySize;
                if (off + entrySize > bytes.Length) throw new Exception("Offset out of bounds");
                uint val = offsetsType == 3 ? BitConverter.ToUInt16(bytes, (int)off) : BitConverter.ToUInt32(bytes, (int)off);
                offsets.Add(val);
            }
            List<uint> byteCounts = new List<uint>();
            entrySize = (uint)(byteCountsType == 3 ? 2 : 4);
            for (uint j = 0; j < numBlocks; j++)
            {
                uint off = byteCountsOffset + j * entrySize;
                if (off + entrySize > bytes.Length) throw new Exception("Byte count offset out of bounds");
                uint val = byteCountsType == 3 ? BitConverter.ToUInt16(bytes, (int)off) : BitConverter.ToUInt32(bytes, (int)off);
                byteCounts.Add(val);
            }
            List<byte> fullRawData = new List<byte>();
            for (uint i = 0; i < numBlocks; i++)
            {
                uint offset = offsets[(int)i];
                uint byteCount = byteCounts[(int)i];
                if (offset + byteCount > bytes.Length) throw new Exception("Data out of bounds");
                byte[] blockData = new byte[byteCount];
                Array.Copy(bytes, (int)offset, blockData, 0, (int)byteCount);
                byte[] decompressed;
                if (compression == 5)
                {
                    decompressed = new LzwDecompressor(blockData).Decompress();
                }
                else if (compression == 1)
                {
                    decompressed = blockData;
                }
                else
                {
                    throw new Exception($"Unsupported compression: {compression}");
                }
                // Apply predictor if necessary
                if (predictor == 3)
                {
                    int rowLength = isTiled ? (int)tileWidth * 4 : (int)imageWidth * 4;
                    int numRows = isTiled ? (int)tileLength : (int)rowsPerStrip;
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
            width = (int)imageWidth;
            height = (int)imageHeight;
            float[,] heightmap = new float[width, height];
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;
            int idx = 0;
            if (isTiled)
            {
                uint numTilesX = (imageWidth + tileWidth - 1) / tileWidth;
                uint numTilesY = (imageHeight + tileLength - 1) / tileLength;
                int blockIndex = 0;
                for (uint tileY = 0; tileY < numTilesY; tileY++)
                {
                    for (uint tileX = 0; tileX < numTilesX; tileX++)
                    {
                        int thisTileW = (int)Math.Min(tileWidth, imageWidth - tileX * tileWidth);
                        int thisTileH = (int)Math.Min(tileLength, imageHeight - tileY * tileLength);
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
                                heightmap[(int)(tileX * tileWidth + col), (int)(tileY * tileLength + row)] = h;
                            }
                            idx += (int)(tileWidth - thisTileW) * 4; // skip padding if any
                        }
                        blockIndex++;
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