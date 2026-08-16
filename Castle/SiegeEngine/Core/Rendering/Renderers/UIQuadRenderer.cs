// Folder: SiegeEngine/Core/Rendering
// File: UIQuadRenderer.cs
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Rendering.Renderers
{
    public unsafe class UIQuadRenderer : IDisposable
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

        private void EnsureUIState()
        {
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
        }

        // FUTURE-PROOF: Explicit VAO bind + disable extra attribs (prevents NDC quad leakage into simple DrawQuad/DrawLine)
        private void ResetVertexState()
        {
            _renderContext.BindVertexArray(_vao);
            _renderContext.DisableVertexAttribArray(1); // critical: NDC draws leave attrib 1 enabled
            _renderContext.EnableVertexAttribArray(0);
        }

        public void DrawQuad(float posX, float posY, float sizeX, float sizeY, Vector4 color, float viewportWidth, float viewportHeight)
        {
            EnsureUIState();
            ResetVertexState();

            _shader.Use();
            _shader.SetMatrix4("uTransform", Matrix4x4.Identity);

            float left = 2.0f * posX / viewportWidth - 1.0f;
            float right = 2.0f * (posX + sizeX) / viewportWidth - 1.0f;
            float top = 1.0f - 2.0f * posY / viewportHeight;
            float bottom = 1.0f - 2.0f * (posY + sizeY) / viewportHeight;

            float[] vertices = new float[] { left, bottom, right, bottom, right, top, left, top };

            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);
            fixed (float* ptr = vertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, _renderContext.Enums.DynamicDraw);
            }

            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 2 * sizeof(float), (void*)0);

            _shader.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
            _shader.SetUniform("uUseTexture", 0.0f);
            _shader.SetUniform("uUseRounded", 0.0f);

            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
            _renderContext.DrawElements(_renderContext.Enums.Triangles, 6, _renderContext.Enums.UnsignedInt, (void*)0);

            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
        }

        public void DrawNdcQuad(float[] ndc, Vector4 color)
        {
            DrawNdcQuad(ndc, color, Vector4.Zero, Vector2.Zero, 0f, Vector4.Zero);
        }

        public void DrawNdcQuad(float[] ndc, Vector4 color, Vector4 borderRadius, Vector2 rectSize, float borderWidth = 0f, Vector4 borderColor = new Vector4())
        {
            EnsureUIState();
            ResetVertexState();

            _shader.Use();
            _shader.SetMatrix4("uTransform", Matrix4x4.Identity);
            _shader.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
            _shader.SetUniform("uUseTexture", 0.0f);

            float useRounded = borderRadius == Vector4.Zero ? 0f : 1f;
            _shader.SetUniform("uUseRounded", useRounded);
            if (useRounded > 0.5f)
            {
                _shader.SetUniform("uBorderRadius", borderRadius.X, borderRadius.Y, borderRadius.Z, borderRadius.W);
                _shader.SetUniform("uRectSize", rectSize.X, rectSize.Y, 0f, 0f);
            }
            _shader.SetUniform("uBorderWidth", borderWidth);
            _shader.SetUniform("uBorderColor", borderColor.X, borderColor.Y, borderColor.Z, borderColor.W);

            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);

            float[] vertices = new float[16];
            vertices[0] = ndc[0]; vertices[1] = ndc[1]; vertices[2] = 0f; vertices[3] = 0f;
            vertices[4] = ndc[2]; vertices[5] = ndc[3]; vertices[6] = 1f; vertices[7] = 0f;
            vertices[8] = ndc[4]; vertices[9] = ndc[5]; vertices[10] = 1f; vertices[11] = 1f;
            vertices[12] = ndc[6]; vertices[13] = ndc[7]; vertices[14] = 0f; vertices[15] = 1f;

            fixed (float* ptr = vertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, _renderContext.Enums.DynamicDraw);
            }

            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
            _renderContext.DrawElements(_renderContext.Enums.Triangles, 6, _renderContext.Enums.UnsignedInt, (void*)0);

            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
        }

        public void DrawLine(float x1, float y1, float x2, float y2, float thickness, Vector4 color, float viewportWidth, float viewportHeight)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return;

            float ux = dx / len;
            float uy = dy / len;
            float px = -uy * (thickness * 0.5f);
            float py = ux * (thickness * 0.5f);

            float lx1 = x1 + px; float ly1 = y1 + py;
            float lx2 = x2 + px; float ly2 = y2 + py;
            float rx1 = x1 - px; float ry1 = y1 - py;
            float rx2 = x2 - px; float ry2 = y2 - py;

            float[] vertices = new float[]
            {
                2.0f * lx1 / viewportWidth - 1.0f, 1.0f - 2.0f * ly1 / viewportHeight,
                2.0f * lx2 / viewportWidth - 1.0f, 1.0f - 2.0f * ly2 / viewportHeight,
                2.0f * rx2 / viewportWidth - 1.0f, 1.0f - 2.0f * ry2 / viewportHeight,
                2.0f * rx1 / viewportWidth - 1.0f, 1.0f - 2.0f * ry1 / viewportHeight
            };

            EnsureUIState();
            ResetVertexState();

            _shader.Use();
            _shader.SetMatrix4("uTransform", Matrix4x4.Identity);
            _shader.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
            _shader.SetUniform("uUseTexture", 0.0f);
            _shader.SetUniform("uUseRounded", 0.0f);

            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);
            fixed (float* ptr = vertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, _renderContext.Enums.DynamicDraw);
            }

            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 2 * sizeof(float), (void*)0);

            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
            _renderContext.DrawElements(_renderContext.Enums.Triangles, 6, _renderContext.Enums.UnsignedInt, (void*)0);

            _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
        }

        public void Dispose()
        {
            if (_vao != 0)
            {
                _renderContext.DeleteVertexArray(_vao);
                _vao = 0;
            }
            if (_vbo != 0)
            {
                _renderContext.DeleteBuffer(_vbo);
                _vbo = 0;
            }
            if (_ebo != 0)
            {
                _renderContext.DeleteBuffer(_ebo);
                _ebo = 0;
            }
            _shader?.Dispose();
        }
    }
}