// Folder: SiegeEngine/Core/GPU/Renderers
// File: DebugSurfaceRenderer.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe class DebugSurfaceRenderer
    {
        private readonly IRenderContext _renderContext;
        private GpuHandle _pipeline;

        public DebugSurfaceRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext;
        }

        public void Initialize()
        {
            if (_pipeline.IsValid)
                return;
            PipelineDesc desc = ShaderCatalog.Describe(ShaderId.Point, _renderContext);
            desc.State.Primitive = _renderContext.Enums.Triangles;
            desc.State.DepthTest = false;
            desc.State.DepthWrite = false;
            desc.State.Blend = true;
            _pipeline = _renderContext.CreatePipeline(desc);
        }

        public void DrawTriangles(VertexBuffer buffer, Matrix4x4 view, Matrix4x4 projection)
        {
            DrawTriangles(buffer, Matrix4x4.Identity, view, projection);
        }

        public void DrawTriangles(VertexBuffer buffer, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
        {
            if (buffer == null) return;
            if (!_pipeline.IsValid) Initialize();
            uint indexCount = buffer.GetIndexCount();
            uint vertexCount = buffer.GetVertexCount();
            if (indexCount == 0 && vertexCount == 0) return;

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);

            FrameCB frame = new FrameCB { View = view, Projection = projection };
            ObjectCB obj = new ObjectCB { Model = model, PointSize = 6f };
            _renderContext.BindPipeline(_pipeline);
            _renderContext.SetConstants(ConstantSlot.Frame, frame);
            _renderContext.SetConstants(ConstantSlot.Object, obj);

            buffer.Bind();
            if (indexCount > 0)
                _renderContext.DrawIndexed((int)indexCount);
            else
                _renderContext.Draw((int)vertexCount);

            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public void Dispose()
        {
            if (_pipeline.IsValid)
            {
                _renderContext.Destroy(_pipeline);
                _pipeline = GpuHandle.Invalid;
            }
        }
    }
}
