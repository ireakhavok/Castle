// Folder: SiegeEngine/Core/Rendering
// File: TerrainRenderer.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.Terrain;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe sealed class TerrainRenderer : IDisposable
    {
        // Terrain-only program. SceneShader stays untouched so ModelRenderer
        // does not pick up gl_FragDepth or any bias.
        //
        // Fill is pushed away from the camera by a slope-scaled window-Z
        // offset (same math as glPolygonOffset). Lattice is drawn at true
        // depth with the test on. No constant NDC pull — that is what
        // punched through hills once the camera backed up.
        private const string TerrainVertex = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec4 aColor;
            layout(location = 2) in vec2 aUV;
            out vec4 vColor;
            out vec2 vUV;
            uniform mat4 uModel;
            uniform mat4 uView;
            uniform mat4 uProjection;
            void main() {
                gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
                vColor = aColor;
                vUV = aUV;
            }";

        private const string TerrainFragment = @"
            #version 330 core
            in vec4 vColor;
            in vec2 vUV;
            out vec4 FragColor;
            uniform sampler2D uTexture;
            uniform int uHasTexture;
            uniform float uPolyFactor;
            uniform float uPolyUnits;
            void main() {
                if (uHasTexture == 1) {
                    if (vUV.x >= 0.0 && vUV.x <= 1.0 && vUV.y >= 0.0 && vUV.y <= 1.0) {
                        FragColor = texture(uTexture, vUV);
                    } else {
                        discard;
                    }
                } else {
                    FragColor = vColor;
                }
                float dz = max(abs(dFdx(gl_FragCoord.z)), abs(dFdy(gl_FragCoord.z)));
                gl_FragDepth = clamp(gl_FragCoord.z + uPolyFactor * dz + uPolyUnits / 16777216.0, 0.0, 1.0);
            }";

        private readonly IRenderContext _renderContext;
        private ShaderProgram _terrainShader;
        private ShaderProgram _spriteShader;

        public TerrainRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        public void Initialize()
        {
            _terrainShader = new ShaderProgram(_renderContext, TerrainVertex, TerrainFragment);
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

            bool textured = hasColorTexture && terrainTextureId != 0 && terrainBuffer != null;

            if (textured || (drawWireframe && terrainBuffer != null))
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
                    // Untextured: write the surface into the depth buffer only.
                    _renderContext.ColorMask(false, false, false, false);
                    _terrainShader.SetUniform("uHasTexture", 0);
                }

                // Push the fill away from the camera. Same as PolygonOffset(1, 2).
                _terrainShader.SetUniform("uPolyFactor", 1f);
                _terrainShader.SetUniform("uPolyUnits", 2f);
                terrainBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.ColorMask(true, true, true, true);
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
                    _renderContext.Enable(_renderContext.Enums.Blend);
                    _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
                    _renderContext.Enable(_renderContext.Enums.LineSmooth);
                    _renderContext.Disable(_renderContext.Enums.CullFace);
                    _renderContext.LineWidth(1.25f);

                    _terrainShader.SetUniform("uHasTexture", 0);
                    _terrainShader.SetUniform("uPolyFactor", 0f);
                    _terrainShader.SetUniform("uPolyUnits", 0f);
                    lines.Bind();
                    _renderContext.DrawElements(_renderContext.Enums.Lines, lines.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

                    _renderContext.LineWidth(1f);
                    _renderContext.Disable(_renderContext.Enums.LineSmooth);
                    _renderContext.Disable(_renderContext.Enums.Blend);
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
                _terrainShader.SetUniform("uPolyFactor", 0f);
                _terrainShader.SetUniform("uPolyUnits", 0f);
                ghostBuffer.Bind();
                _renderContext.Enable(_renderContext.Enums.LineSmooth);
                _renderContext.DrawElements(_renderContext.Enums.Lines, ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.Disable(_renderContext.Enums.LineSmooth);
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