using System;
using System.Drawing;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;

namespace SiegeEngine.Rendering
{
    public class SystemFontRenderer
    {
        private readonly Dictionary<char, CharacterData> _characterData;

        public SystemFontRenderer(string fontName, float fontSize = 12.0f)
        {
            _characterData = new Dictionary<char, CharacterData>();
            LoadFontData(fontName, fontSize);
        }

        private void LoadFontData(string fontName, float fontSize)
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
                        SizeF size = graphics.MeasureString(text, font);
                        int width = (int)Math.Ceiling(size.Width);
                        int height = (int)Math.Ceiling(size.Height);

                        using (var charBitmap = new Bitmap(width, height))
                        using (var charGraphics = Graphics.FromImage(charBitmap))
                        {
                            charGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                            charGraphics.Clear(Color.Transparent);
                            using (var brush = new SolidBrush(Color.White))
                            {
                                charGraphics.DrawString(text, font, brush, 0, 0);
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

                            _characterData[c] = new CharacterData
                            {
                                Width = width,
                                Height = height,
                                PixelData = pixelData
                            };
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

        public CharacterData GetCharacterData(char c)
        {
            if (!_characterData.ContainsKey(c))
            {
                //Console.WriteLine($"SystemFontRenderer: Character '{c}' not found, using space as fallback.");
                return _characterData[' '];
            }
            return _characterData[c];
        }
    }

    public class CharacterData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] PixelData { get; set; }
    }
}