// Folder: SiegeEngine.Core.GPU
// File: SystemFontRenderer.cs
using System;
using System.Drawing;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Renderers
{
    public class SystemFontRenderer
    {
        private readonly Dictionary<string, CharacterData> _characterData;
        private readonly Dictionary<string, uint> _charTextures;
        private readonly Dictionary<string, float> _advances;
        private readonly IRenderContext _renderContext;
        private readonly string _fontName;
        private readonly float _baseSize = 100.0f;

        // Fallback faces that actually contain emoji / symbol glyphs on Windows
        private static readonly string[] EmojiFallbackFaces = new[]
        {
            "Segoe UI Emoji",
            "Segoe UI Symbol",
            "Segoe UI",
            "Arial Unicode MS"
        };

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

                    var glyphs = new List<string>();
                    for (int i = 32; i <= 126; i++)
                        glyphs.Add(((char)i).ToString());

                    glyphs.AddRange(new[]
                    {
                        "\u2026", "\u2714", "\u25CF", "\u25CB",
                        "\u25B6", "\u25BC", "\u25C0", "\u25B2",
                        "\u2753", "\u00A0"
                    });

                    // Real emoji used by the IDE panels
                    glyphs.AddRange(new[]
                    {
                        "📁", "📄", "📦", "🖼️", "🎵", "❓"
                    });

                    foreach (string g in glyphs.Distinct())
                        RasterizeGlyph(g, graphics);
                }
            }
            catch
            {
            }
        }

        private Font GetFontForGlyph(string glyph)
        {
            // Prefer the requested face for ordinary BMP characters
            bool needsEmoji = glyph.Length > 1 || (glyph.Length == 1 && glyph[0] > 0xFFFF);
            // Also force emoji face for known symbol-range characters that Arial lacks
            if (!needsEmoji && glyph.Length == 1)
            {
                char c = glyph[0];
                if (c >= 0x2600 && c <= 0x27BF) needsEmoji = true; // misc symbols
                if (c >= 0x1F300 && c <= 0x1F9FF) needsEmoji = true; // (will be caught by Length>1)
            }

            if (!needsEmoji)
            {
                try { return new Font(_fontName, _baseSize, FontStyle.Regular); }
                catch { }
            }

            foreach (string face in EmojiFallbackFaces)
            {
                try
                {
                    var f = new Font(face, _baseSize, FontStyle.Regular);
                    // Quick test: if the family name matches we got a real font
                    if (string.Equals(f.Name, face, StringComparison.OrdinalIgnoreCase) ||
                        f.Name.IndexOf("Emoji", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        f.Name.IndexOf("Symbol", StringComparison.OrdinalIgnoreCase) >= 0)
                        return f;
                    f.Dispose();
                }
                catch { }
            }

            // Last resort
            return new Font(_fontName, _baseSize, FontStyle.Regular);
        }

        private unsafe void RasterizeGlyph(string glyph, Graphics measureGraphics)
        {
            if (string.IsNullOrEmpty(glyph) || _characterData.ContainsKey(glyph)) return;

            using (var font = GetFontForGlyph(glyph))
            using (StringFormat format = StringFormat.GenericTypographic)
            {
                if (glyph == " " || glyph == "\u00A0")
                    format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                SizeF size = measureGraphics.MeasureString(glyph, font, 0, format);
                int width = Math.Max(1, (int)Math.Ceiling(size.Width));
                int height = Math.Max(1, (int)Math.Ceiling(size.Height));

                // If the primary font produced a near-zero width, try an emoji face
                if (width <= 2 && glyph != " " && glyph != "\u00A0")
                {
                    font.Dispose();
                    using (var emojiFont = GetFontForGlyph(glyph)) // will prefer emoji faces
                    {
                        size = measureGraphics.MeasureString(glyph, emojiFont, 0, format);
                        width = Math.Max(1, (int)Math.Ceiling(size.Width));
                        height = Math.Max(1, (int)Math.Ceiling(size.Height));
                        RasterizeWithFont(glyph, emojiFont, format, width, height, size.Width);
                        return;
                    }
                }

                RasterizeWithFont(glyph, font, format, width, height, size.Width);
            }
        }

        private unsafe void RasterizeWithFont(string glyph, Font font, StringFormat format, int width, int height, float advance)
        {
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
                _advances[glyph] = advance;
            }
        }

        public void EnsureCharacter(string glyph)
        {
            if (string.IsNullOrEmpty(glyph) || _characterData.ContainsKey(glyph)) return;
            try
            {
                using (var bitmap = new Bitmap(1, 1))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    RasterizeGlyph(glyph, graphics);
                }
            }
            catch
            {
            }
        }

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