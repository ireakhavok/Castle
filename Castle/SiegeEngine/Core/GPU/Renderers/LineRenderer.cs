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
    /// Owns the PointShader and all GL state (DepthTest, LineWidth, Blend).
    /// All continuous 3-D line drawing (gizmos, physics debug, acoustic rays, skybox rings/axes)
    /// must go through this class so the cancer is not duplicated in every overlay or scene.
    /// </summary>
    public unsafe sealed class LineRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private ShaderProgram _shader;

        public LineRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        public void Initialize()
        {
            if (_shader == null)
                _shader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
        }

        /// <summary>
        /// Draw a line VertexBuffer with the supplied model/view/projection.
        /// Handles all GL state; caller must not touch DepthTest / LineWidth / Blend.
        /// </summary>
        public void DrawLines(VertexBuffer buffer, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, float lineWidth = 1f)
        {
            if (buffer == null || buffer.GetIndexCount() == 0) return;
            if (_shader == null) Initialize();

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            if (lineWidth != 1f)
                _renderContext.LineWidth(lineWidth);

            _shader.Use();
            _shader.SetMatrix4("uModel", model);
            _shader.SetMatrix4("uView", view);
            _shader.SetMatrix4("uProjection", projection);
            _shader.SetUniform("uPointSize", 6f);

            buffer.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Lines, buffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

            if (lineWidth != 1f)
                _renderContext.LineWidth(1f);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        /// <summary>
        /// Convenience overload when model is identity.
        /// </summary>
        public void DrawLines(VertexBuffer buffer, Matrix4x4 view, Matrix4x4 projection, float lineWidth = 1f)
        {
            DrawLines(buffer, Matrix4x4.Identity, view, projection, lineWidth);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            _shader = null;
        }
    }
}