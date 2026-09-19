// Folder: SiegeEngine.Rendering
// File: BackgroundRenderer.cs
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe class BackgroundRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private GpuHandle _bgVbo;
        private GpuHandle _bgTexture;
        private ShaderProgram _shaderProgram;
        private GpuHandle _pipeline;
        private int _textureWidth;
        private int _textureHeight;
        public BackgroundRenderer(IControlContext controlContext, nint window, IRenderContext renderContext)
        {
            _renderContext = renderContext;
        }
        public void Initialize(string backgroundPath, ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;
            if (!_pipeline.IsValid)
            {
                PipelineDesc desc = ShaderCatalog.Describe(ShaderId.Ui, _renderContext);
                desc.Layout = new VertexLayout(16, new[]
                {
                    new VertexAttribute(VertexSemantic.Position, _renderContext.Enums.Float, 2, 0, 0),
                    new VertexAttribute(VertexSemantic.Color, _renderContext.Enums.Float, 2, 8, 0)
                });
                desc.State.Primitive = _renderContext.Enums.TriangleFan;
                _pipeline = _renderContext.CreatePipeline(desc);
            }
            _bgVbo = _renderContext.CreateBuffer(new BufferDesc
            {
                Target = _renderContext.Enums.ArrayBuffer,
                Usage = _renderContext.Enums.DynamicDraw,
                ByteSize = 16 * sizeof(float)
            });
            float[] bgVertices = new float[]
            {
                -1.0f, -1.0f, 0.0f, 1.0f,
                 1.0f, -1.0f, 1.0f, 1.0f,
                 1.0f, 1.0f, 1.0f, 0.0f,
                -1.0f, 1.0f, 0.0f, 0.0f
            };
            fixed (float* ptr = bgVertices)
            {
                _renderContext.UpdateBuffer(_bgVbo, new ReadOnlySpan<byte>((byte*)ptr, bgVertices.Length * sizeof(float)));
            }
            Console.WriteLine($"Attempting to load background texture from: {backgroundPath}");
            try
            {
                (GpuHandle texId, Vector2 nativeSize) = TextureLoader.LoadTextureWithSize(_renderContext, backgroundPath);
                if (!texId.IsValid)
                    throw new Exception("TextureLoader returned 0");
                _textureWidth = (int)nativeSize.X;
                _textureHeight = (int)nativeSize.Y;
                _bgTexture = texId;
                _renderContext.SetTextureParams(_bgTexture, _renderContext.Enums.Nearest, _renderContext.Enums.Nearest, _renderContext.Enums.ClampToEdge, _renderContext.Enums.ClampToEdge);
                Console.WriteLine($"JPG texture loaded: {_bgTexture.Id} ({_textureWidth}x{_textureHeight})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load JPG: {ex.Message}");
                _bgTexture = default;
            }
        }
        public void Render(float posX, float posY, float width, float height, float viewportWidth, float viewportHeight)
        {
            if (!_bgTexture.IsValid) return;
            float drawWidth = _textureWidth;
            float drawHeight = _textureHeight;
            float drawX = posX + (width - drawWidth) / 2f;
            float drawY = posY + (height - drawHeight) / 2f;
            float left = drawX;
            float right = drawX + drawWidth;
            float top = drawY;
            float bottom = drawY + drawHeight;
            float leftNDC = left / viewportWidth * 2f - 1f;
            float rightNDC = right / viewportWidth * 2f - 1f;
            float topNDC = 1f - top / viewportHeight * 2f;
            float bottomNDC = 1f - bottom / viewportHeight * 2f;
            float[] bgVertices = new float[]
            {
                leftNDC, bottomNDC, 0.0f, 1.0f,
                rightNDC, bottomNDC, 1.0f, 1.0f,
                rightNDC, topNDC, 1.0f, 0.0f,
                leftNDC, topNDC, 0.0f, 0.0f
            };
            fixed (float* ptr = bgVertices)
            {
                _renderContext.UpdateBuffer(_bgVbo, new ReadOnlySpan<byte>((byte*)ptr, bgVertices.Length * sizeof(float)));
            }
            _renderContext.BindPipeline(_pipeline);
            _renderContext.SetConstants(ConstantSlot.Ui, new UiCB
            {
                Transform = Matrix4x4.Identity,
                Color = Vector4.One,
                UseTexture = 1f
            });
            _renderContext.BindTextureSlot(0, _bgTexture);
            _renderContext.BindVertexBuffer(_bgVbo, 0, 16, 0);
            _renderContext.Draw(4);
        }
        public void Dispose()
        {
            if (_bgVbo.IsValid)
                _renderContext.Destroy(_bgVbo);
            if (_bgTexture.IsValid)
                _renderContext.Destroy(_bgTexture);
            if (_pipeline.IsValid)
                _renderContext.Destroy(_pipeline);
        }
    }
}
