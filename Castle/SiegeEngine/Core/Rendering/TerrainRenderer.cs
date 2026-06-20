// Folder: SiegeEngine/Core/Rendering
// File: TerrainRenderer.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.Terrain;
using System;
using System.Numerics;

namespace SiegeEngine.Core.Rendering
{
    public unsafe class TerrainRenderer
    {
        private readonly IRenderContext _renderContext;
        private ShaderProgram _terrainShader;

        public TerrainRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext;
        }

        public void Initialize()
        {
            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
        }

        public void Render(VertexBuffer buffer, Matrix4x4 view, Matrix4x4 projection, bool hasTexture, uint textureId, float[,] heightmap = null)
        {
            if (buffer == null) return;
            buffer.Bind();
            uint stride = 9 * sizeof(float);
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
            _renderContext.EnableVertexAttribArray(2);
            _renderContext.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(7 * sizeof(float)));
            _terrainShader.Use();
            _terrainShader.SetMatrix4("uView", view);
            _terrainShader.SetMatrix4("uProjection", projection);
            _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);
            if (hasTexture && textureId != 0)
            {
                _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, textureId);
                _terrainShader.SetUniform("uHasTexture", 1);
                _terrainShader.SetUniform("uTexture", 0);
            }
            else
            {
                _terrainShader.SetUniform("uHasTexture", 0);
            }
            uint idxCount = buffer.GetIndexCount();
            _renderContext.DrawElements(_renderContext.Enums.Triangles, idxCount, _renderContext.Enums.UnsignedInt, null);
        }

        public void Dispose()
        {
            _terrainShader?.Dispose();
        }
    }
}