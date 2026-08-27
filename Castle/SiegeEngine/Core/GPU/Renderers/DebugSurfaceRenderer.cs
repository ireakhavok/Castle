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
        private ShaderProgram _shader;

        public DebugSurfaceRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext;
        }

        public void Initialize()
        {
            _shader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
        }

        public void DrawTriangles(VertexBuffer buffer, Matrix4x4 view, Matrix4x4 projection)
        {
            DrawTriangles(buffer, Matrix4x4.Identity, view, projection);
        }

        public void DrawTriangles(VertexBuffer buffer, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
        {
            if (buffer == null || _shader == null) return;
            uint indexCount = buffer.GetIndexCount();
            uint vertexCount = buffer.GetVertexCount();
            if (indexCount == 0 && vertexCount == 0) return;

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);

            _shader.Use();
            _shader.SetMatrix4("uModel", model);
            _shader.SetMatrix4("uView", view);
            _shader.SetMatrix4("uProjection", projection);
            _shader.SetUniform("uPointSize", 6f);

            buffer.Bind();
            if (indexCount > 0)
                _renderContext.DrawElements(_renderContext.Enums.Triangles, indexCount, _renderContext.Enums.UnsignedInt, null);
            else
                _renderContext.DrawArrays(_renderContext.Enums.Triangles, 0, vertexCount);

            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            _shader = null;
        }
    }
}