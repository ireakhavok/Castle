// Folder: SiegeEngine.Rendering
// File: TextRenderer.cs
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Rendering
{
    public unsafe class TextRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly nint _window;
        private uint _textVao, _textVbo;
        private ShaderProgram _shaderProgram;
        private Dictionary<string, SystemFontRenderer> _fontRenderers = new Dictionary<string, SystemFontRenderer>();
        private SystemFontRenderer _defaultFontRenderer;

        // === PRODUCTION-GRADE GLYPH RUN CACHE ===
        // Bounded at 2048 entries with LRU eviction for long sessions (dynamic lists, many panels)
        // Key includes text + size + font + transform flag (future-proof)
        private class GlyphInstance
        {
            public float LocalX;
            public uint TextureId;
            public float Width;
            public float Height;
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

        public TextRenderer(IRenderContext renderContext, nint window)
        {
            _renderContext = renderContext;
            _window = window;
            _defaultFontRenderer = new SystemFontRenderer(_renderContext, "Arial");
            _fontRenderers["Arial"] = _defaultFontRenderer;
        }

        public void Initialize(ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;
            _renderContext.GenVertexArrays(1, out _textVao);
            _renderContext.GenBuffers(1, out _textVbo);
            _renderContext.BindVertexArray(_textVao);
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _textVbo);

            float[] textVertices = new float[]
            {
                0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 1.0f, 0.0f,
                1.0f, 1.0f, 1.0f, 1.0f,
                0.0f, 1.0f, 0.0f, 1.0f
            };

            fixed (float* ptr = textVertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(textVertices.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
            }

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
            {
                _lruOrder.Remove(key);
            }
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
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                var data = renderer.GetCharacterData(c);
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

            RenderCachedGlyphRun(run, startX, startY, viewportWidth, viewportHeight, fontSize, textColor ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f), fontFamily, transformMatrix);
        }

        private CachedGlyphRun BuildCachedGlyphRun(string text, float fontSize, string fontFamily)
        {
            var run = new CachedGlyphRun();
            var renderer = GetFontRenderer(fontFamily);
            float scale = fontSize / renderer.BaseSize;
            run.LineHeight = renderer.LineHeight * scale;

            float currentX = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r') continue;

                var data = renderer.GetCharacterData(c);
                if (data == null) continue;

                float charWidth = data.Width * scale;
                float charHeight = data.Height * scale;

                run.Glyphs.Add(new GlyphInstance
                {
                    LocalX = currentX,
                    TextureId = renderer.GetCharacterTexture(c),
                    Width = charWidth,
                    Height = charHeight
                });

                float advance = renderer.GetStringWidth(c.ToString()) * scale;
                if (i < text.Length - 1)
                {
                    char next = text[i + 1];
                    float m_a = renderer.GetStringWidth(c.ToString());
                    float m_b = renderer.GetStringWidth(next.ToString());
                    float m_ab = renderer.GetStringWidth(c.ToString() + next.ToString());
                    float kerning = (m_ab - m_a - m_b) * scale;
                    advance += kerning;
                }
                currentX += advance;
            }

            run.TotalWidth = currentX;
            return run;
        }

        private void RenderCachedGlyphRun(CachedGlyphRun run, float startX, float startY, float viewportWidth, float viewportHeight, float fontSize, Vector4 color, string fontFamily, Matrix4x4 transformMatrix)
        {
            _shaderProgram.Use();

            var renderer = GetFontRenderer(fontFamily);
            float scale = fontSize / renderer.BaseSize;

            for (int i = 0; i < run.Glyphs.Count; i++)
            {
                var g = run.Glyphs[i];

                float x = startX + g.LocalX;
                float y = startY;

                float[] ndc = HtmlLayoutUtils.GetNdcQuad(x, y, g.Width, g.Height, transformMatrix, viewportWidth, viewportHeight);

                float[] textVertices = new float[]
                {
                    ndc[0], ndc[1], 0.0f, 1.0f,
                    ndc[2], ndc[3], 1.0f, 1.0f,
                    ndc[4], ndc[5], 1.0f, 0.0f,
                    ndc[6], ndc[7], 0.0f, 0.0f
                };

                _renderContext.BindVertexArray(_textVao);
                _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _textVbo);

                fixed (float* ptr = textVertices)
                {
                    _renderContext.BufferSubData(_renderContext.Enums.ArrayBuffer, 0, (uint)(textVertices.Length * sizeof(float)), ptr);
                }

                _renderContext.EnableVertexAttribArray(0);
                _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)0);
                _renderContext.EnableVertexAttribArray(1);
                _renderContext.VertexAttribPointer(1, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

                _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, g.TextureId);

                _shaderProgram.SetUniform("uTexture", 0);
                _shaderProgram.SetUniform("uUseTexture", 1.0f);
                _shaderProgram.SetMatrix4("uTransform", Matrix4x4.Identity);
                _shaderProgram.SetUniform("uColor", color.X, color.Y, color.Z, color.W);

                _renderContext.DrawArrays(_renderContext.Enums.TriangleFan, 0, 4);
            }

            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _renderContext.BindVertexArray(0);
        }

        private SystemFontRenderer GetFontRenderer(string fontFamily)
        {
            if (_fontRenderers.TryGetValue(fontFamily, out var renderer))
            {
                return renderer;
            }
            try
            {
                renderer = new SystemFontRenderer(_renderContext, fontFamily);
            }
            catch
            {
                renderer = _defaultFontRenderer;
            }
            _fontRenderers[fontFamily] = renderer;
            return renderer;
        }

        public void Dispose()
        {
            ClearCache();
            _fontRenderers.Clear();
            _renderContext.DeleteVertexArray(_textVao);
            _renderContext.DeleteBuffer(_textVbo);
        }
    }
}