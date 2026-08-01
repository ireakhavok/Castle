// Folder: SiegeEngine.Core.Rendering
// File: SystemFontRenderer.cs
using System;
using System.Drawing;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using SiegeEngine.Core.Rendering.ContextManagement;

namespace SiegeEngine.Core.Rendering
{
    public class SystemFontRenderer
    {
        private readonly Dictionary<char, CharacterData> _characterData;
        private readonly Dictionary<char, uint> _charTextures;
        private readonly Dictionary<char, float> _advances;
        private readonly IRenderContext _renderContext;
        private readonly string _fontName;
        private readonly float _baseSize = 100.0f;

        public float BaseSize => _baseSize;
        public float LineHeight { get; private set; }

        public SystemFontRenderer(IRenderContext renderContext, string fontName)
        {
            _renderContext = renderContext;
            _fontName = fontName;
            _characterData = new Dictionary<char, CharacterData>();
            _charTextures = new Dictionary<char, uint>();
            _advances = new Dictionary<char, float>();
            LoadFontData(fontName);
        }

        private unsafe void LoadFontData(string fontName)
        {
            try
            {
                using (var font = new Font(fontName, _baseSize, FontStyle.Regular))
                using (var bitmap = new Bitmap(1, 1))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    LineHeight = font.Height;

                    // Full printable ASCII 32-126 + common single-code-unit IDE / C# / HTML glyphs
                    var chars = new List<char>();
                    for (int i = 32; i <= 126; i++)
                        chars.Add((char)i);

                    // Only characters that fit in a single UTF-16 code unit
                    chars.AddRange(new[]
                    {
                        '\u2026', // …
                        '\u2714', // ✔
                        '\u25CF', // ●
                        '\u25CB', // ○
                        '\u25B6', // ▶
                        '\u25BC', // ▼
                        '\u25C0', // ◀
                        '\u25B2', // ▲
                        '\u2753', // ❓
                        '\u00A0'  // non-breaking space
                    });

                    foreach (char c in chars.Distinct())
                    {
                        RasterizeCharacter(c, font, graphics);
                    }
                }
            }
            catch
            {
            }
        }

        private unsafe void RasterizeCharacter(char c, Font font, Graphics measureGraphics)
        {
            if (_characterData.ContainsKey(c)) return;

            string text = c.ToString();
            using (StringFormat format = StringFormat.GenericTypographic)
            {
                if (c == ' ' || c == '\u00A0')
                    format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                SizeF size = measureGraphics.MeasureString(text, font, 0, format);
                int width = Math.Max(1, (int)Math.Ceiling(size.Width));
                int height = Math.Max(1, (int)Math.Ceiling(size.Height));

                using (var charBitmap = new Bitmap(width, height))
                using (var charGraphics = Graphics.FromImage(charBitmap))
                {
                    charGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    charGraphics.Clear(Color.Transparent);
                    using (var brush = new SolidBrush(Color.White))
                    {
                        charGraphics.DrawString(text, font, brush, 0, 0, format);
                    }

                    var data = charBitmap.LockBits(
                        new Rectangle(0, 0, width, height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);

                    int bytesPerPixel = 4;
                    byte[] pixelData = new byte[width * height * bytesPerPixel];
                    nint ptr = data.Scan0;
                    for (int y = 0; y < height; y++)
                    {
                        nint row = nint.Add(ptr, y * data.Stride);
                        System.Runtime.InteropServices.Marshal.Copy(row, pixelData, y * width * bytesPerPixel, width * bytesPerPixel);
                    }
                    charBitmap.UnlockBits(data);

                    for (int i = 0; i < pixelData.Length; i += 4)
                    {
                        if (pixelData[i] > 0 || pixelData[i + 1] > 0 || pixelData[i + 2] > 0)
                            pixelData[i + 3] = 255;
                        else
                            pixelData[i + 3] = 0;
                    }

                    uint texture;
                    _renderContext.GenTextures(1, out texture);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, texture);
                    _renderContext.PixelStore(_renderContext.Enums.UnpackAlignment, 1);
                    fixed (byte* pixelPtr = pixelData)
                    {
                        _renderContext.TexImage2D(
                            _renderContext.Enums.Texture2D, 0,
                            _renderContext.Enums.InternalRgba,
                            (uint)width, (uint)height, 0,
                            _renderContext.Enums.PixelBgra,
                            _renderContext.Enums.UnsignedByte,
                            pixelPtr);
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
                    _advances[c] = size.Width;
                }
            }
        }

        public void EnsureCharacter(char c)
        {
            if (_characterData.ContainsKey(c)) return;
            try
            {
                using (var font = new Font(_fontName, _baseSize, FontStyle.Regular))
                using (var bitmap = new Bitmap(1, 1))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    RasterizeCharacter(c, font, graphics);
                }
            }
            catch
            {
            }
        }

        public float GetStringWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float width = 0f;
            foreach (char c in text)
            {
                if (!_advances.TryGetValue(c, out float adv))
                {
                    EnsureCharacter(c);
                    if (!_advances.TryGetValue(c, out adv))
                        adv = _advances.TryGetValue(' ', out float spaceAdv) ? spaceAdv : 0f;
                }
                width += adv;
            }
            return width;
        }

        public float GetAdvance(char c)
        {
            if (_advances.TryGetValue(c, out float adv)) return adv;
            EnsureCharacter(c);
            if (_advances.TryGetValue(c, out adv)) return adv;
            return _advances.TryGetValue(' ', out float spaceAdv) ? spaceAdv : 0f;
        }

        public CharacterData GetCharacterData(char c)
        {
            if (!_characterData.ContainsKey(c))
            {
                EnsureCharacter(c);
                if (!_characterData.ContainsKey(c))
                    return _characterData[' '];
            }
            return _characterData[c];
        }

        public uint GetCharacterTexture(char c)
        {
            if (!_charTextures.ContainsKey(c))
            {
                EnsureCharacter(c);
                if (!_charTextures.ContainsKey(c))
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