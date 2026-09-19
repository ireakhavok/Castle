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
        private GpuHandle _pipeline;
        private bool _batchOpen;

        public SpriteRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        public void Initialize()
        {
            if (_pipeline.IsValid)
                return;
            _pipeline = _renderContext.CreatePipeline(ShaderCatalog.Describe(ShaderId.Sprite, _renderContext));
        }

        public void Begin(Matrix4x4 view, Matrix4x4 projection)
        {
            if (!_pipeline.IsValid) Initialize();
            FrameCB frame = new FrameCB { View = view, Projection = projection };
            _renderContext.BindPipeline(_pipeline);
            _renderContext.SetConstants(ConstantSlot.Frame, frame);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            _batchOpen = true;
        }

        public void Draw(VertexBuffer buffer, GpuHandle texture, Matrix4x4 model)
        {
            if (!_batchOpen || buffer == null || !texture.IsValid) return;
            ObjectCB obj = new ObjectCB { Model = model };
            _renderContext.SetConstants(ConstantSlot.Object, obj);
            _renderContext.BindTextureSlot(0, texture);
            _renderContext.BindVertexBuffer(buffer.VertexHandle, 0, buffer.Stride, 0);
            uint indexCount = buffer.GetIndexCount();
            if (indexCount == 0) indexCount = 6;
            if (indexCount > 0)
            {
                _renderContext.BindIndexBuffer(buffer.IndexHandle);
                _renderContext.DrawIndexed((int)indexCount);
            }
            else
                _renderContext.Draw((int)buffer.GetVertexCount());
        }

        public void End()
        {
            if (!_batchOpen) return;
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _batchOpen = false;
        }

        public void Dispose()
        {
            if (_batchOpen) End();
            if (_pipeline.IsValid)
            {
                _renderContext.Destroy(_pipeline);
                _pipeline = default;
            }
        }
    }
}
