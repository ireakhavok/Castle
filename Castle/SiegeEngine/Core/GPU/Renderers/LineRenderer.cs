// Folder: SiegeEngine/Core/GPU/Renderers
// File: LineRenderer.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    /// <summary>
    /// Generic line / gizmo / debug-line renderer.
    /// Owns the Point shader pipeline and all GL state (DepthTest, LineWidth, Blend, LineSmooth).
    /// All continuous 3-D line drawing (gizmos, physics debug, acoustic rays, skybox rings/axes)
    /// must go through this class so the cancer is not duplicated in every overlay or scene.
    /// </summary>
    public unsafe sealed class LineRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private GpuHandle _pipeline;

        public LineRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        public void Initialize()
        {
            if (_pipeline.IsValid)
                return;
            PipelineDesc desc = ShaderCatalog.Describe(ShaderId.Point, _renderContext);
            desc.State.Primitive = _renderContext.Enums.Lines;
            desc.State.DepthTest = false;
            desc.State.DepthWrite = false;
            _pipeline = _renderContext.CreatePipeline(desc);
        }

        /// <summary>
        /// Draw a line VertexBuffer with the supplied model/view/projection.
        /// Uses DrawIndexed when the buffer has indices, Draw otherwise.
        /// Handles all GL state; caller must not touch DepthTest / LineWidth / Blend / LineSmooth.
        /// </summary>
        public void DrawLines(VertexBuffer buffer, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, float lineWidth = 1f, bool lineSmooth = false)
        {
            if (buffer == null) return;
            uint indexCount = buffer.GetIndexCount();
            uint vertexCount = buffer.GetVertexCount();
            if (indexCount == 0 && vertexCount == 0) return;
            if (!_pipeline.IsValid) Initialize();

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            if (lineSmooth)
                _renderContext.Enable(_renderContext.Enums.LineSmooth);
            if (lineWidth != 1f)
                _renderContext.LineWidth(lineWidth);

            FrameCB frame = new FrameCB { View = view, Projection = projection };
            ObjectCB obj = new ObjectCB { Model = model, PointSize = 6f };
            _renderContext.BindPipeline(_pipeline);
            _renderContext.SetConstants(ConstantSlot.Frame, frame);
            _renderContext.SetConstants(ConstantSlot.Object, obj);

            _renderContext.BindVertexBuffer(buffer.VertexHandle, 0, buffer.Stride, 0);
            if (indexCount > 0)
            {
                _renderContext.BindIndexBuffer(buffer.IndexHandle);
                _renderContext.DrawIndexed((int)indexCount);
            }
            else
                _renderContext.Draw((int)vertexCount);

            if (lineWidth != 1f)
                _renderContext.LineWidth(1f);
            if (lineSmooth)
                _renderContext.Disable(_renderContext.Enums.LineSmooth);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        /// <summary>
        /// Convenience overload when model is identity.
        /// </summary>
        public void DrawLines(VertexBuffer buffer, Matrix4x4 view, Matrix4x4 projection, float lineWidth = 1f)
        {
            DrawLines(buffer, Matrix4x4.Identity, view, projection, lineWidth);
        }

        public void DrawLines(VertexBuffer buffer, Matrix4x4 view, Matrix4x4 projection, float lineWidth, bool lineSmooth)
        {
            DrawLines(buffer, Matrix4x4.Identity, view, projection, lineWidth, lineSmooth);
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
