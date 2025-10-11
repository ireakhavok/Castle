// Folder: SiegeEngine.Rendering
// File: TextRenderer.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.UI;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.Rendering
{
    public unsafe class TextRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly IntPtr _window;
        private uint _textVao, _textVbo;
        private ShaderProgram _shaderProgram;
        private Dictionary<string, SystemFontRenderer> _fontRenderers = new Dictionary<string, SystemFontRenderer>();
        private SystemFontRenderer _defaultFontRenderer;
        public TextRenderer(IRenderContext renderContext, IntPtr window)
        {
            _renderContext = renderContext;
            _window = window;
            _defaultFontRenderer = new SystemFontRenderer(_renderContext, "Arial");
            _fontRenderers["Arial"] = _defaultFontRenderer;
        }
        public void Initialize(ShaderProgram shaderProgram)
        {
            //Console.WriteLine("TextRenderer: Initializing with font 'Arial', size 12.0f");
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
            //Console.WriteLine("TextRenderer: Text VAO and VBO initialized.");
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
        }
        public Vector2 GetTextSize(string text, float fontSize, string fontFamily = "Arial")
        {
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
        public void RenderText(string text, float startX, float startY, float viewportWidth, float viewportHeight, float fontSize = 12.0f, Vector4? textColor = null, string fontFamily = "Arial")
        {
            RenderText(text, startX, startY, viewportWidth, viewportHeight, fontSize, textColor, fontFamily, Matrix4x4.Identity);
        }
        public void RenderText(string text, float startX, float startY, float viewportWidth, float viewportHeight, float fontSize, Vector4? textColor, string fontFamily, Matrix4x4 transformMatrix)
        {
            if (string.IsNullOrEmpty(text))
                return;
            text = text.Replace("\n", " ").Replace("\r", " "); // Avoid non-printable
            // Render black outline (2px)
            float[] offsets = { -0.5f, 0.5f };
            for (int i = 0; i < offsets.Length; i++)
            {
                for (int j = 0; j < offsets.Length; j++)
                {
                    float offsetX = offsets[i];
                    float offsetY = offsets[j];
                    RenderTextPass(text, startX + offsetX, startY + offsetY, viewportWidth, viewportHeight, fontSize, new Vector4(0.0f, 0.0f, 0.0f, 1.0f), fontFamily, transformMatrix);
                }
            }
            // Render white text
            RenderTextPass(text, startX, startY, viewportWidth, viewportHeight, fontSize, textColor ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f), fontFamily, transformMatrix);
        }
        private void RenderTextPass(string text, float startX, float startY, float viewportWidth, float viewportHeight, float fontSize, Vector4 color, string fontFamily, Matrix4x4 transformMatrix)
        {
            _shaderProgram.Use();
            var renderer = GetFontRenderer(fontFamily);
            float scale = fontSize / renderer.BaseSize;
            float currentX = startX;
            Matrix4x4 trans = transformMatrix;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r') continue; // Skip non-printable
                var data = renderer.GetCharacterData(c);
                if (data == null)
                {
                    Console.WriteLine($"TextRenderer: Character data for '{c}' is null.");
                    continue;
                }
                float charWidth = data.Width * scale;
                float charHeight = data.Height * scale;
                if (!char.IsWhiteSpace(c))
                {
                    float x = currentX;
                    float y = startY;
                    float[] ndc = HtmlElement.GetNdcQuad(x, y, charWidth, charHeight, trans, viewportWidth, viewportHeight);
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
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, renderer.GetCharacterTexture(c));
                    _shaderProgram.SetUniform("uTexture", 0);
                    _shaderProgram.SetUniform("uUseTexture", 1.0f);
                    _shaderProgram.SetMatrix4("uTransform", Matrix4x4.Identity);
                    _shaderProgram.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
                    _renderContext.DrawArrays(_renderContext.Enums.TriangleFan, 0, 4);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
                    _renderContext.BindVertexArray(0);
                }
                float m_a = renderer.GetStringWidth(c.ToString());
                float advance = m_a * scale;
                if (i < text.Length - 1)
                {
                    char next = text[i + 1];
                    float m_b = renderer.GetStringWidth(next.ToString());
                    float m_ab = renderer.GetStringWidth(c.ToString() + next.ToString());
                    float kerning = (m_ab - m_a - m_b) * scale;
                    advance += kerning;
                }
                currentX += advance;
                //Console.WriteLine($"TextRenderer: Rendered char '{c}' at ({charLeft:F3}, {charTop:F3}) to ({charRight:F3}, {charBottom:F3}), Texture: {_charTextures[c]}, Unit: Texture0");
            }
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
            _renderContext.DeleteVertexArray(_textVao);
            _renderContext.DeleteBuffer(_textVbo);
        }
    }
}