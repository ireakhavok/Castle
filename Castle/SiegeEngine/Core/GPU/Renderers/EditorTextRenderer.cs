// Folder: SiegeEngine
// File: EditorTextRenderer.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe class EditorTextRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly nint _window;
        private GpuHandle _textVbo;
        private GpuHandle _pipeline;
        private ShaderProgram _shaderProgram;
        private Dictionary<char, GpuHandle> _charTextures;
        private SystemFontRenderer _fontRenderer;
        public EditorTextRenderer(IRenderContext renderContext, nint window)
        {
            _renderContext = renderContext;
            _window = window;
            _charTextures = new Dictionary<char, GpuHandle>();
            _fontRenderer = new SystemFontRenderer(_renderContext, "Arial");
        }
        public void Initialize(ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;
            if (!_pipeline.IsValid)
                _pipeline = _renderContext.CreatePipeline(ShaderCatalog.Describe(ShaderId.Text, _renderContext));
            float[] textVertices = new float[6 * 8];
            _textVbo = _renderContext.CreateBuffer(new BufferDesc
            {
                Target = _renderContext.Enums.ArrayBuffer,
                Usage = _renderContext.Enums.DynamicDraw,
                ByteSize = textVertices.Length * sizeof(float)
            });
            fixed (float* ptr = textVertices)
            {
                _renderContext.UpdateBuffer(_textVbo, new ReadOnlySpan<byte>((byte*)ptr, textVertices.Length * sizeof(float)));
            }
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 :.,!?-+()[]{}x";
            foreach (char c in characters)
            {
                var charData = _fontRenderer.GetCharacterData(c);
                if (charData == null || charData.PixelData == null || charData.PixelData.Length == 0)
                    continue;
                GpuHandle allocated = _renderContext.CreateTexture(new TextureDesc
                {
                    Target = _renderContext.Enums.Texture2D,
                    InternalFormat = _renderContext.Enums.InternalRgba,
                    Width = charData.Width,
                    Height = charData.Height
                });
                fixed (byte* pixelPtr = charData.PixelData)
                {
                    _renderContext.UpdateTexture(allocated, charData.Width, charData.Height,
                        _renderContext.Enums.PixelBgra, _renderContext.Enums.UnsignedByte, pixelPtr);
                }
                _renderContext.SetTextureParams(allocated,
                    _renderContext.Enums.Linear, _renderContext.Enums.Linear,
                    _renderContext.Enums.ClampToEdge, _renderContext.Enums.ClampToEdge);
                _charTextures[c] = allocated;
            }
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
            if (_pipeline.IsValid)
                _renderContext.BindPipeline(_pipeline);
            else
                _shaderProgram.Use();
            _renderContext.ColorMask(true, true, true, true);
            _renderContext.DepthMask(false);
            _renderContext.SetConstants(ConstantSlot.Ui, new UiCB
            {
                Transform = Matrix4x4.Identity,
                Color = Vector4.One,
                UseTexture = 1f
            });
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
            _renderContext.Disable(_renderContext.Enums.ScissorTest);
            RenderTextPass(text, startX, adjustedStartY, width, height, fontSize, textColor ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
        }
        private void RenderTextPass(string text, float startX, float startY, int width, int height, float fontSize, Vector4 color, bool useTexture = true)
        {
            float currentX = startX;
            float spacing = -2.0f;
            Matrix4x4 transform = Matrix4x4.Identity;
            if (_pipeline.IsValid)
                _renderContext.BindPipeline(_pipeline);
            else
                _shaderProgram.Use();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!_charTextures.ContainsKey(c))
                    c = ' ';
                var charData = _fontRenderer.GetCharacterData(c);
                if (charData == null)
                    continue;
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
                    charLeft, charBottom, color.X, color.Y, color.Z, color.W, 0.0f, 1.0f,
                    charRight, charTop, color.X, color.Y, color.Z, color.W, 1.0f, 0.0f,
                    charLeft, charTop, color.X, color.Y, color.Z, color.W, 0.0f, 0.0f
                };
                fixed (float* ptr = textVertices)
                {
                    _renderContext.UpdateBuffer(_textVbo, new ReadOnlySpan<byte>((byte*)ptr, textVertices.Length * sizeof(float)));
                }
                _renderContext.BindVertexBuffer(_textVbo, 0, 8 * sizeof(float), 0);
                if (useTexture)
                {
                    GpuHandle texture = _charTextures[c];
                    _renderContext.BindTextureSlot(TextureSlot.Color, texture);
                    _renderContext.SetConstants(ConstantSlot.Ui, new UiCB
                    {
                        Transform = transform,
                        Color = color,
                        UseTexture = texture.IsValid ? 1f : 0f
                    });
                }
                else
                {
                    _renderContext.BindTextureSlot(TextureSlot.Color, default);
                    _renderContext.SetConstants(ConstantSlot.Ui, new UiCB
                    {
                        Transform = transform,
                        Color = color,
                        UseTexture = 0f
                    });
                }
                _renderContext.Draw(6);
                currentX += charWidth + spacing;
            }
        }
        public void Dispose()
        {
            if (_textVbo.IsValid)
                _renderContext.Destroy(_textVbo);
            if (_pipeline.IsValid)
                _renderContext.Destroy(_pipeline);
            foreach (var texture in _charTextures.Values)
            {
                if (texture.IsValid)
                    _renderContext.Destroy(texture);
            }
            _charTextures.Clear();
        }
    }
}
