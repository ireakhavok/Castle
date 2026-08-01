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
        // Keyed by the full glyph string (1 char for BMP, 2 chars for surrogate-pair emoji)
        private readonly Dictionary<string, CharacterData> _characterData;
        private readonly Dictionary<string, uint> _charTextures;
        private readonly Dictionary<string, float> _advances;
        private readonly IRenderContext _renderContext;
        private readonly string _fontName;
        private readonly float _baseSize = 100.0f;

        public float BaseSize => _baseSize;
        public float LineHeight { get; private set; }

        public SystemFontRenderer(IRenderContext renderContext, string fontName)
        {
            _renderContext = renderContext;
            _fontName = fontName;
            _characterData = new Dictionary<string, CharacterData>();
            _charTextures = new Dictionary<string, uint>();
            _advances = new Dictionary<string, float>();
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

                    // Full printable ASCII 32-126
                    var glyphs = new List<string>();
                    for (int i = 32; i <= 126; i++)
                        glyphs.Add(((char)i).ToString());

                    // Common single-code-unit symbols
                    glyphs.AddRange(new[]
                    {
                        "\u2026", // …
                        "\u2714", // ✔
                        "\u25CF", // ●
                        "\u25CB", // ○
                        "\u25B6", // ▶
                        "\u25BC", // ▼
                        "\u25C0", // ◀
                        "\u25B2", // ▲
                        "\u2753", // ❓
                        "\u00A0"  // non-breaking space
                    });

                    // Real emoji used by FileSelectorPanel / AssetBrowserPanel (surrogate pairs)
                    glyphs.AddRange(new[]
                    {
                        "📁", "📄", "📦", "🖼️", "🎵"
                    });

                    foreach (string g in glyphs.Distinct())
                    {
                        RasterizeGlyph(g, font, graphics);
                    }
                }
            }
            catch
            {
            }
        }

        private unsafe void RasterizeGlyph(string glyph, Font font, Graphics measureGraphics)
        {
            if (string.IsNullOrEmpty(glyph) || _characterData.ContainsKey(glyph)) return;

            using (StringFormat format = StringFormat.GenericTypographic)
            {
                if (glyph == " " || glyph == "\u00A0")
                    format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                SizeF size = measureGraphics.MeasureString(glyph, font, 0, format);
                int width = Math.Max(1, (int)Math.Ceiling(size.Width));
                int height = Math.Max(1, (int)Math.Ceiling(size.Height));

                using (var charBitmap = new Bitmap(width, height))
                using (var charGraphics = Graphics.FromImage(charBitmap))
                {
                    charGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    charGraphics.Clear(Color.Transparent);
                    using (var brush = new SolidBrush(Color.White))
                    {
                        charGraphics.DrawString(glyph, font, brush, 0, 0, format);
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

                    _charTextures[glyph] = texture;
                    _characterData[glyph] = new CharacterData
                    {
                        Width = width,
                        Height = height,
                        PixelData = pixelData
                    };
                    _advances[glyph] = size.Width;
                }
            }
        }

        public void EnsureCharacter(string glyph)
        {
            if (string.IsNullOrEmpty(glyph) || _characterData.ContainsKey(glyph)) return;
            try
            {
                using (var font = new Font(_fontName, _baseSize, FontStyle.Regular))
                using (var bitmap = new Bitmap(1, 1))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    RasterizeGlyph(glyph, font, graphics);
                }
            }
            catch
            {
            }
        }

        // Convenience for single BMP char
        public void EnsureCharacter(char c) => EnsureCharacter(c.ToString());

        public float GetStringWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float width = 0f;
            foreach (var (glyph, _) in EnumerateGlyphs(text))
            {
                if (!_advances.TryGetValue(glyph, out float adv))
                {
                    EnsureCharacter(glyph);
                    if (!_advances.TryGetValue(glyph, out adv))
                        adv = _advances.TryGetValue(" ", out float spaceAdv) ? spaceAdv : 0f;
                }
                width += adv;
            }
            return width;
        }

        public float GetAdvance(string glyph)
        {
            if (_advances.TryGetValue(glyph, out float adv)) return adv;
            EnsureCharacter(glyph);
            if (_advances.TryGetValue(glyph, out adv)) return adv;
            return _advances.TryGetValue(" ", out float spaceAdv) ? spaceAdv : 0f;
        }

        public float GetAdvance(char c) => GetAdvance(c.ToString());

        public CharacterData GetCharacterData(string glyph)
        {
            if (!_characterData.ContainsKey(glyph))
            {
                EnsureCharacter(glyph);
                if (!_characterData.ContainsKey(glyph))
                    return _characterData[" "];
            }
            return _characterData[glyph];
        }

        public CharacterData GetCharacterData(char c) => GetCharacterData(c.ToString());

        public uint GetCharacterTexture(string glyph)
        {
            if (!_charTextures.ContainsKey(glyph))
            {
                EnsureCharacter(glyph);
                if (!_charTextures.ContainsKey(glyph))
                    return _charTextures[" "];
            }
            return _charTextures[glyph];
        }

        public uint GetCharacterTexture(char c) => GetCharacterTexture(c.ToString());

        /// <summary>
        /// Yields logical glyphs: either a single BMP character or a full surrogate-pair emoji.
        /// </summary>
        public static IEnumerable<(string glyph, int length)> EnumerateGlyphs(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            for (int i = 0; i < text.Length;)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    yield return (text.Substring(i, 2), 2);
                    i += 2;
                }
                else
                {
                    yield return (text[i].ToString(), 1);
                    i++;
                }
            }
        }
    }

    public class CharacterData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] PixelData { get; set; }
    }
}