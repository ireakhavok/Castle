// Folder: SiegeEngine/Core/Rendering
// File: TerrainRenderer.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.Terrain;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Rendering
{
    public unsafe sealed class TerrainRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private ShaderProgram _terrainShader;
        private ShaderProgram _spriteShader;

        public TerrainRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        public void Initialize()
        {
            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
            _spriteShader = new ShaderProgram(_renderContext, SpriteShader.VertexShaderSource, SpriteShader.FragmentShaderSource);
        }

        public void RenderTerrain(Matrix4x4 view, Matrix4x4 projection, bool hasColorTexture, uint terrainTextureId, VertexBuffer terrainBuffer, float[,] heightmap = null, bool drawWireframe = true)
        {
            if (terrainBuffer == null) return;

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.CullFace);
            _renderContext.CullFace(_renderContext.Enums.Back);
            _renderContext.FrontFace(_renderContext.Enums.CounterClockwise);

            terrainBuffer.Bind();

            _terrainShader.Use();
            _terrainShader.SetMatrix4("uView", view);
            _terrainShader.SetMatrix4("uProjection", projection);
            _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);

            if (drawWireframe)
            {
                _terrainShader.SetUniform("uHasTexture", 0);
                _renderContext.DrawElements(_renderContext.Enums.Lines, terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }

            if (hasColorTexture && terrainTextureId != 0)
            {
                _renderContext.Disable(_renderContext.Enums.CullFace);

                _terrainShader.SetUniform("uHasTexture", 1);
                _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, terrainTextureId);
                _terrainShader.SetUniform("uTexture", 0);

                _renderContext.DrawElements(_renderContext.Enums.Triangles, terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

                _renderContext.Enable(_renderContext.Enums.CullFace);
                _renderContext.CullFace(_renderContext.Enums.Back);
            }

            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _renderContext.BindVertexArray(0);
        }

        public void RenderGhost(ShaderProgram spriteShader, Matrix4x4 view, Matrix4x4 projection, Matrix4x4 ghostModel, uint ghostTextureId, VertexBuffer ghostBuffer, bool isPaintMode)
        {
            if (ghostBuffer == null) return;

            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            // Ghost is a camera-facing preview overlay (triangles or lines).
            // CullFace is left enabled by RenderTerrain; disable it so the textured sticker is never culled.
            _renderContext.Disable(_renderContext.Enums.CullFace);

            if (isPaintMode && ghostTextureId != 0)
            {
                spriteShader.Use();
                spriteShader.SetMatrix4("uModel", ghostModel);
                spriteShader.SetMatrix4("uView", view);
                spriteShader.SetMatrix4("uProjection", projection);
                _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, ghostTextureId);
                spriteShader.SetUniform("uTexture", 0);
                ghostBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }
            else
            {
                _terrainShader.Use();
                _terrainShader.SetMatrix4("uModel", ghostModel);
                _terrainShader.SetUniform("uHasTexture", 0);
                ghostBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Lines, ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _renderContext.BindVertexArray(0);
        }

        public void Dispose()
        {
            _terrainShader?.Dispose();
            _spriteShader?.Dispose();
        }
    }
}