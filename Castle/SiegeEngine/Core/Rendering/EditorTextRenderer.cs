// Folder: SiegeEngine
// File: EditorTextRenderer.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.Rendering.ContextManagement;
namespace SiegeEngine.Core.Rendering
{
    public unsafe class EditorTextRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly nint _window;
        private uint _textVao, _textVbo;
        private ShaderProgram _shaderProgram;
        private Dictionary<char, uint> _charTextures;
        private SystemFontRenderer _fontRenderer;
        public EditorTextRenderer(IRenderContext renderContext, nint window)
        {
            _renderContext = renderContext;
            _window = window;
            _charTextures = new Dictionary<char, uint>();
            _fontRenderer = new SystemFontRenderer(_renderContext, "Arial");
        }
        public void Initialize(ShaderProgram shaderProgram)
        {
            //Console.WriteLine("EditorTextRenderer: Initializing with font 'Arial', size 12.0f");
            _shaderProgram = shaderProgram;
            _renderContext.GenVertexArrays(1, out _textVao);
            _renderContext.GenBuffers(1, out _textVbo);
            _renderContext.BindVertexArray(_textVao);
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _textVbo);
            float[] textVertices = new float[]
            {
                0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 1.0f,
                1.0f, 0.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f,
                1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f
            };
            fixed (float* ptr = textVertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(textVertices.Length * sizeof(float)), ptr, _renderContext.Enums.DynamicDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 8 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, 8 * sizeof(float), (void*)(2 * sizeof(float)));
            _renderContext.EnableVertexAttribArray(2);
            _renderContext.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
            //Console.WriteLine($"EditorTextRenderer: Text VAO {_textVao} and VBO {_textVbo} initialized.");
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 :.,!?-+()[]{}x";
            foreach (char c in characters)
            {
                var charData = _fontRenderer.GetCharacterData(c);
                if (charData == null || charData.PixelData == null || charData.PixelData.Length == 0)
                {
                    //Console.WriteLine($"EditorTextRenderer: Failed to load character '{c}' - charData is null or PixelData empty.");
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
                    //Console.WriteLine($"EditorTextRenderer: OpenGL error after loading texture for '{c}': {error}");
                }
                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Linear);
                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Linear);
                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapS, _renderContext.Enums.ClampToEdge);
                _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapT, _renderContext.Enums.ClampToEdge);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
                _charTextures[c] = texture;
                //Console.WriteLine($"EditorTextRenderer: Loaded texture for character '{c}': {texture}, Width: {charData.Width}, Height: {charData.Height}, PixelData Length: {charData.PixelData.Length}");
                if (charData.PixelData.Length >= 4)
                {
                    //Console.WriteLine($"EditorTextRenderer: Sample pixels for '{c}' (BGRA): {charData.PixelData[0]:X2}-{charData.PixelData[1]:X2}-{charData.PixelData[2]:X2}-{charData.PixelData[3]:X2}...");
                }
            }
            //Console.WriteLine($"EditorTextRenderer: Initialization complete. Loaded {_charTextures.Count} characters.");
        }
        public void RenderText(string text, float startX, float startY, int width, int height, float fontSize = 16.0f, Vector4? textColor = null)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (width <= 0 || height <= 0)
            {
                width = 1280;
                height = 720;
            }
            float adjustedStartY = text == "Grid" ? startY : startY - 10;
            _renderContext.UseProgram(0);
            _shaderProgram.Use();
            //Console.WriteLine($"EditorTextRenderer: Rebound shader program {_shaderProgram.GetHashCode()} for text '{text}'");
            _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
            _renderContext.ColorMask(true, true, true, true);
            _renderContext.DepthMask(false);
            _shaderProgram.SetUniform("uUseTexture", 1.0f);
            _shaderProgram.SetUniform("uTexture", 0);
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
            _renderContext.Disable(_renderContext.Enums.ScissorTest);
            //Console.WriteLine($"EditorTextRenderer: Reset viewport to {width}x{height}, disabled scissor test for text '{text}'");
            bool isVao = _renderContext.IsVertexArray(_textVao);
            bool isVbo = _renderContext.IsBuffer(_textVbo);
            //Console.WriteLine($"EditorTextRenderer: VAO {_textVao} IsValid: {isVao}, VBO {_textVbo} IsValid: {isVbo} for text '{text}'");
            _renderContext.BindVertexArray(_textVao);
            //Console.WriteLine($"EditorTextRenderer: Rebound VAO {_textVao} for text '{text}'");
            RenderTextPass(text, startX, adjustedStartY, width, height, fontSize, textColor ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
        }
        private void RenderTextPass(string text, float startX, float startY, int width, int height, float fontSize, Vector4 color, bool useTexture = true)
        {
            float currentX = startX;
            float spacing = -2.0f;
            Matrix4x4 transform = Matrix4x4.Identity;
            _renderContext.UseProgram(0);
            _shaderProgram.Use();
            //Console.WriteLine($"EditorTextRenderer: Bound shader program {_shaderProgram.GetHashCode()} for text '{text}'");
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!_charTextures.ContainsKey(c))
                {
                    //Console.WriteLine($"EditorTextRenderer: Character '{c}' not found, using space as fallback.");
                    c = ' ';
                }
                var charData = _fontRenderer.GetCharacterData(c);
                if (charData == null)
                {
                    //Console.WriteLine($"EditorTextRenderer: Character data for '{c}' is null.");
                    continue;
                }
                float charWidth = charData.Width * (fontSize / 12.0f);
                float charHeight = charData.Height * (fontSize / 12.0f);
                float charLeft = currentX;
                float charRight = currentX + charWidth;
                float charTop = startY;
                float charBottom = startY + charHeight;
                float[] textVertices = new float[]
                {
                    charLeft, charBottom, color.X, color.Y, color.Z, color.W, 0.0f, 1.0f,
                    charRight, charBottom, color.X, color.Y, color.Z, color.W, 1.0f, 1.0f,
                    charRight, charTop, color.X, color.Y, color.Z, color.W, 1.0f, 0.0f,
                    charLeft, charTop, color.X, color.Y, color.Z, color.W, 0.0f, 0.0f
                };
                _renderContext.BindVertexArray(_textVao);
                _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _textVbo);
                //Console.WriteLine($"EditorTextRenderer: Bound VAO {_textVao}, VBO {_textVbo} for char '{c}'");
                for (uint j = 0; j < 16; j++)
                {
                    _renderContext.DisableVertexAttribArray(j);
                }
                _renderContext.EnableVertexAttribArray(0);
                _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 8 * sizeof(float), (void*)0);
                _renderContext.EnableVertexAttribArray(1);
                _renderContext.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, 8 * sizeof(float), (void*)(2 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(2);
                _renderContext.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
                //Console.WriteLine($"EditorTextRenderer: Reinitialized vertex attribs for char '{c}', VAO {_textVao}, VBO {_textVbo}");
                fixed (float* ptr = textVertices)
                {
                    _renderContext.BufferSubData(_renderContext.Enums.ArrayBuffer, 0, (uint)(textVertices.Length * sizeof(float)), ptr);
                }
                //Console.WriteLine($"EditorTextRenderer: Updated VBO data for char '{c}', Size: {textVertices.Length * sizeof(float)} bytes");
                if (useTexture)
                {
                    uint textureId = _charTextures[c];
                    bool isTextureValid = _renderContext.IsTexture(textureId);
                    if (!isTextureValid)
                    {
                        //Console.WriteLine($"EditorTextRenderer: Invalid texture {textureId} for char '{c}'");
                        _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
                    }
                    else
                    {
                        _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                        _renderContext.BindTexture(_renderContext.Enums.Texture2D, textureId);
                        //Console.WriteLine($"EditorTextRenderer: Bound texture {textureId} for char '{c}'");
                    }
                    _shaderProgram.SetUniform("uUseTexture", 1.0f);
                }
                else
                {
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
                    _shaderProgram.SetUniform("uUseTexture", 0.0f);
                    //Console.WriteLine($"EditorTextRenderer: No texture for char '{c}'");
                }
                _shaderProgram.SetMatrix4("uTransform", transform);
                //Console.WriteLine($"EditorTextRenderer: Set uniforms for char '{c}', uColor: ({color.X}, {color.Y}, {color.Z}, {color.W})");
                _renderContext.DrawArrays(_renderContext.Enums.TriangleFan, 0, 4);
                int error = _renderContext.GetError();
                if (error != _renderContext.Enums.NoError)
                {
                    //Console.WriteLine($"EditorTextRenderer: OpenGL error after drawing char '{c}': {error}");
                }
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
                _renderContext.BindVertexArray(0);
                _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, 0);
                currentX += charWidth + spacing;
                //Console.WriteLine($"EditorTextRenderer: Rendered char '{c}' at ({charLeft:F3}, {charTop:F3}) to ({charRight:F3}, {charBottom:F3}), Texture: {(_charTextures.ContainsKey(c) ? _charTextures[c] : 0)}, Unit: Texture0, UseTexture: {useTexture}, Shader: {_shaderProgram.GetHashCode()}");
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