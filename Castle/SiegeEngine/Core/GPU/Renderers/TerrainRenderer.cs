// Folder: SiegeEngine/Core/Rendering
// File: TerrainRenderer.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Core.GPU.Shaders.OpenGL;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.Terrain;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
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
            _terrainShader = new ShaderProgram(_renderContext, TerrainShader.VertexShaderSource, TerrainShader.FragmentShaderSource);
            _spriteShader = new ShaderProgram(_renderContext, SpriteShader.VertexShaderSource, SpriteShader.FragmentShaderSource);
        }

        public void RenderTerrain(Matrix4x4 view, Matrix4x4 projection, bool hasColorTexture, uint terrainTextureId, VertexBuffer terrainBuffer, float[,] heightmap = null, bool drawWireframe = true)
        {
            RenderTerrain(view, projection, hasColorTexture, terrainTextureId, terrainBuffer, null, heightmap, drawWireframe);
        }

        public void RenderTerrain(Matrix4x4 view, Matrix4x4 projection, bool hasColorTexture, uint terrainTextureId, VertexBuffer terrainBuffer, VertexBuffer wireframeBuffer, float[,] heightmap, bool drawWireframe)
        {
            if (terrainBuffer == null && wireframeBuffer == null) return;

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthMask(true);
            _renderContext.DepthFunc(_renderContext.Enums.Less);
            _renderContext.ColorMask(true, true, true, true);
            _renderContext.Enable(_renderContext.Enums.CullFace);
            _renderContext.CullFace(_renderContext.Enums.Back);
            _renderContext.FrontFace(_renderContext.Enums.CounterClockwise);

            _terrainShader.Use();
            _terrainShader.SetMatrix4("uView", view);
            _terrainShader.SetMatrix4("uProjection", projection);
            _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);
            _terrainShader.SetUniform("uLightDir", LightingFrame.DefaultSunDirection.X, LightingFrame.DefaultSunDirection.Y, LightingFrame.DefaultSunDirection.Z);
            _terrainShader.SetUniform("uLightColor", 1f, 1f, 1f);
            _terrainShader.SetUniform("uLightIntensity", 0f);
            _terrainShader.SetUniform("uAmbientColor", 0.45f, 0.45f, 0.48f);
            _terrainShader.SetUniform("uAmbientStrength", 0.30f);
            _terrainShader.SetUniform("uUnlit", 0);
            LightingFrame.Current?.ApplyTo(_terrainShader, _renderContext);
            _terrainShader.SetUniform("uReceiveShadows", 1);
            _terrainShader.SetUniform("uUnlit", 0);

            bool textured = hasColorTexture && terrainTextureId != 0 && terrainBuffer != null;

            if (terrainBuffer != null)
            {
                if (textured)
                {
                    _renderContext.Disable(_renderContext.Enums.CullFace);
                    _terrainShader.SetUniform("uHasTexture", 1);
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, terrainTextureId);
                    _terrainShader.SetUniform("uTexture", 0);
                }
                else
                {
                    _renderContext.Disable(_renderContext.Enums.CullFace);
                    _terrainShader.SetUniform("uHasTexture", 0);
                }

                _terrainShader.SetUniform("uPolyFactor", 1f);
                _terrainShader.SetUniform("uPolyUnits", 2f);
                terrainBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.Enable(_renderContext.Enums.CullFace);
                _renderContext.CullFace(_renderContext.Enums.Back);
            }

            if (drawWireframe)
            {
                VertexBuffer lines = wireframeBuffer ?? terrainBuffer;
                if (lines != null && lines.GetIndexCount() > 0)
                {
                    _renderContext.Enable(_renderContext.Enums.DepthTest);
                    _renderContext.DepthMask(false);
                    _renderContext.DepthFunc(_renderContext.Enums.Less);
                    _renderContext.Disable(_renderContext.Enums.Blend);
                    _renderContext.Disable(_renderContext.Enums.LineSmooth);
                    _renderContext.Disable(_renderContext.Enums.CullFace);
                    _renderContext.LineWidth(1f);

                    _terrainShader.SetUniform("uHasTexture", 0);
                    _terrainShader.SetUniform("uUnlit", 1);
                    _terrainShader.SetUniform("uPolyFactor", 0f);
                    _terrainShader.SetUniform("uPolyUnits", 0f);
                    lines.Bind();
                    _renderContext.DrawElements(_renderContext.Enums.Lines, lines.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

                    _terrainShader.SetUniform("uUnlit", 0);
                    _renderContext.Enable(_renderContext.Enums.CullFace);
                    _renderContext.DepthMask(true);
                    _renderContext.Enable(_renderContext.Enums.DepthTest);
                }
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
                _terrainShader.SetUniform("uUnlit", 1);
                _terrainShader.SetUniform("uPolyFactor", 0f);
                _terrainShader.SetUniform("uPolyUnits", 0f);
                ghostBuffer.Bind();
                _renderContext.Disable(_renderContext.Enums.LineSmooth);
                _renderContext.DrawElements(_renderContext.Enums.Lines, ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _terrainShader.SetUniform("uUnlit", 0);
            }

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthMask(true);
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
