// Folder: SiegeEngine.Rendering
// File: UIQuadRenderer.cs
using System.Numerics;
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering.Shaders;
using System;

namespace SiegeEngine.Rendering
{
    public unsafe class UIQuadRenderer
    {
        private readonly IRenderContext _renderContext;
        private uint _vao, _vbo, _ebo;
        private ShaderProgram _shader;
        public UIQuadRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext;
            Initialize();
        }
        private void Initialize()
        {
            _shader = new ShaderProgram(_renderContext, UiShader.VertexSource, UiShader.FragmentSource);
            _renderContext.GenVertexArrays(1, out _vao);
            _renderContext.BindVertexArray(_vao);
            _renderContext.GenBuffers(1, out _vbo);
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);
            float[] vertices = new float[]
            {
                0.0f, 0.0f,
                1.0f, 0.0f,
                1.0f, 1.0f,
                0.0f, 1.0f
            };
            fixed (float* ptr = vertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 2 * sizeof(float), (void*)0);
            _renderContext.GenBuffers(1, out _ebo);
            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
            uint[] indices = new uint[] { 0, 1, 2, 0, 2, 3 };
            fixed (uint* idxPtr = indices)
            {
                _renderContext.BufferData(_renderContext.Enums.ElementArrayBuffer, (uint)(indices.Length * sizeof(uint)), idxPtr, _renderContext.Enums.StaticDraw);
            }
            _renderContext.BindVertexArray(0);
        }
        public void DrawQuad(Vector2 position, Vector2 size, Vector4 color, Matrix4x4 ortho)
        {
            _shader.Use();
            Matrix4x4 scale = Matrix4x4.CreateScale(size.X, size.Y, 1f);
            Matrix4x4 translate = Matrix4x4.CreateTranslation(position.X, position.Y, 0f);
            Matrix4x4 model = scale * translate;
            _shader.SetMatrix4("uTransform", ortho * model);
            _shader.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
            _shader.SetUniform("uUseTexture", 0.0f);
            _renderContext.BindVertexArray(_vao);
            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
            _renderContext.DrawElements(_renderContext.Enums.Triangles, 6, _renderContext.Enums.UnsignedInt, (void*)0);
            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
        }
    }
}