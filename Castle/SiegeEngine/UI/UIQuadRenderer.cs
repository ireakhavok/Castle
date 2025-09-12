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
            _renderContext.GenBuffers(1, out _ebo);
            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
            uint[] indices = new uint[] { 0, 1, 2, 0, 2, 3 };
            fixed (uint* idxPtr = indices)
            {
                _renderContext.BufferData(_renderContext.Enums.ElementArrayBuffer, (uint)(indices.Length * sizeof(uint)), idxPtr, _renderContext.Enums.StaticDraw);
            }
            _renderContext.BindVertexArray(0);
        }
        public void DrawQuad(float posX, float posY, float sizeX, float sizeY, Vector4 color, float viewportWidth, float viewportHeight)
        {
            _shader.Use();
            float left = 2.0f * posX / viewportWidth - 1.0f;
            float right = 2.0f * (posX + sizeX) / viewportWidth - 1.0f;
            float top = 1.0f - 2.0f * posY / viewportHeight;
            float bottom = 1.0f - 2.0f * (posY + sizeY) / viewportHeight;
            float[] vertices = new float[]
            {
                left, bottom,
                right, bottom,
                right, top,
                left, top
            };
            _renderContext.BindVertexArray(_vao);
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);
            fixed (float* ptr = vertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, _renderContext.Enums.DynamicDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 2 * sizeof(float), (void*)0);
            _shader.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
            _shader.SetUniform("uUseTexture", 0.0f);
            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
            _renderContext.DrawElements(_renderContext.Enums.Triangles, 6, _renderContext.Enums.UnsignedInt, (void*)0);
            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
        }
    }
}