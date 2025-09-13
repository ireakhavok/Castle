// Folder: SiegeEngine.Rendering
// File: SystemFontRenderer.cs
using System;
using System.Drawing;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using SiegeEngine.ContextManagement;
namespace SiegeEngine.Rendering
{
    public class SystemFontRenderer
    {
        private readonly Dictionary<char, CharacterData> _characterData;
        private readonly Dictionary<char, uint> _charTextures;
        private readonly IRenderContext _renderContext;
        private readonly string _fontName;
        private readonly float _baseSize;
        public float BaseSize => _baseSize;
        public SystemFontRenderer(IRenderContext renderContext, string fontName, float fontSize = 12.0f)
        {
            _renderContext = renderContext;
            _fontName = fontName;
            _baseSize = fontSize;
            _characterData = new Dictionary<char, CharacterData>();
            _charTextures = new Dictionary<char, uint>();
            LoadFontData(fontName, fontSize);
        }
        private unsafe void LoadFontData(string fontName, float fontSize)
        {
            //Console.WriteLine($"SystemFontRenderer: Loading font '{fontName}', size {fontSize}");
            try
            {
                using (var font = new Font(fontName, fontSize, FontStyle.Bold))
                using (var bitmap = new Bitmap(1, 1))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 :.,!?-+()[]{}x";
                    foreach (char c in chars)
                    {
                        string text = c.ToString();
                        using (StringFormat format = StringFormat.GenericTypographic)
                        {
                            if (c == ' ') format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                            SizeF size = graphics.MeasureString(text, font, 0, format);
                            int width = (int)Math.Ceiling(size.Width);
                            int height = (int)Math.Ceiling(size.Height);
                            using (var charBitmap = new Bitmap(width, height))
                            using (var charGraphics = Graphics.FromImage(charBitmap))
                            {
                                charGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                                charGraphics.Clear(Color.Transparent);
                                using (var brush = new SolidBrush(Color.White))
                                {
                                    charGraphics.DrawString(text, font, brush, 0, 0, format);
                                }
                                var data = charBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                                int bytesPerPixel = 4;
                                byte[] pixelData = new byte[width * height * bytesPerPixel];
                                nint ptr = data.Scan0;
                                for (int y = 0; y < height; y++)
                                {
                                    nint row = nint.Add(ptr, y * data.Stride);
                                    System.Runtime.InteropServices.Marshal.Copy(row, pixelData, y * width * bytesPerPixel, width * bytesPerPixel);
                                }
                                charBitmap.UnlockBits(data);
                                // Force alpha to 255 for any non-zero pixel
                                for (int i = 0; i < pixelData.Length; i += 4)
                                {
                                    if (pixelData[i] > 0 || pixelData[i + 1] > 0 || pixelData[i + 2] > 0) // Any RGB non-zero
                                    {
                                        pixelData[i + 3] = 255; // Set alpha to opaque
                                    }
                                    else
                                    {
                                        pixelData[i + 3] = 0; // Transparent background
                                    }
                                }
                                // Log sample pixel data and sum
                                string sample = string.Join("-", pixelData.Take(12).Select(b => b.ToString("X2")));
                                long pixelSum = pixelData.Sum(b => (long)b);
                                //Console.WriteLine($"SystemFontRenderer: Loaded character '{c}': {width}x{height}, Sample pixels (BGRA): {sample}, Pixel sum: {pixelSum}");
                                uint texture;
                                _renderContext.GenTextures(1, out texture);
                                _renderContext.BindTexture(_renderContext.Enums.Texture2D, texture);
                                _renderContext.PixelStore(_renderContext.Enums.UnpackAlignment, 1);
                                fixed (byte* pixelPtr = pixelData)
                                {
                                    _renderContext.TexImage2D(_renderContext.Enums.Texture2D, 0, _renderContext.Enums.InternalRgba, (uint)width, (uint)height, 0, _renderContext.Enums.PixelBgra, _renderContext.Enums.UnsignedByte, pixelPtr);
                                }
                                int error = _renderContext.GetError();
                                if (error != _renderContext.Enums.NoError)
                                {
                                    Console.WriteLine($"SystemFontRenderer: OpenGL error after loading texture for '{c}': {error}");
                                }
                                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Linear);
                                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Linear);
                                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapS, _renderContext.Enums.ClampToEdge);
                                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapT, _renderContext.Enums.ClampToEdge);
                                _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
                                _charTextures[c] = texture;
                                _characterData[c] = new CharacterData
                                {
                                    Width = width,
                                    Height = height,
                                    PixelData = pixelData
                                };
                            }
                        }
                    }
                }
                //Console.WriteLine($"SystemFontRenderer: Font '{fontName}' loaded with {_characterData.Count} characters.");
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"SystemFontRenderer: Failed to load font '{fontName}': {ex.Message}");
            }
        }
        public float GetStringWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            using (var font = new Font(_fontName, _baseSize, FontStyle.Bold))
            using (var bitmap = new Bitmap(1, 1))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                using (StringFormat format = StringFormat.GenericTypographic)
                {
                    format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                    return graphics.MeasureString(text, font, 0, format).Width;
                }
            }
        }
        public CharacterData GetCharacterData(char c)
        {
            if (!_characterData.ContainsKey(c))
            {
                //Console.WriteLine($"SystemFontRenderer: Character '{c}' not found, using space as fallback.");
                return _characterData[' '];
            }
            return _characterData[c];
        }
        public uint GetCharacterTexture(char c)
        {
            if (!_charTextures.ContainsKey(c))
            {
                return _charTextures[' '];
            }
            return _charTextures[c];
        }
    }
    public class CharacterData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] PixelData { get; set; }
    }
}