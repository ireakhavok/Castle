// Folder: SiegeEngine/Core/Rendering
// File: TextureLoader.cs
using SiegeEngine.Core.GPU.ContextManagement;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
namespace SiegeEngine.Core.GPU
{
    public static class TextureLoader
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly HashSet<byte> ValidTgaTypes = new HashSet<byte> { 1, 2, 3, 9, 10, 11, 32, 33 };

        private static int ResolveWrap(IRenderContext renderContext, int wrap)
        {
            return wrap != 0 ? wrap : renderContext.Enums.ClampToEdge;
        }

        public static (GpuHandle, byte) LoadTexture(IRenderContext renderContext, string path, int proceduralFallbackId = 1, int wrapS = 0, int wrapT = 0)
        {
            Console.WriteLine($"[TextureLoader] LoadTexture START: {path}");
            try
            {
                string extension = Path.GetExtension(path).ToLower();
                if (extension == ".tga")
                {
                    Console.WriteLine($"[TextureLoader] Loading as TGA: {path}");
                    (GpuHandle textureId, byte pixelDepth2) = LoadTgaTexture(renderContext, path, wrapS, wrapT);
                    if (textureId.IsValid)
                    {
                        Console.WriteLine($"[TextureLoader] TGA SUCCESS for {path}: ID={textureId}");
                        return (textureId, pixelDepth2);
                    }
                    Console.WriteLine($"[TextureLoader] TGA failed, falling back to PNG: {path}");
                }
                Console.WriteLine($"[TextureLoader] Loading as PNG: {path}");
                using (var bitmap = new Bitmap(path))
                {
                    Console.WriteLine($"[TextureLoader] Bitmap loaded: {bitmap.Width}x{bitmap.Height} {bitmap.PixelFormat}");
                    (GpuHandle textureId, byte pixelDepth) = LoadTextureFromBitmap(renderContext, bitmap, false, wrapS, wrapT);
                    Console.WriteLine($"[TextureLoader] PNG load result for {path}: ID={textureId}");
                    return (textureId, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] CRITICAL FAIL {path}: {ex.Message}\n{ex.StackTrace}");
                return (default, 0);
            }
        }
        public static (GpuHandle texId, Vector2 nativeSize) LoadTextureWithSize(IRenderContext renderContext, string path)
        {
            Console.WriteLine($"[TextureLoader] LoadTextureWithSize: {path}");
            try
            {
                using (var bitmap = new Bitmap(path))
                {
                    (GpuHandle texId, byte _) = LoadTextureFromBitmap(renderContext, bitmap);
                    return (texId, new Vector2(bitmap.Width, bitmap.Height));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] LoadTextureWithSize FAIL {path}: {ex.Message}");
                return (default, Vector2.One);
            }
        }
        public static (GpuHandle, byte) LoadEmbeddedTexture(IRenderContext renderContext, byte[] textureData, string textureName, int proceduralFallbackId = 1, int wrapS = 0, int wrapT = 0)
        {
            Console.WriteLine($"[TextureLoader] LoadEmbeddedTexture START: {textureName}");
            try
            {
                byte imageType = textureData[2];
                ushort widthTga = BitConverter.ToUInt16(textureData, 12);
                ushort heightTga = BitConverter.ToUInt16(textureData, 14);
                byte pixelDepth = textureData[16];
                Console.WriteLine($"[TextureLoader] TGA Header for {textureName}: Type={imageType}, {widthTga}x{heightTga}, Depth={pixelDepth}");
                if (ValidTgaTypes.Contains(imageType) && pixelDepth is 8 or 16 or 24 or 32 && widthTga > 0 && heightTga > 0 && widthTga <= 16384 && heightTga <= 16384)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tga");
                    File.WriteAllBytes(tempPath, textureData);
                    try
                    {
                        (GpuHandle textureId, byte pixelDepth2) = LoadTgaTexture(renderContext, tempPath, wrapS, wrapT);
                        if (textureId.IsValid)
                        {
                            Console.WriteLine($"[TextureLoader] Embedded TGA SUCCESS for {textureName}: ID={textureId}");
                            return (textureId, pixelDepth);
                        }
                    }
                    finally
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
                using (var stream = new MemoryStream(textureData))
                using (var bitmap = new Bitmap(stream))
                {
                    (GpuHandle textureId, pixelDepth) = LoadTextureFromBitmap(renderContext, bitmap, false, wrapS, wrapT);
                    Console.WriteLine($"[TextureLoader] Embedded PNG fallback result for {textureName}: ID={textureId}");
                    return (textureId, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] Embedded CRITICAL FAIL {textureName}: {ex.Message}");
                return (default, 0);
            }
        }
        public static (GpuHandle, byte) LoadTgaTexture(IRenderContext renderContext, string path, int wrapS = 0, int wrapT = 0)
        {
            Console.WriteLine($"[TextureLoader] LoadTgaTexture: {path}");
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    byte idLength = reader.ReadByte();
                    byte colorMapType = reader.ReadByte();
                    byte imageType = reader.ReadByte();
                    reader.ReadBytes(5);
                    reader.ReadInt16();
                    reader.ReadInt16();
                    ushort width = reader.ReadUInt16();
                    ushort height = reader.ReadUInt16();
                    byte pixelDepth = reader.ReadByte();
                    byte imageDescriptor = reader.ReadByte();
                    Console.WriteLine($"[TextureLoader] TGA header: {width}x{height} depth={pixelDepth} type={imageType}");
                    if (width == 0 || height == 0 || width > 16384 || height > 16384)
                    {
                        Console.WriteLine("[TextureLoader] Invalid TGA dimensions");
                        return (default, pixelDepth);
                    }
                    if (!ValidTgaTypes.Contains(imageType))
                    {
                        Console.WriteLine($"[TextureLoader] Unsupported TGA type {imageType}");
                        return (default, pixelDepth);
                    }
                    if (idLength > 0)
                        reader.ReadBytes(idLength);
                    int internalFormat = pixelDepth == 24 ? renderContext.Enums.InternalRgb : renderContext.Enums.InternalRgba;
                    int pixelFormat = pixelDepth == 24 ? renderContext.Enums.PixelBgr : renderContext.Enums.PixelBgra;
                    int bytesPerPixel = pixelDepth / 8;
                    byte[] pixelData = new byte[width * height * bytesPerPixel];
                    if (imageType == 2 || imageType == 1 || imageType == 3)
                    {
                        pixelData = reader.ReadBytes(width * height * bytesPerPixel);
                    }
                    else if (imageType == 9 || imageType == 10 || imageType == 11)
                    {
                        int pixelIndex = 0;
                        while (pixelIndex < pixelData.Length)
                        {
                            byte packetHeader = reader.ReadByte();
                            int pixelCount = (packetHeader & 0x7F) + 1;
                            bool isRlePacket = (packetHeader & 0x80) != 0;
                            if (isRlePacket)
                            {
                                byte[] pixel = reader.ReadBytes(bytesPerPixel);
                                for (int i = 0; i < pixelCount && pixelIndex < pixelData.Length; i++)
                                {
                                    Array.Copy(pixel, 0, pixelData, pixelIndex, bytesPerPixel);
                                    pixelIndex += bytesPerPixel;
                                }
                            }
                            else
                            {
                                int bytesToRead = pixelCount * bytesPerPixel;
                                byte[] rawPixels = reader.ReadBytes(bytesToRead);
                                Array.Copy(rawPixels, 0, pixelData, pixelIndex, bytesToRead);
                                pixelIndex += bytesToRead;
                            }
                        }
                    }
                    int rowSize = width * bytesPerPixel;
                    if ((imageDescriptor & 0x20) == 0)
                    {
                        byte[] flippedData = new byte[pixelData.Length];
                        for (int y = 0; y < height; y++)
                        {
                            Array.Copy(pixelData, y * rowSize, flippedData, (height - 1 - y) * rowSize, rowSize);
                        }
                        pixelData = flippedData;
                    }
                    GpuHandle allocated = renderContext.CreateTexture(new TextureDesc
                    {
                        Target = renderContext.Enums.Texture2D,
                        InternalFormat = internalFormat,
                        Width = width,
                        Height = height
                    });
                    GpuHandle texture = allocated;
                    Console.WriteLine($"[TextureLoader] Uploading TGA {width}x{height} to texture {texture}");
                    unsafe
                    {
                        fixed (byte* ptr = pixelData)
                        {
                            renderContext.UpdateTexture(allocated, width, height, pixelFormat, renderContext.Enums.UnsignedByte, ptr);
                        }
                    }
                    int error = renderContext.GetError();
                    if (error != renderContext.Enums.NoError)
                    {
                        Console.WriteLine($"[TextureLoader] TexImage2D ERROR after TGA upload: {error}");
                    }
                    renderContext.SetTextureParams(allocated, renderContext.Enums.LinearMipmapLinear, renderContext.Enums.Linear, ResolveWrap(renderContext, wrapS), ResolveWrap(renderContext, wrapT));
                    renderContext.GenerateMipmaps(allocated);
                    Console.WriteLine($"[TextureLoader] TGA load complete: ID={texture}");
                    return (texture, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] TGA CRITICAL FAIL {path}: {ex.Message}");
                return (default, 0);
            }
        }
        public static (GpuHandle, byte) LoadTextureFromBitmap(IRenderContext renderContext, Bitmap bitmap, bool crispPaintMode = false, int wrapS = 0, int wrapT = 0)
        {
            Console.WriteLine($"[TextureLoader] LoadTextureFromBitmap START: {bitmap.Width}x{bitmap.Height} {bitmap.PixelFormat} crispPaint={crispPaintMode}");
            try
            {
                if (bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                {
                    Console.WriteLine($"[TextureLoader] Converting bitmap to 32bppArgb");
                    using (var converted = new Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(converted))
                        {
                            g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                        }
                        return LoadTextureFromBitmap(renderContext, converted, crispPaintMode, wrapS, wrapT);
                    }
                }
                int internalFormat = renderContext.Enums.InternalRgba;
                int pixelFormat = renderContext.Enums.PixelBgra;
                byte pixelDepth = 32;
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                try
                {
                    GpuHandle allocated = renderContext.CreateTexture(new TextureDesc
                    {
                        Target = renderContext.Enums.Texture2D,
                        InternalFormat = internalFormat,
                        Width = bitmap.Width,
                        Height = bitmap.Height
                    });
                    GpuHandle texture = allocated;
                    Console.WriteLine($"[TextureLoader] Generated texture ID {texture}");
                    int error = renderContext.GetError();
                    if (error != renderContext.Enums.NoError)
                    {
                        Console.WriteLine($"[TextureLoader] ERROR before TexImage2D: {error}");
                    }
                    int bytesPerPixel = 4;
                    int dataSize = bitmap.Width * bitmap.Height * bytesPerPixel;
                    byte[] pixelData = new byte[dataSize];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixelData, 0, dataSize);
                    Console.WriteLine($"[TextureLoader] Copied {dataSize} bytes, Stride={data.Stride}");
                    unsafe
                    {
                        fixed (byte* ptr = pixelData)
                        {
                            renderContext.UpdateTexture(allocated, bitmap.Width, bitmap.Height, pixelFormat, renderContext.Enums.UnsignedByte, ptr);
                        }
                    }
                    error = renderContext.GetError();
                    Console.WriteLine($"[TextureLoader] TexImage2D completed - error code: {error}");
                    if (crispPaintMode)
                    {
                        renderContext.SetTextureParams(allocated, renderContext.Enums.Nearest, renderContext.Enums.Nearest, ResolveWrap(renderContext, wrapS), ResolveWrap(renderContext, wrapT));
                    }
                    else
                    {
                        renderContext.SetTextureParams(allocated, renderContext.Enums.LinearMipmapLinear, renderContext.Enums.Linear, ResolveWrap(renderContext, wrapS), ResolveWrap(renderContext, wrapT));
                        renderContext.GenerateMipmaps(allocated);
                    }
                    return (texture, pixelDepth);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] Bitmap CRITICAL FAIL: {ex.Message}\n{ex.StackTrace}");
                return (default, 0);
            }
        }
        public static void UpdateFromBitmap(IRenderContext renderContext, GpuHandle texture, Bitmap bitmap)
        {
            if (renderContext == null || bitmap == null || !texture.IsValid) return;
            GpuHandle existing = texture;
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                unsafe
                {
                    byte* ptr = (byte*)data.Scan0.ToPointer();
                    renderContext.UpdateTexture(existing, bitmap.Width, bitmap.Height, renderContext.Enums.PixelBgra, renderContext.Enums.UnsignedByte, ptr);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            renderContext.GenerateMipmaps(existing);
        }
        public static void DeleteTexture(IRenderContext renderContext, ref GpuHandle texture)
        {
            if (renderContext == null || !texture.IsValid) return;
            renderContext.Destroy(texture);
            texture = default;
        }
        public static GpuHandle LoadCubemap(IRenderContext renderContext, string path)
        {
            GpuHandle allocated = renderContext.CreateTexture(new TextureDesc
            {
                Target = renderContext.Enums.TextureCubeMap,
                InternalFormat = renderContext.Enums.InternalRgba
            });
            renderContext.SetTextureParams(allocated, renderContext.Enums.Linear, renderContext.Enums.Linear, renderContext.Enums.ClampToEdge, renderContext.Enums.ClampToEdge);
            renderContext.SetTextureParam(allocated, renderContext.Enums.TextureWrapR, renderContext.Enums.ClampToEdge);
            using (var bmp = new Bitmap(path))
            {
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                int dataSize = bmp.Width * bmp.Height * 4;
                byte[] pixelData = new byte[dataSize];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixelData, 0, dataSize);
                bmp.UnlockBits(data);
                unsafe
                {
                    fixed (byte* ptr = pixelData)
                        renderContext.UpdateCubemapFace(allocated, renderContext.Enums.TextureCubeMapPositiveX, bmp.Width, bmp.Height, renderContext.Enums.PixelBgra, renderContext.Enums.UnsignedByte, ptr);
                }
            }
            renderContext.GenerateMipmaps(allocated);
            return allocated;
        }
        public static GpuHandle LoadSixFacesCubemap(IRenderContext renderContext, string[] faces)
        {
            GpuHandle allocated = renderContext.CreateTexture(new TextureDesc
            {
                Target = renderContext.Enums.TextureCubeMap,
                InternalFormat = renderContext.Enums.InternalRgba
            });
            renderContext.SetTextureParams(allocated, renderContext.Enums.Linear, renderContext.Enums.Linear, renderContext.Enums.ClampToEdge, renderContext.Enums.ClampToEdge);
            renderContext.SetTextureParam(allocated, renderContext.Enums.TextureWrapR, renderContext.Enums.ClampToEdge);
            for (int i = 0; i < 6 && i < faces.Length; i++)
            {
                if (File.Exists(faces[i]))
                {
                    using (var bmp = new Bitmap(faces[i]))
                    {
                        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        int dataSize = bmp.Width * bmp.Height * 4;
                        byte[] pixelData = new byte[dataSize];
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixelData, 0, dataSize);
                        bmp.UnlockBits(data);
                        unsafe
                        {
                            fixed (byte* ptr = pixelData)
                                renderContext.UpdateCubemapFace(allocated, renderContext.Enums.TextureCubeMapPositiveX + i, bmp.Width, bmp.Height, renderContext.Enums.PixelBgra, renderContext.Enums.UnsignedByte, ptr);
                        }
                    }
                }
            }
            renderContext.GenerateMipmaps(allocated);
            return allocated;
        }
    }
}