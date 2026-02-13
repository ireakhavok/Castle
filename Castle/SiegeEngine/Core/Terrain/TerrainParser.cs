using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
using System.Linq;
namespace SiegeEngine.Core.Terrain
{
    public class TiffTag
    {
        public ushort Tag { get; set; }
        public string Name { get; set; } = "Unknown";
        public ushort Type { get; set; }
        public uint Count { get; set; }
        public uint ValueOrOffset { get; set; }
        public object Value { get; set; }
    }
    public class TiffIFD
    {
        public uint Offset { get; set; }
        public Dictionary<ushort, TiffTag> Tags { get; set; } = new Dictionary<ushort, TiffTag>();
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
        public uint FillOrder { get; set; } = 1;
        public List<uint> BlockOffsets { get; set; } = new List<uint>();
        public List<uint> BlockByteCounts { get; set; } = new List<uint>();
        public ushort BlockOffsetsType { get; set; }
        public ushort BlockByteCountsType { get; set; }
        public uint NumBlocks { get; set; }
        public ushort PhotometricInterpretation { get; set; } = 1;
        public ushort SamplesPerPixel { get; set; } = 1;
        public ushort PlanarConfig { get; set; } = 1;
    }
    public static class TerrainParser
    {
        private static readonly Dictionary<ushort, string> TagNames = new Dictionary<ushort, string>
        {
            { 256, "ImageWidth" }, { 257, "ImageLength" }, { 258, "BitsPerSample" },
            { 259, "Compression" }, { 262, "PhotometricInterpretation" },
            { 266, "FillOrder" }, { 277, "SamplesPerPixel" }, { 278, "RowsPerStrip" },
            { 284, "PlanarConfiguration" }, { 317, "Predictor" },
            { 322, "TileWidth" }, { 323, "TileLength" }, { 324, "TileOffsets" },
            { 325, "TileByteCounts" }, { 339, "SampleFormat" },
            { 33550, "ModelPixelScaleTag" }, { 33922, "ModelTiepointTag" },
            { 34735, "GeoKeyDirectoryTag" }, { 34736, "GeoDoubleParamsTag" },
            { 34737, "GeoAsciiParamsTag" }, { 42112, "GDAL_METADATA" },
            { 42113, "GDAL_NODATA" },
            { 316, "SMinSampleValue" } // fixes the "key '316' not present" crash
        };
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
            private uint FillOrder;
            public LzwDecompressor(byte[] compressedData, uint fillOrder)
            {
                input = compressedData;
                FillOrder = fillOrder;
            }
            private int GetNextCode()
            {
                while (bitsInBuffer < codeSize)
                {
                    if (position >= input.Length) return eoiCode;
                    byte b = input[position++];
                    if (FillOrder == 2)
                    {
                        b = (byte)(((b & 0x55) << 1) | ((b & 0xAA) >> 1));
                        b = (byte)(((b & 0x33) << 2) | ((b & 0xCC) >> 2));
                        b = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
                    }
                    bitBuffer = (bitBuffer << 8) | b;
                    bitsInBuffer += 8;
                }
                int code = (int)(bitBuffer >> (bitsInBuffer - codeSize)) & ((1 << codeSize) - 1);
                bitsInBuffer -= codeSize;
                bitBuffer &= ((1UL << bitsInBuffer) - 1UL);
                return code;
            }
            public byte[] Decompress(bool isFirstBlock, int expectedLength)
            {
                List<byte> output = new List<byte>(expectedLength);
                dictionary.Clear();
                for (int i = 0; i < 256; i++) dictionary[i] = new List<byte> { (byte)i };
                int oldCode = -1;
                int nextCode = 258;
                codeSize = 9;
                List<int> codes = new List<int>();
                int code = GetNextCode();
                if (code < 0) return output.ToArray();
                while (code != eoiCode)
                {
                    if (output.Count >= expectedLength) break;
                    codes.Add(code);
                    if (code == clearCode)
                    {
                        dictionary.Clear();
                        for (int i = 0; i < 256; i++) dictionary[i] = new List<byte> { (byte)i };
                        codeSize = 9;
                        nextCode = 258;
                        oldCode = -1;
                        code = GetNextCode();
                        if (code < 0 || code == eoiCode) break;
                        output.AddRange(dictionary[code]);
                        oldCode = code;
                        code = GetNextCode();
                        if (code < 0) break;
                        continue;
                    }
                    List<byte> entry;
                    if (dictionary.ContainsKey(code))
                    {
                        entry = dictionary[code];
                    }
                    else if (code == nextCode && oldCode != -1)
                    {
                        entry = new List<byte>(dictionary[oldCode]) { dictionary[oldCode][0] };
                    }
                    else
                    {
                        Console.WriteLine($"[TerrainParser] Invalid LZW code: {code} (next={nextCode}, size={codeSize}) at output {output.Count}");
                        break;
                    }
                    output.AddRange(entry);
                    if (oldCode != -1)
                    {
                        var newEntry = new List<byte>(dictionary[oldCode]) { entry[0] };
                        dictionary[nextCode] = newEntry;
                        nextCode++;
                        // TIFF LZW EARLY-CHANGE (increase ONE code EARLY)
                        if (nextCode == ((1 << codeSize) - 1) && codeSize < 12)
                        {
                            codeSize++;
                        }
                    }
                    oldCode = code;
                    code = GetNextCode();
                    if (code < 0) break;
                }
                if (isFirstBlock)
                    Console.WriteLine($"[TerrainParser] First 10 codes: {string.Join(", ", codes.Take(10))}");
                Console.WriteLine($"[TerrainParser] Decompressed {output.Count} bytes (expected {expectedLength}), pos {position}/{input.Length}");
                return output.ToArray();
            }
        }
        private static void DecodeDeltaBytes(byte[] ptr, int cols, int channels)
        {
            for (int COL = 1; COL < cols; ++COL)
                for (int CHAN = 0; CHAN < channels; ++CHAN)
                    ptr[COL * channels + CHAN] += ptr[(COL - 1) * channels + CHAN];
        }
        private static void DecodeFPDeltaRow(byte[] input, byte[] output, int cols, int channels, int bytesPerSample)
        {
            DecodeDeltaBytes(input, cols * bytesPerSample, channels);
            int rowIncrement = cols * channels;
            for (int COL = 0; COL < rowIncrement; ++COL)
                for (int BYTE = 0; BYTE < bytesPerSample; ++BYTE)
                    output[bytesPerSample * COL + BYTE] = input[(bytesPerSample - BYTE - 1) * rowIncrement + COL];
        }
        private static void ApplyFloatingPointPredictor3(byte[] decompressed, int tileWidth, int tileHeight, int bytesPerSample = 4, int channels = 1)
        {
            int rowLength = tileWidth * channels * bytesPerSample;
            for (int r = 0; r < tileHeight; r++)
            {
                int rowOff = r * rowLength;
                byte[] rowInput = new byte[rowLength];
                Array.Copy(decompressed, rowOff, rowInput, 0, rowLength);
                byte[] rowOutput = new byte[rowLength];
                DecodeFPDeltaRow(rowInput, rowOutput, tileWidth, channels, bytesPerSample);
                Array.Copy(rowOutput, 0, decompressed, rowOff, rowLength);
            }
        }
        public static float[,] LoadUSGSDEM(string filePath, out int width, out int height, out float minHeight, out float maxHeight)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("TIFF file not found", filePath);
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < 8 || bytes[0] != 'I' || bytes[1] != 'I' || BitConverter.ToUInt16(bytes, 2) != 42)
                throw new Exception("Not a valid TIFF file (II*42)");
            TiffFile tiffFile = new TiffFile { Bytes = bytes };
            tiffFile.Ifd = new TiffIFD();
            tiffFile.Ifd.Offset = BitConverter.ToUInt32(bytes, 4);
            uint numEntries = BitConverter.ToUInt16(bytes, (int)tiffFile.Ifd.Offset);
            Console.WriteLine($"[TerrainParser] IFD at {tiffFile.Ifd.Offset}, {numEntries} entries");
            for (uint i = 0; i < numEntries; i++)
            {
                uint entryOffset = tiffFile.Ifd.Offset + 2 + i * 12;
                TiffTag tag = new TiffTag
                {
                    Tag = BitConverter.ToUInt16(bytes, (int)entryOffset),
                    Type = BitConverter.ToUInt16(bytes, (int)entryOffset + 2),
                    Count = BitConverter.ToUInt32(bytes, (int)entryOffset + 4),
                    ValueOrOffset = BitConverter.ToUInt32(bytes, (int)entryOffset + 8)
                };
                tag.Name = TagNames.TryGetValue(tag.Tag, out var name) ? name : $"Unknown({tag.Tag})";
                uint valSize = tag.Type switch
                {
                    1 or 6 => 1,
                    2 => 1,
                    3 or 8 => 2,
                    4 or 9 or 11 => 4,
                    5 or 10 or 12 => 8,
                    _ => throw new Exception($"Unknown TIFF type {tag.Type}")
                };
                uint dataSize = valSize * tag.Count;
                if (tag.Count == 1 && tag.Type != 2)
                {
                    uint value = tag.Type == 3 ? (uint)BitConverter.ToUInt16(bytes, (int)entryOffset + 8) : tag.ValueOrOffset;
                    tag.Value = value;
                }
                else
                {
                    uint offBase = dataSize <= 4 ? (uint)(entryOffset + 8) : tag.ValueOrOffset;
                    if (offBase + dataSize > bytes.Length) throw new Exception("Data out of bounds");
                    List<object> values = new List<object>();
                    for (uint j = 0; j < tag.Count; j++)
                    {
                        uint off = offBase + j * valSize;
                        object val = tag.Type switch
                        {
                            1 => bytes[off],
                            2 => (char)bytes[off],
                            3 => BitConverter.ToUInt16(bytes, (int)off),
                            4 => BitConverter.ToUInt32(bytes, (int)off),
                            5 => (BitConverter.ToUInt32(bytes, (int)off), BitConverter.ToUInt32(bytes, (int)off + 4)),
                            11 => BitConverter.ToSingle(bytes, (int)off),
                            12 => BitConverter.ToDouble(bytes, (int)off),
                            _ => null
                        };
                        values.Add(val);
                    }
                    tag.Value = tag.Type == 2
                        ? new string(values.OfType<char>().ToArray()).TrimEnd('\0')
                        : values;
                }
                tiffFile.Ifd.Tags[tag.Tag] = tag;
                if (tag.Count == 1 && tag.Value is uint tagValue)
                {
                    switch (tag.Tag)
                    {
                        case 256: tiffFile.ImageWidth = tagValue; break;
                        case 257: tiffFile.ImageHeight = tagValue; break;
                        case 258: tiffFile.BitsPerSample = tagValue; break;
                        case 259: tiffFile.Compression = tagValue; break;
                        case 262: tiffFile.PhotometricInterpretation = (ushort)tagValue; break;
                        case 277: tiffFile.SamplesPerPixel = (ushort)tagValue; break;
                        case 278: tiffFile.RowsPerStrip = tagValue; break;
                        case 284: tiffFile.PlanarConfig = (ushort)tagValue; break;
                        case 317: tiffFile.Predictor = tagValue; break;
                        case 322: tiffFile.TileWidth = tagValue; break;
                        case 323: tiffFile.TileLength = tagValue; break;
                        case 339: tiffFile.SampleFormat = tagValue; break;
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
            Console.WriteLine("[TerrainParser] All parsed TIFF tags (meaningful names):");
            foreach (var kv in tiffFile.Ifd.Tags.OrderBy(k => k.Key))
            {
                var t = kv.Value;
                Console.WriteLine($" Tag {t.Name} ({t.Tag}): type={t.Type}, count={t.Count}, value={t.Value}");
            }
            bool isTiled = tiffFile.TileWidth > 0 && tiffFile.TileLength > 0;
            if (tiffFile.SampleFormat != 3 || tiffFile.BitsPerSample != 32)
                throw new Exception($"Unsupported format: SampleFormat={tiffFile.SampleFormat}, BitsPerSample={tiffFile.BitsPerSample}");
            if (tiffFile.NumBlocks == 0)
                throw new Exception("No tiles or strips found");
            float noData = float.MinValue;
            if (tiffFile.Ifd.Tags.TryGetValue(42113, out var ndTag) && ndTag.Value is string ndStr && float.TryParse(ndStr, out float nd))
            {
                noData = nd;
                Console.WriteLine($"[TerrainParser] NoData value: {noData}");
            }
            List<byte> fullRawData = new List<byte>((int)(tiffFile.ImageWidth * tiffFile.ImageHeight * 4));
            uint numTilesX = isTiled ? (tiffFile.ImageWidth + tiffFile.TileWidth - 1) / tiffFile.TileWidth : 1;
            uint numTilesY = isTiled ? (tiffFile.ImageHeight + tiffFile.TileLength - 1) / tiffFile.TileLength : 1;
            for (uint i = 0; i < tiffFile.NumBlocks; i++)
            {
                uint tileX = i % numTilesX;
                uint tileY = i / numTilesX;
                int thisTileW = isTiled ? (int)Math.Min(tiffFile.TileWidth, tiffFile.ImageWidth - tileX * tiffFile.TileWidth) : (int)tiffFile.ImageWidth;
                int thisTileH = isTiled ? (int)Math.Min(tiffFile.TileLength, tiffFile.ImageHeight - tileY * tiffFile.TileLength) : (int)tiffFile.ImageHeight;
                int expectedBlockSize = isTiled ? (int)(tiffFile.TileWidth * tiffFile.TileLength * (tiffFile.BitsPerSample / 8)) : thisTileW * thisTileH * (int)(tiffFile.BitsPerSample / 8);
                uint offset = tiffFile.BlockOffsets[(int)i];
                uint byteCount = tiffFile.BlockByteCounts[(int)i];
                if (offset + byteCount > bytes.Length) throw new Exception("Data out of bounds");
                byte[] blockData = new byte[byteCount];
                Array.Copy(bytes, (int)offset, blockData, 0, (int)byteCount);
                byte[] decompressed = tiffFile.Compression == 5
                    ? new LzwDecompressor(blockData, tiffFile.FillOrder).Decompress(i == 0, expectedBlockSize)
                    : blockData;
                if (decompressed.Length != expectedBlockSize)
                {
                    byte[] adjusted = new byte[expectedBlockSize];
                    int copyLen = Math.Min(decompressed.Length, expectedBlockSize);
                    Array.Copy(decompressed, 0, adjusted, 0, copyLen);
                    decompressed = adjusted;
                    Console.WriteLine($"[TerrainParser] Adjusted block {i} length from {decompressed.Length} to {expectedBlockSize}");
                }
                if (tiffFile.Predictor == 3)
                    ApplyFloatingPointPredictor3(decompressed, isTiled ? (int)tiffFile.TileWidth : thisTileW, isTiled ? (int)tiffFile.TileLength : thisTileH);
                byte[] dataToAdd = decompressed;
                if (isTiled && (thisTileW < (int)tiffFile.TileWidth || thisTileH < (int)tiffFile.TileLength))
                {
                    int fullW = (int)tiffFile.TileWidth;
                    int fullH = (int)tiffFile.TileLength;
                    int bytesPerSample = (int)(tiffFile.BitsPerSample / 8);
                    int validRowBytes = thisTileW * bytesPerSample;
                    int fullRowBytes = fullW * bytesPerSample;
                    dataToAdd = new byte[thisTileW * thisTileH * bytesPerSample];
                    for (int r = 0; r < thisTileH; r++)
                    {
                        Array.Copy(decompressed, r * fullRowBytes, dataToAdd, r * validRowBytes, validRowBytes);
                    }
                }
                fullRawData.AddRange(dataToAdd);
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
                for (uint tileY = 0; tileY < numTilesY; tileY++)
                    for (uint tileX = 0; tileX < numTilesX; tileX++)
                    {
                        int thisTileW = (int)Math.Min(tiffFile.TileWidth, tiffFile.ImageWidth - tileX * tiffFile.TileWidth);
                        int thisTileH = (int)Math.Min(tiffFile.TileLength, tiffFile.ImageHeight - tileY * tiffFile.TileLength);
                        for (int row = 0; row < thisTileH; row++)
                            for (int col = 0; col < thisTileW; col++)
                            {
                                float h = ToSingleLittleEndian(rawData, idx);
                                idx += 4;
                                if (float.IsNaN(h) || float.IsInfinity(h) || (noData != float.MinValue && Math.Abs(h - noData) < 0.001f))
                                    h = 0f;
                                else
                                {
                                    minHeight = Math.Min(minHeight, h);
                                    maxHeight = Math.Max(maxHeight, h);
                                }
                                heightmap[(int)(tileX * tiffFile.TileWidth + col), (int)(tileY * tiffFile.TileLength + row)] = h;
                            }
                    }
            }
            else
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float h = ToSingleLittleEndian(rawData, idx);
                        idx += 4;
                        if (float.IsNaN(h) || float.IsInfinity(h) || (noData != float.MinValue && Math.Abs(h - noData) < 0.001f))
                            h = 0f;
                        else
                        {
                            minHeight = Math.Min(minHeight, h);
                            maxHeight = Math.Max(maxHeight, h);
                        }
                        heightmap[x, y] = h;
                    }
            }
            Console.WriteLine($"[TerrainParser] Loaded {width}x{height} USGS DEM. Raw Min={minHeight:F1}m, Max={maxHeight:F1}m");
            bool debugPng = true;
            if (debugPng)
            {
                string pngPath = Path.ChangeExtension(filePath, ".png");
                using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                float range = maxHeight - minHeight;
                if (range == 0) range = 1;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float norm = (heightmap[x, y] - minHeight) / range;
                        byte val = (byte)(norm * 255);
                        bmp.SetPixel(x, y, Color.FromArgb(val, val, val));
                    }
                bmp.Save(pngPath, ImageFormat.Png);
                Console.WriteLine($"[TerrainParser] Saved debug PNG: {pngPath}");
            }
            return heightmap;
        }
        private static float ToSingleLittleEndian(byte[] bytes, int offset)
        {
            if (!BitConverter.IsLittleEndian)
            {
                byte[] reversed = new byte[4] { bytes[offset + 3], bytes[offset + 2], bytes[offset + 1], bytes[offset] };
                return BitConverter.ToSingle(reversed, 0);
            }
            return BitConverter.ToSingle(bytes, offset);
        }
    }
}