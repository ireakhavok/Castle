using SiegeEngine.Core.ContextManagement;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace SiegeEngine.Core.Rendering
{
    public static class TextureLoader
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly HashSet<byte> ValidTgaTypes = new HashSet<byte> { 1, 2, 3, 9, 10, 11, 32, 33 };
        public static (uint, byte) LoadTexture(IRenderContext renderContext, string path, int proceduralFallbackId = 1, int wrapS = (int)GLEnum.Repeat, int wrapT = (int)GLEnum.Repeat)
        {
            try
            {
                string extension = Path.GetExtension(path).ToLower();
                if (extension == ".tga")
                {
                    Console.WriteLine("TextureLoader: Loading as TGA file");
                    (uint textureId, byte pixelDepth2) = LoadTgaTexture(renderContext, path, wrapS, wrapT);
                    if (textureId != 0)
                    {
                        Console.WriteLine($"TextureLoader: TGA load result for {path}: Texture ID {textureId}");
                        return (textureId, pixelDepth2);
                    }
                    Console.WriteLine("TextureLoader: TGA loading failed, attempting PNG");
                }
                Console.WriteLine("TextureLoader: Loading as PNG file");
                using (var bitmap = new Bitmap(path))
                {
                    Console.WriteLine($"Bitmap dimensions: {bitmap.Width}x{bitmap.Height}, PixelFormat: {bitmap.PixelFormat}");
                    (uint textureId, byte pixelDepth) = LoadTextureFromBitmap(renderContext, bitmap, wrapS, wrapT);
                    Console.WriteLine($"TextureLoader: PNG load result for {path}: Texture ID {textureId}");
                    return (textureId, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TextureLoader: Failed to load texture {path}: {ex.Message}, StackTrace: {ex.StackTrace}");
                return (0, 0);
            }
        }
        public static (uint, byte) LoadEmbeddedTexture(IRenderContext renderContext, byte[] textureData, string textureName, int proceduralFallbackId = 1, int wrapS = (int)GLEnum.Repeat, int wrapT = (int)GLEnum.Repeat)
        {
            try
            {
                byte imageType = textureData[2];
                ushort widthTga = BitConverter.ToUInt16(textureData, 12);
                ushort heightTga = BitConverter.ToUInt16(textureData, 14);
                byte pixelDepth = textureData[16];
                Console.WriteLine($"TextureLoader: TGA Header for {textureName}: Type={imageType}, Width={widthTga}, Height={heightTga}, PixelDepth={pixelDepth}");
                if (ValidTgaTypes.Contains(imageType) && pixelDepth is 8 or 16 or 24 or 32 && widthTga > 0 && heightTga > 0 && widthTga <= 16384 && heightTga <= 16384)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tga");
                    File.WriteAllBytes(tempPath, textureData);
                    try
                    {
                        (uint textureId, byte pixelDepth2) = LoadTgaTexture(renderContext, tempPath, wrapS, wrapT);
                        if (textureId != 0)
                        {
                            Console.WriteLine($"TextureLoader: TGA load result for {textureName}: Texture ID {textureId}");
                            return (textureId, pixelDepth);
                        }
                    }
                    finally
                    {
                        try { File.Delete(tempPath); }
                        catch (IOException) { Console.WriteLine($"TextureLoader: Warning: Failed to delete temp file '{tempPath}'"); }
                    }
                }
                else
                {
                    Console.WriteLine($"TextureLoader: Invalid TGA type {imageType} or dimensions for {textureName}, attempting PNG");
                }
                // Fallback to PNG
                using (var stream = new MemoryStream(textureData))
                using (var bitmap = new Bitmap(stream))
                {
                    (uint textureId, pixelDepth) = LoadTextureFromBitmap(renderContext, bitmap, wrapS, wrapT);
                    Console.WriteLine($"TextureLoader: PNG fallback load result for {textureName}: Texture ID {textureId}");
                    return (textureId, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TextureLoader: Failed to load embedded texture {textureName}: {ex.Message}, StackTrace: {ex.StackTrace}");
                return (0, 0);
            }
        }
        public static (uint, byte) LoadTgaTexture(IRenderContext renderContext, string path, int wrapS = (int)GLEnum.Repeat, int wrapT = (int)GLEnum.Repeat)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    byte idLength = reader.ReadByte();
                    byte colorMapType = reader.ReadByte();
                    byte imageType = reader.ReadByte();
                    reader.ReadBytes(5); // Skip color map specification
                    reader.ReadInt16(); // Skip x-origin
                    reader.ReadInt16(); // Skip y-origin
                    ushort width = reader.ReadUInt16();
                    ushort height = reader.ReadUInt16();
                    byte pixelDepth = reader.ReadByte();
                    byte imageDescriptor = reader.ReadByte();
                    //Console.WriteLine($"TGA Header: Type={imageType}, Width={width}, Height={height}, PixelDepth={pixelDepth}, ImageDescriptor={imageDescriptor}");
                    if (width == 0 || height == 0 || width > 16384 || height > 16384)
                    {
                        Console.WriteLine("TextureLoader: Invalid TGA dimensions or exceeds max size (16384)");
                        return (0, pixelDepth);
                    }
                    if (!ValidTgaTypes.Contains(imageType))
                    {
                        Console.WriteLine($"TextureLoader: Unsupported TGA image type: {imageType}");
                        return (0, pixelDepth);
                    }
                    if (idLength > 0)
                        reader.ReadBytes(idLength);
                    int internalFormat;
                    int pixelFormat;
                    int bytesPerPixel;
                    if (pixelDepth == 24)
                    {
                        internalFormat = renderContext.Enums.InternalRgb;
                        pixelFormat = renderContext.Enums.PixelBgr;
                        bytesPerPixel = 3;
                    }
                    else if (pixelDepth == 32)
                    {
                        internalFormat = renderContext.Enums.InternalRgba;
                        pixelFormat = renderContext.Enums.PixelBgra;
                        bytesPerPixel = 4;
                    }
                    else
                    {
                        Console.WriteLine($"TextureLoader: Unsupported TGA pixel depth: {pixelDepth}");
                        return (0, pixelDepth);
                    }
                    byte[] pixelData = new byte[width * height * bytesPerPixel];
                    int pixelIndex = 0;
                    if (imageType == 2 || imageType == 1 || imageType == 3)
                    {
                        pixelData = reader.ReadBytes(width * height * bytesPerPixel);
                    }
                    else if (imageType == 9 || imageType == 10 || imageType == 11)
                    {
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
                    else if (imageType == 32 || imageType == 33)
                    {
                        Console.WriteLine($"TextureLoader: Limited support for TGA type {imageType}, treating as uncompressed");
                        pixelData = reader.ReadBytes(width * height * bytesPerPixel);
                    }
                    // Handle flipping based on image descriptor (bit 5: 0x20 for top-left origin)
                    int rowSize = width * bytesPerPixel;
                    if ((imageDescriptor & 0x20) == 0) // Bottom-left origin (bottom-up data), flip to top-down for consistency
                    {
                        byte[] flippedData = new byte[pixelData.Length];
                        for (int y = 0; y < height; y++)
                        {
                            Array.Copy(pixelData, y * rowSize, flippedData, (height - 1 - y) * rowSize, rowSize);
                        }
                        pixelData = flippedData;
                        //Console.WriteLine("TextureLoader: Flipped TGA pixel data rows (bottom-up to top-down)");
                    }
                    else
                    {
                        Console.WriteLine("TextureLoader: TGA is already top-down, no flip needed");
                    }
                    uint texture;
                    renderContext.GenTextures(1, out texture);
                    renderContext.BindTexture(renderContext.Enums.Texture2D, texture);
                    renderContext.PixelStore(renderContext.Enums.UnpackAlignment, 1);
                    unsafe
                    {
                        fixed (byte* ptr = pixelData)
                        {
                            renderContext.TexImage2D(renderContext.Enums.Texture2D, 0, internalFormat, width, height, 0, pixelFormat, renderContext.Enums.UnsignedByte, ptr);
                        }
                    }
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureMinFilter, renderContext.Enums.LinearMipmapLinear);
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureMagFilter, renderContext.Enums.Linear);
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureWrapS, renderContext.Enums.ClampToEdge);
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureWrapT, renderContext.Enums.ClampToEdge);
                    renderContext.TexParameter(renderContext.Enums.Texture2D, renderContext.Enums.TextureLodBias, 0);
                    renderContext.GenerateMipmap(renderContext.Enums.Texture2D);
                    if (renderContext.IsExtensionPresent("EXT_texture_filter_anisotropic"))
                    {
                        renderContext.GetFloat(renderContext.Enums.MaxTextureMaxAnisotropyExt, out float maxAniso);
                        renderContext.TexParameterf(renderContext.Enums.Texture2D, renderContext.Enums.TextureMaxAnisotropyExt, Math.Min(16.0f, maxAniso));
                    }
                    renderContext.BindTexture(renderContext.Enums.Texture2D, 0);
                    //Console.WriteLine($"TGA texture loaded: {texture}");
                    return (texture, pixelDepth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TextureLoader: Failed to load TGA texture {path}: {ex.Message}, StackTrace: {ex.StackTrace}");
                return (0, 0);
            }
        }
        private static (uint, byte) LoadTextureFromBitmap(IRenderContext renderContext, Bitmap bitmap, int wrapS = (int)GLEnum.Repeat, int wrapT = (int)GLEnum.Repeat)
        {
            try
            {
                byte pixelDepth;
                //Console.WriteLine($"TextureLoader: Processing bitmap {bitmap.Width}x{bitmap.Height}, PixelFormat: {bitmap.PixelFormat}");
                int internalFormat;
                int pixelFormat;
                switch (bitmap.PixelFormat)
                {
                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                        internalFormat = renderContext.Enums.InternalRgba;
                        pixelFormat = renderContext.Enums.PixelBgra;
                        pixelDepth = 32;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        internalFormat = renderContext.Enums.InternalRgb;
                        pixelFormat = renderContext.Enums.PixelBgr;
                        pixelDepth = 32;
                        break;
                    default:
                        pixelDepth = 32;
                        Console.WriteLine($"TextureLoader: Unsupported bitmap pixel format: {bitmap.PixelFormat}, converting to 32bppArgb");
                        using (var convertedBitmap = new Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                        {
                            using (var g = Graphics.FromImage(convertedBitmap))
                            {
                                g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                            }
                            (uint textureId, pixelDepth) = LoadTextureFromBitmap(renderContext, convertedBitmap, wrapS, wrapT);
                            //Console.WriteLine($"TextureLoader: Converted bitmap load result: Texture ID {textureId}");
                            return (textureId, pixelDepth);
                        }
                }
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                try
                {
                    if (data.Scan0 == nint.Zero)
                    {
                        Console.WriteLine($"TextureLoader: Invalid bitmap pixel data for {bitmap.Width}x{bitmap.Height}, PixelFormat: {bitmap.PixelFormat}");
                        return (0, 0);
                    }
                    uint texture;
                    renderContext.GenTextures(1, out texture);
                    if (texture == 0)
                    {
                        Console.WriteLine("TextureLoader: Failed to generate OpenGL texture");
                        return (0, 0);
                    }
                    try
                    {
                        renderContext.BindTexture(renderContext.Enums.Texture2D, texture);
                        int error = renderContext.GetError();
                        if (error != renderContext.Enums.NoError)
                        {
                            Console.WriteLine($"TextureLoader: OpenGL error before TexImage2D: {error}");
                            renderContext.DeleteTexture(texture);
                            return (0, 0);
                        }
                        int bytesPerPixel = bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb ? 4 : 3;
                        int dataSize = bitmap.Width * bitmap.Height * bytesPerPixel;
                        byte[] pixelData = new byte[dataSize];
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixelData, 0, dataSize);
                        Console.WriteLine($"TextureLoader: Copied {dataSize} bytes of pixel data, Stride: {data.Stride}");
                        unsafe
                        {
                            fixed (byte* ptr = pixelData)
                            {
                                renderContext.TexImage2D(renderContext.Enums.Texture2D, 0, internalFormat, (uint)bitmap.Width, (uint)bitmap.Height, 0, pixelFormat, renderContext.Enums.UnsignedByte, ptr);
                            }
                        }
                        Console.WriteLine("TexImage2D called successfully");
                        error = renderContext.GetError();
                        if (error != renderContext.Enums.NoError)
                        {
                            Console.WriteLine($"TextureLoader: OpenGL error after TexImage2D: {error}");
                            renderContext.DeleteTexture(texture);
                            return (0, 0);
                        }
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
                        renderContext.BindTexture(renderContext.Enums.Texture2D, 0);
                        //Console.WriteLine($"Texture loaded: {texture}");
                        return (texture, pixelDepth);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"TextureLoader: Failed to upload texture to OpenGL: {ex.Message}, StackTrace: {ex.StackTrace}");
                        renderContext.DeleteTexture(texture);
                        return (0, 0);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TextureLoader: Failed to process bitmap: {ex.Message}, StackTrace: {ex.StackTrace}");
                return (0, 0);
            }
        }
    }
}