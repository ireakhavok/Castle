// Folder: SiegeEngine/Core/Rendering
// File: TextureLoader.cs
using SiegeEngine.Core.GPU.ContextManagement;
using Silk.NET.OpenGL;
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
        public static (uint, byte) LoadTexture(IRenderContext renderContext, string path, int proceduralFallbackId = 1, int wrapS = (int)GLEnum.Repeat, int wrapT = (int)GLEnum.Repeat)
        {
            Console.WriteLine($"[TextureLoader] LoadTexture START: {path}");
            try
            {
                string extension = Path.GetExtension(path).ToLower();
                if (extension == ".tga")
                {
                    Console.WriteLine($"[TextureLoader] Loading as TGA: {path}");
                    (uint textureId, byte pixelDepth2) = LoadTgaTexture(renderContext, path, wrapS, wrapT);
                    if (textureId != 0)
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
                    (uint textureId, byte pixelDepth) = LoadTextureFromBitmap(renderContext, bitmap, wrapS, wrapT);
                    Console.WriteLine($"[TextureLoader] PNG load result for {path}: ID={textureId}");
                    return (textureId, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] CRITICAL FAIL {path}: {ex.Message}\n{ex.StackTrace}");
                return (0, 0);
            }
        }
        public static (uint texId, Vector2 nativeSize) LoadTextureWithSize(IRenderContext renderContext, string path)
        {
            Console.WriteLine($"[TextureLoader] LoadTextureWithSize: {path}");
            try
            {
                using (var bitmap = new Bitmap(path))
                {
                    (uint texId, byte _) = LoadTextureFromBitmap(renderContext, bitmap);
                    return (texId, new Vector2(bitmap.Width, bitmap.Height));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] LoadTextureWithSize FAIL {path}: {ex.Message}");
                return (0, Vector2.One);
            }
        }
        public static (uint, byte) LoadEmbeddedTexture(IRenderContext renderContext, byte[] textureData, string textureName, int proceduralFallbackId = 1, int wrapS = (int)GLEnum.Repeat, int wrapT = (int)GLEnum.Repeat)
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
                        (uint textureId, byte pixelDepth2) = LoadTgaTexture(renderContext, tempPath, wrapS, wrapT);
                        if (textureId != 0)
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
                    (uint textureId, pixelDepth) = LoadTextureFromBitmap(renderContext, bitmap, wrapS, wrapT);
                    Console.WriteLine($"[TextureLoader] Embedded PNG fallback result for {textureName}: ID={textureId}");
                    return (textureId, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] Embedded CRITICAL FAIL {textureName}: {ex.Message}");
                return (0, 0);
            }
        }
        public static (uint, byte) LoadTgaTexture(IRenderContext renderContext, string path, int wrapS = (int)GLEnum.Repeat, int wrapT = (int)GLEnum.Repeat)
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
                        return (0, pixelDepth);
                    }
                    if (!ValidTgaTypes.Contains(imageType))
                    {
                        Console.WriteLine($"[TextureLoader] Unsupported TGA type {imageType}");
                        return (0, pixelDepth);
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
                    uint texture;
                    renderContext.GenTextures(1, out texture);
                    renderContext.BindTexture(renderContext.Enums.Texture2D, texture);
                    renderContext.PixelStore(renderContext.Enums.UnpackAlignment, 1);
                    Console.WriteLine($"[TextureLoader] Uploading TGA {width}x{height} to texture {texture}");
                    unsafe
                    {
                        fixed (byte* ptr = pixelData)
                        {
                            renderContext.TexImage2D(renderContext.Enums.Texture2D, 0, internalFormat, (uint)width, (uint)height, 0, pixelFormat, renderContext.Enums.UnsignedByte, ptr);
                        }
                    }
                    int error = renderContext.GetError();
                    if (error != renderContext.Enums.NoError)
                    {
                        Console.WriteLine($"[TextureLoader] TexImage2D ERROR after TGA upload: {error}");
                    }
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureMinFilter, renderContext.Enums.LinearMipmapLinear);
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureMagFilter, renderContext.Enums.Linear);
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureWrapS, renderContext.Enums.ClampToEdge);
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureWrapT, renderContext.Enums.ClampToEdge);
                    renderContext.GenerateMipmap(renderContext.Enums.Texture2D);
                    if (renderContext.IsExtensionPresent("EXT_texture_filter_anisotropic"))
                    {
                        renderContext.GetFloat(renderContext.Enums.MaxTextureMaxAnisotropyExt, out float maxAniso);
                        renderContext.TexParameterf(renderContext.Enums.Texture2D, renderContext.Enums.TextureMaxAnisotropyExt, Math.Min(16.0f, maxAniso));
                    }
                    renderContext.BindTexture(renderContext.Enums.Texture2D, 0);
                    Console.WriteLine($"[TextureLoader] TGA load complete: ID={texture}");
                    return (texture, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextureLoader] TGA CRITICAL FAIL {path}: {ex.Message}");
                return (0, 0);
            }
        }
        public static (uint, byte) LoadTextureFromBitmap(IRenderContext renderContext, Bitmap bitmap, int wrapS = (int)GLEnum.Repeat, int wrapT = (int)GLEnum.Repeat, bool crispPaintMode = false)
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
                        return LoadTextureFromBitmap(renderContext, converted, wrapS, wrapT, crispPaintMode);
                    }
                }
                int internalFormat = renderContext.Enums.InternalRgba;
                int pixelFormat = renderContext.Enums.PixelBgra;
                byte pixelDepth = 32;
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                try
                {
                    uint texture;
                    renderContext.GenTextures(1, out texture);
                    Console.WriteLine($"[TextureLoader] Generated texture ID {texture}");
                    renderContext.BindTexture(renderContext.Enums.Texture2D, texture);
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
                            renderContext.TexImage2D(renderContext.Enums.Texture2D, 0, internalFormat, (uint)bitmap.Width, (uint)bitmap.Height, 0, pixelFormat, renderContext.Enums.UnsignedByte, ptr);
                        }
                    }
                    error = renderContext.GetError();
                    Console.WriteLine($"[TextureLoader] TexImage2D completed - error code: {error}");
                    if (crispPaintMode)
                    {
                        renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureMinFilter, renderContext.Enums.Nearest);
                        renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureMagFilter, renderContext.Enums.Nearest);
                    }
                    else
                    {
                        renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureMinFilter, renderContext.Enums.LinearMipmapLinear);
                        renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureMagFilter, renderContext.Enums.Linear);
                        renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureWrapS, renderContext.Enums.ClampToEdge);
                        renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureWrapT, renderContext.Enums.ClampToEdge);
                        renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureLodBias, -1);
                        renderContext.GenerateMipmap(renderContext.Enums.Texture2D);
                        if (renderContext.IsExtensionPresent("EXT_texture_filter_anisotropic"))
                        {
                            renderContext.GetFloat(renderContext.Enums.MaxTextureMaxAnisotropyExt, out float maxAniso);
                            renderContext.TexParameterf(renderContext.Enums.Texture2D, renderContext.Enums.TextureMaxAnisotropyExt, Math.Min(16.0f, maxAniso));
                        }
                    }
                    renderContext.BindTexture(renderContext.Enums.Texture2D, 0);
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
                return (0, 0);
            }
        }
        public static void UpdateFromBitmap(IRenderContext renderContext, uint textureId, Bitmap bitmap)
        {
            if (renderContext == null || bitmap == null || textureId == 0) return;
            renderContext.BindTexture(renderContext.Enums.Texture2D, textureId);
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                unsafe
                {
                    byte* ptr = (byte*)data.Scan0.ToPointer();
                    renderContext.TexImage2D(renderContext.Enums.Texture2D, 0, renderContext.Enums.InternalRgba, (uint)bitmap.Width, (uint)bitmap.Height, 0, renderContext.Enums.PixelBgra, renderContext.Enums.UnsignedByte, ptr);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            renderContext.GenerateMipmap(renderContext.Enums.Texture2D);
            renderContext.BindTexture(renderContext.Enums.Texture2D, 0);
        }
        public static void DeleteTexture(IRenderContext renderContext, ref uint textureId)
        {
            if (renderContext == null || textureId == 0) return;
            renderContext.DeleteTexture(textureId);
            textureId = 0;
        }
        public static uint LoadCubemap(IRenderContext renderContext, string path)
        {
            uint tex;
            renderContext.GenTextures(1, out tex);
            renderContext.BindTexture(renderContext.Enums.TextureCubeMap, tex);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureMinFilter, renderContext.Enums.Linear);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureMagFilter, renderContext.Enums.Linear);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureWrapS, renderContext.Enums.ClampToEdge);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureWrapT, renderContext.Enums.ClampToEdge);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureWrapR, renderContext.Enums.ClampToEdge);
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
                    {
                        renderContext.TexImage2D(renderContext.Enums.TextureCubeMapPositiveX, 0, renderContext.Enums.InternalRgba, (uint)bmp.Width, (uint)bmp.Height, 0, renderContext.Enums.PixelBgra, renderContext.Enums.UnsignedByte, ptr);
                    }
                }
            }
            renderContext.GenerateMipmap(renderContext.Enums.TextureCubeMap);
            renderContext.BindTexture(renderContext.Enums.TextureCubeMap, 0);
            return tex;
        }
        public static uint LoadSixFacesCubemap(IRenderContext renderContext, string[] faces)
        {
            uint tex;
            renderContext.GenTextures(1, out tex);
            renderContext.BindTexture(renderContext.Enums.TextureCubeMap, tex);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureMinFilter, renderContext.Enums.Linear);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureMagFilter, renderContext.Enums.Linear);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureWrapS, renderContext.Enums.ClampToEdge);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureWrapT, renderContext.Enums.ClampToEdge);
            renderContext.TexParameter(renderContext.Enums.TextureCubeMap, renderContext.Enums.TextureWrapR, renderContext.Enums.ClampToEdge);
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
                            {
                                renderContext.TexImage2D((int)(renderContext.Enums.TextureCubeMapPositiveX + i), 0, renderContext.Enums.InternalRgba, (uint)bmp.Width, (uint)bmp.Height, 0, renderContext.Enums.PixelBgra, renderContext.Enums.UnsignedByte, ptr);
                            }
                        }
                    }
                }
            }
            renderContext.GenerateMipmap(renderContext.Enums.TextureCubeMap);
            renderContext.BindTexture(renderContext.Enums.TextureCubeMap, 0);
            return tex;
        }
    }
}