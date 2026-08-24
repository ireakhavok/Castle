// Folder: SiegeEngine.Core.GPU
// File: TextRenderer.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe class TextRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly nint _window;
        private uint _textVao, _textVbo;
        private ShaderProgram _shaderProgram;
        private Dictionary<string, SystemFontRenderer> _fontRenderers = new Dictionary<string, SystemFontRenderer>();
        private SystemFontRenderer _defaultFontRenderer;

        // ------------------------------------------------------------------
        // Simple multi-page glyph atlas
        // ------------------------------------------------------------------
        private const int AtlasSize = 1024;
        private const int AtlasPadding = 1;

        private class AtlasPage
        {
            public uint TextureId;
            public int CursorX;
            public int CursorY;
            public int RowHeight;
            public byte[] Pixels; // RGBA
        }

        private class AtlasEntry
        {
            public int PageIndex;
            public float U0, V0, U1, V1;
            public int Width, Height;
        }

        private readonly List<AtlasPage> _atlasPages = new List<AtlasPage>();
        // key = fontFamily|glyph
        private readonly Dictionary<string, AtlasEntry> _atlasLookup = new Dictionary<string, AtlasEntry>();

        private class GlyphInstance
        {
            public float LocalX;
            public float Width;
            public float Height;
            public float U0, V0, U1, V1;
            public int PageIndex;
        }

        private class CachedGlyphRun
        {
            public List<GlyphInstance> Glyphs = new List<GlyphInstance>();
            public float TotalWidth;
            public float LineHeight;
        }

        private readonly Dictionary<string, CachedGlyphRun> _glyphRunCache = new Dictionary<string, CachedGlyphRun>();
        private readonly LinkedList<string> _lruOrder = new LinkedList<string>();
        private const int MaxCacheEntries = 2048;

        // Scratch buffer reused for batched vertices (pos.xy + uv.xy)
        private float[] _batchVerts = new float[4096];
        private int _batchVertCount;

        public TextRenderer(IRenderContext renderContext, nint window)
        {
            _renderContext = renderContext;
            _window = window;
            _defaultFontRenderer = new SystemFontRenderer(_renderContext, "Arial");
            _fontRenderers["Arial"] = _defaultFontRenderer;
            CreateAtlasPage();
        }

        private void CreateAtlasPage()
        {
            var page = new AtlasPage
            {
                CursorX = AtlasPadding,
                CursorY = AtlasPadding,
                RowHeight = 0,
                Pixels = new byte[AtlasSize * AtlasSize * 4]
            };
            _renderContext.GenTextures(1, out page.TextureId);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, page.TextureId);
            _renderContext.PixelStore(_renderContext.Enums.UnpackAlignment, 1);
            fixed (byte* ptr = page.Pixels)
            {
                _renderContext.TexImage2D(
                    _renderContext.Enums.Texture2D, 0,
                    _renderContext.Enums.InternalRgba,
                    (uint)AtlasSize, (uint)AtlasSize, 0,
                    _renderContext.Enums.PixelRgba,
                    _renderContext.Enums.UnsignedByte,
                    ptr);
            }
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapS, _renderContext.Enums.ClampToEdge);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapT, _renderContext.Enums.ClampToEdge);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _atlasPages.Add(page);
        }

        private AtlasEntry PackGlyph(string fontFamily, string glyph, CharacterData data)
        {
            string key = fontFamily + "|" + glyph;
            if (_atlasLookup.TryGetValue(key, out var existing))
                return existing;

            int w = data.Width;
            int h = data.Height;
            if (w <= 0 || h <= 0)
            {
                // empty glyph – return a zero-size entry on page 0
                var empty = new AtlasEntry { PageIndex = 0, U0 = 0, V0 = 0, U1 = 0, V1 = 0, Width = 0, Height = 0 };
                _atlasLookup[key] = empty;
                return empty;
            }

            AtlasPage page = _atlasPages[_atlasPages.Count - 1];

            // New row if needed
            if (page.CursorX + w + AtlasPadding > AtlasSize)
            {
                page.CursorX = AtlasPadding;
                page.CursorY += page.RowHeight + AtlasPadding;
                page.RowHeight = 0;
            }

            // New page if needed
            if (page.CursorY + h + AtlasPadding > AtlasSize)
            {
                CreateAtlasPage();
                page = _atlasPages[_atlasPages.Count - 1];
            }

            int x = page.CursorX;
            int y = page.CursorY;

            // Copy pixels (BGRA from SystemFontRenderer → RGBA atlas)
            byte[] src = data.PixelData;
            for (int row = 0; row < h; row++)
            {
                int srcOff = row * w * 4;
                int dstOff = ((y + row) * AtlasSize + x) * 4;
                for (int col = 0; col < w; col++)
                {
                    // SystemFontRenderer stores BGRA
                    page.Pixels[dstOff + 0] = src[srcOff + 2]; // R
                    page.Pixels[dstOff + 1] = src[srcOff + 1]; // G
                    page.Pixels[dstOff + 2] = src[srcOff + 0]; // B
                    page.Pixels[dstOff + 3] = src[srcOff + 3]; // A
                    srcOff += 4;
                    dstOff += 4;
                }
            }

            // Upload the dirty rectangle
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, page.TextureId);
            _renderContext.PixelStore(_renderContext.Enums.UnpackAlignment, 1);
            fixed (byte* ptr = &page.Pixels[(y * AtlasSize + x) * 4])
            {
                // We upload row-by-row because the source is not contiguous for the full width
                for (int row = 0; row < h; row++)
                {
                    fixed (byte* rowPtr = &page.Pixels[((y + row) * AtlasSize + x) * 4])
                    {
                        _renderContext.TexImage2D(
                            _renderContext.Enums.Texture2D, 0,
                            _renderContext.Enums.InternalRgba,
                            (uint)AtlasSize, (uint)AtlasSize, 0,
                            _renderContext.Enums.PixelRgba,
                            _renderContext.Enums.UnsignedByte,
                            null); // ensure texture exists
                        // Use the full-page upload for simplicity and correctness on this path
                    }
                }
            }
            // Full-page re-upload (simple, correct, and still far cheaper than per-glyph draw calls)
            fixed (byte* fullPtr = page.Pixels)
            {
                _renderContext.TexImage2D(
                    _renderContext.Enums.Texture2D, 0,
                    _renderContext.Enums.InternalRgba,
                    (uint)AtlasSize, (uint)AtlasSize, 0,
                    _renderContext.Enums.PixelRgba,
                    _renderContext.Enums.UnsignedByte,
                    fullPtr);
            }
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);

            page.CursorX += w + AtlasPadding;
            page.RowHeight = Math.Max(page.RowHeight, h);

            var entry = new AtlasEntry
            {
                PageIndex = _atlasPages.Count - 1,
                Width = w,
                Height = h,
                U0 = (float)x / AtlasSize,
                V0 = (float)y / AtlasSize,
                U1 = (float)(x + w) / AtlasSize,
                V1 = (float)(y + h) / AtlasSize
            };
            _atlasLookup[key] = entry;
            return entry;
        }

        public void Initialize(ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;
            _renderContext.GenVertexArrays(1, out _textVao);
            _renderContext.GenBuffers(1, out _textVbo);
            _renderContext.BindVertexArray(_textVao);
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _textVbo);
            // Allocate a reasonably large dynamic buffer up front
            _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(_batchVerts.Length * sizeof(float)), null, _renderContext.Enums.DynamicDraw);
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
        }

        public void ClearCache()
        {
            _glyphRunCache.Clear();
            _lruOrder.Clear();
        }

        private string GetCacheKey(string text, float fontSize, string fontFamily)
        {
            return $"{text ?? ""}|{fontSize:F3}|{fontFamily ?? "Arial"}";
        }

        private void TouchLRU(string key)
        {
            if (_lruOrder.Contains(key))
                _lruOrder.Remove(key);
            _lruOrder.AddLast(key);
            while (_glyphRunCache.Count > MaxCacheEntries)
            {
                var oldest = _lruOrder.First.Value;
                _lruOrder.RemoveFirst();
                _glyphRunCache.Remove(oldest);
            }
        }

        public Vector2 GetTextSize(string text, float fontSize, string fontFamily = "Arial")
        {
            var key = GetCacheKey(text, fontSize, fontFamily);
            if (_glyphRunCache.TryGetValue(key, out var cached))
            {
                TouchLRU(key);
                return new Vector2(cached.TotalWidth, cached.LineHeight);
            }
            var renderer = GetFontRenderer(fontFamily);
            float scale = fontSize / renderer.BaseSize;
            if (string.IsNullOrEmpty(text)) return Vector2.Zero;
            float width = renderer.GetStringWidth(text) * scale;
            float height = 0;
            foreach (var (glyph, _) in SystemFontRenderer.EnumerateGlyphs(text))
            {
                if (string.IsNullOrWhiteSpace(glyph)) continue;
                var data = renderer.GetCharacterData(glyph);
                height = Math.Max(height, data.Height * scale);
            }
            return new Vector2(width, height);
        }

        public float GetLineHeight(float fontSize, string fontFamily = "Arial")
        {
            var renderer = GetFontRenderer(fontFamily);
            float scale = fontSize / renderer.BaseSize;
            return renderer.LineHeight * scale;
        }

        public void RenderText(string text, float startX, float startY, float viewportWidth, float viewportHeight, float fontSize = 12.0f, Vector4? textColor = null, string fontFamily = "Arial")
        {
            RenderText(text, startX, startY, viewportWidth, viewportHeight, fontSize, textColor, fontFamily, Matrix4x4.Identity);
        }

        public void RenderText(string text, float startX, float startY, float viewportWidth, float viewportHeight, float fontSize, Vector4? textColor, string fontFamily, Matrix4x4 transformMatrix)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.Replace("\n", " ").Replace("\r", " ");
            var key = GetCacheKey(text, fontSize, fontFamily);
            if (!_glyphRunCache.TryGetValue(key, out var run))
            {
                run = BuildCachedGlyphRun(text, fontSize, fontFamily);
                _glyphRunCache[key] = run;
            }
            TouchLRU(key);
            RenderCachedGlyphRun(run, startX, startY, viewportWidth, viewportHeight, textColor ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f), transformMatrix);
        }

        private CachedGlyphRun BuildCachedGlyphRun(string text, float fontSize, string fontFamily)
        {
            var run = new CachedGlyphRun();
            var renderer = GetFontRenderer(fontFamily);
            float scale = fontSize / renderer.BaseSize;
            run.LineHeight = renderer.LineHeight * scale;
            float currentX = 0f;

            foreach (var (glyph, _) in SystemFontRenderer.EnumerateGlyphs(text))
            {
                if (glyph == "\n" || glyph == "\r") continue;
                var data = renderer.GetCharacterData(glyph);
                if (data == null) continue;

                var entry = PackGlyph(fontFamily, glyph, data);
                float charWidth = data.Width * scale;
                float charHeight = data.Height * scale;

                run.Glyphs.Add(new GlyphInstance
                {
                    LocalX = currentX,
                    Width = charWidth,
                    Height = charHeight,
                    U0 = entry.U0,
                    V0 = entry.V0,
                    U1 = entry.U1,
                    V1 = entry.V1,
                    PageIndex = entry.PageIndex
                });

                float advance = renderer.GetAdvance(glyph) * scale;
                currentX += advance;
            }
            run.TotalWidth = currentX;
            return run;
        }

        private void RenderCachedGlyphRun(CachedGlyphRun run, float startX, float startY, float viewportWidth, float viewportHeight, Vector4 color, Matrix4x4 transformMatrix)
        {
            if (run.Glyphs.Count == 0) return;

            _shaderProgram.Use();
            _shaderProgram.SetUniform("uUseTexture", 1.0f);
            _shaderProgram.SetMatrix4("uTransform", Matrix4x4.Identity);
            _shaderProgram.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
            _shaderProgram.SetUniform("uTexture", 0);

            // Group by atlas page (almost always a single page)
            int currentPage = -1;
            _batchVertCount = 0;

            void Flush()
            {
                if (_batchVertCount == 0) return;
                if (currentPage < 0 || currentPage >= _atlasPages.Count) return;

                _renderContext.BindVertexArray(_textVao);
                _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _textVbo);

                // Grow scratch buffer if needed
                if (_batchVertCount > _batchVerts.Length)
                {
                    int newSize = Math.Max(_batchVerts.Length * 2, _batchVertCount);
                    Array.Resize(ref _batchVerts, newSize);
                    _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(newSize * sizeof(float)), null, _renderContext.Enums.DynamicDraw);
                }

                fixed (float* ptr = _batchVerts)
                {
                    _renderContext.BufferSubData(_renderContext.Enums.ArrayBuffer, 0, (uint)(_batchVertCount * sizeof(float)), ptr);
                }

                _renderContext.EnableVertexAttribArray(0);
                _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)0);
                _renderContext.EnableVertexAttribArray(1);
                _renderContext.VertexAttribPointer(1, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

                _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, _atlasPages[currentPage].TextureId);

                // 6 vertices per quad (two triangles)
                int quadCount = _batchVertCount / 24; // 6 verts * 4 floats
                _renderContext.DrawArrays(_renderContext.Enums.Triangles, 0, (uint)(quadCount * 6));

                _batchVertCount = 0;
            }

            for (int i = 0; i < run.Glyphs.Count; i++)
            {
                var g = run.Glyphs[i];
                if (g.Width <= 0 || g.Height <= 0) continue;

                if (g.PageIndex != currentPage)
                {
                    Flush();
                    currentPage = g.PageIndex;
                }

                float x = startX + g.LocalX;
                float y = startY;
                float[] ndc = HtmlLayoutUtils.GetNdcQuad(x, y, g.Width, g.Height, transformMatrix, viewportWidth, viewportHeight);

                // Ensure room for 24 floats (6 verts * 4)
                if (_batchVertCount + 24 > _batchVerts.Length)
                {
                    int newSize = _batchVerts.Length * 2;
                    Array.Resize(ref _batchVerts, newSize);
                }

                // Triangle 1: bl, br, tr
                // Triangle 2: bl, tr, tl
                // ndc = {blx,bly, brx,bry, trx,try, tlx,tly}
                // UV:   bl=(u0,v1), br=(u1,v1), tr=(u1,v0), tl=(u0,v0)  (V flipped for top-left origin of atlas)

                // bl
                _batchVerts[_batchVertCount++] = ndc[0];
                _batchVerts[_batchVertCount++] = ndc[1];
                _batchVerts[_batchVertCount++] = g.U0;
                _batchVerts[_batchVertCount++] = g.V1;
                // br
                _batchVerts[_batchVertCount++] = ndc[2];
                _batchVerts[_batchVertCount++] = ndc[3];
                _batchVerts[_batchVertCount++] = g.U1;
                _batchVerts[_batchVertCount++] = g.V1;
                // tr
                _batchVerts[_batchVertCount++] = ndc[4];
                _batchVerts[_batchVertCount++] = ndc[5];
                _batchVerts[_batchVertCount++] = g.U1;
                _batchVerts[_batchVertCount++] = g.V0;

                // bl
                _batchVerts[_batchVertCount++] = ndc[0];
                _batchVerts[_batchVertCount++] = ndc[1];
                _batchVerts[_batchVertCount++] = g.U0;
                _batchVerts[_batchVertCount++] = g.V1;
                // tr
                _batchVerts[_batchVertCount++] = ndc[4];
                _batchVerts[_batchVertCount++] = ndc[5];
                _batchVerts[_batchVertCount++] = g.U1;
                _batchVerts[_batchVertCount++] = g.V0;
                // tl
                _batchVerts[_batchVertCount++] = ndc[6];
                _batchVerts[_batchVertCount++] = ndc[7];
                _batchVerts[_batchVertCount++] = g.U0;
                _batchVerts[_batchVertCount++] = g.V0;
            }

            Flush();

            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _renderContext.BindVertexArray(0);
        }

        private SystemFontRenderer GetFontRenderer(string fontFamily)
        {
            if (_fontRenderers.TryGetValue(fontFamily, out var renderer))
                return renderer;
            try { renderer = new SystemFontRenderer(_renderContext, fontFamily); }
            catch { renderer = _defaultFontRenderer; }
            _fontRenderers[fontFamily] = renderer;
            return renderer;
        }

        public void Dispose()
        {
            ClearCache();
            foreach (var page in _atlasPages)
            {
                if (page.TextureId != 0)
                    _renderContext.DeleteTexture(page.TextureId);
            }
            _atlasPages.Clear();
            _atlasLookup.Clear();
            _fontRenderers.Clear();
            _renderContext.DeleteVertexArray(_textVao);
            _renderContext.DeleteBuffer(_textVbo);
        }
    }
}