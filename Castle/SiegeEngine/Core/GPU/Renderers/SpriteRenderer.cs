// Folder: SiegeEngine/Core/GPU/Renderers
// File: SpriteRenderer.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe sealed class SpriteRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private ShaderProgram _shader;
        private bool _batchOpen;

        public SpriteRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        public void Initialize()
        {
            if (_shader == null)
                _shader = new ShaderProgram(_renderContext, SpriteShader.VertexShaderSource, SpriteShader.FragmentShaderSource);
        }

        public void Begin(Matrix4x4 view, Matrix4x4 projection)
        {
            if (_shader == null) Initialize();
            _shader.Use();
            _shader.SetMatrix4("uView", view);
            _shader.SetMatrix4("uProjection", projection);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            _batchOpen = true;
        }

        public void Draw(VertexBuffer buffer, uint textureId, Matrix4x4 model)
        {
            if (!_batchOpen || buffer == null || textureId == 0 || _shader == null) return;
            _shader.SetMatrix4("uModel", model);
            _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, textureId);
            buffer.Bind();
            uint indexCount = buffer.GetIndexCount();
            if (indexCount == 0) indexCount = 6;
            _renderContext.DrawElements(_renderContext.Enums.Triangles, indexCount, _renderContext.Enums.UnsignedInt, null);
        }

        public void End()
        {
            if (!_batchOpen) return;
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _batchOpen = false;
        }

        public void Dispose()
        {
            if (_batchOpen) End();
            _shader?.Dispose();
            _shader = null;
        }
    }
}