using System;
using System.Numerics;
using System.Collections.Generic;
using SiegeEngine.ContextManagement;

namespace SiegeEngine.Rendering
{
    public unsafe class TextRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly IntPtr _window;
        private uint _textVao, _textVbo;
        private ShaderProgram _shaderProgram;
        private Dictionary<char, uint> _charTextures;
        private SystemFontRenderer _fontRenderer;
        public TextRenderer(IRenderContext renderContext, IntPtr window)
        {
            _renderContext = renderContext;
            _window = window;
            _charTextures = new Dictionary<char, uint>();
            _fontRenderer = new SystemFontRenderer("Arial");
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
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(textVertices.Length * sizeof(float)), ptr, _renderContext.Enums.DynamicDraw);
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
            string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 :.,!?-+()[]{}x";
            foreach (char c in characters)
            {
                var charData = _fontRenderer.GetCharacterData(c);
                if (charData == null || charData.PixelData == null || charData.PixelData.Length == 0)
                {
                    Console.WriteLine($"TextRenderer: Failed to load character '{c}' - charData is null or PixelData empty.");
                    continue;
                }
                uint texture;
                _renderContext.GenTextures(1, out texture);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, texture);
                _renderContext.PixelStore(_renderContext.Enums.UnpackAlignment, 1);
                fixed (byte* pixelPtr = charData.PixelData)
                {
                    _renderContext.TexImage2D(_renderContext.Enums.Texture2D, 0, _renderContext.Enums.InternalRgba, (uint)charData.Width, (uint)charData.Height, 0, _renderContext.Enums.PixelBgra, _renderContext.Enums.UnsignedByte, pixelPtr);
                }
                int error = _renderContext.GetError();
                if (error != _renderContext.Enums.NoError)
                {
                    Console.WriteLine($"TextRenderer: OpenGL error after loading texture for '{c}': {error}");
                }
                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Linear);
                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Linear);
                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapS, _renderContext.Enums.ClampToEdge);
                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapT, _renderContext.Enums.ClampToEdge);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
                _charTextures[c] = texture;
                //Console.WriteLine($"TextRenderer: Loaded texture for character '{c}': {texture}");
            }
            //Console.WriteLine($"TextRenderer: Initialization complete. Loaded {_charTextures.Count} characters.");
        }
        public void RenderText(string text, float startX, float startY, int width, int height, float fontSize = 12.0f, Vector4? textColor = null)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (width <= 0 || height <= 0)
            {
                width = 1280;
                height = 720;
            }
            // Render black outline (2px)
            float[] offsets = { -1.5f, -1.0f, 1.0f, 1.5f };
            for (int i = 0; i < offsets.Length; i++)
            {
                for (int j = 0; j < offsets.Length; j++)
                {
                    float offsetX = offsets[i];
                    float offsetY = offsets[j];
                    RenderTextPass(text, startX + offsetX, startY + offsetY, width, height, fontSize, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                }
            }
            // Render white text
            RenderTextPass(text, startX, startY, width, height, fontSize, textColor ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
        }
        private void RenderTextPass(string text, float startX, float startY, int width, int height, float fontSize, Vector4 color)
        {
            float currentX = startX;
            float spacing = -2.0f;
            Matrix4x4 transform = Matrix4x4.Identity;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!_charTextures.ContainsKey(c))
                {
                    Console.WriteLine($"TextRenderer: Character '{c}' not found, using space as fallback.");
                    c = ' ';
                }
                var charData = _fontRenderer.GetCharacterData(c);
                if (charData == null)
                {
                    Console.WriteLine($"TextRenderer: Character data for '{c}' is null.");
                    continue;
                }
                float charWidth = charData.Width * (fontSize / 12.0f);
                float charHeight = charData.Height * (fontSize / 12.0f);
                float charLeft = 2.0f * currentX / width - 1.0f;
                float charRight = 2.0f * (currentX + charWidth) / width - 1.0f;
                float charTop = 1.0f - 2.0f * startY / height;
                float charBottom = 1.0f - 2.0f * (startY + charHeight) / height;
                float[] textVertices = new float[]
                {
                    charLeft, charBottom, 0.0f, 1.0f,
                    charRight, charBottom, 1.0f, 1.0f,
                    charRight, charTop, 1.0f, 0.0f,
                    charLeft, charTop, 0.0f, 0.0f
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
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, _charTextures[c]);
                _shaderProgram.SetUniform("uTexture", 0);
                _shaderProgram.SetUniform("uUseTexture", 1.0f);
                _shaderProgram.SetMatrix4("uTransform", transform);
                _shaderProgram.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
                _renderContext.DrawArrays(_renderContext.Enums.TriangleFan, 0, 4);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
                _renderContext.BindVertexArray(0);
                currentX += charWidth + spacing;
                //Console.WriteLine($"TextRenderer: Rendered char '{c}' at ({charLeft:F3}, {charTop:F3}) to ({charRight:F3}, {charBottom:F3}), Texture: {_charTextures[c]}, Unit: Texture0");
            }
        }
        public void Dispose()
        {
            _renderContext.DeleteVertexArray(_textVao);
            _renderContext.DeleteBuffer(_textVbo);
            foreach (var texture in _charTextures.Values)
            {
                _renderContext.DeleteTexture(texture);
            }
        }
    }
}