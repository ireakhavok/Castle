// Folder: SiegeEngine/Core/Rendering
// File: TerrainRenderer.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
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
            _terrainShader = ShaderProgram.FromId(_renderContext, ShaderId.Terrain);
            _spriteShader = ShaderProgram.FromId(_renderContext, ShaderId.Sprite);
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
            _renderContext.BindCamera(view, projection, Matrix4x4.Identity);
            if (_renderContext.TryGetConstants(ConstantSlot.Object, out ObjectCB terrainObj))
            {
                terrainObj.ReceiveShadows = 1;
                _renderContext.SetConstants(ConstantSlot.Object, terrainObj);
            }
            LightingFrame.Current?.ApplyConstants(_renderContext);
            LightingFrame.Current?.ApplyTo(_terrainShader, _renderContext);
            WritePost(unlit: 0f, polyFactor: 0f, polyUnits: 0f);

            bool textured = hasColorTexture && terrainTextureId != 0 && terrainBuffer != null;

            if (terrainBuffer != null)
            {
                _renderContext.Disable(_renderContext.Enums.CullFace);
                WriteFrameHasTexture(textured);
                if (textured)
                {
                    _renderContext.BindTextureSlot(TextureSlot.Color, _renderContext.ImportTexture(terrainTextureId, _renderContext.Enums.Texture2D));
                }

                WritePost(unlit: 0f, polyFactor: 1f, polyUnits: 2f);
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

                    WriteFrameHasTexture(false);
                    WritePost(unlit: 1f, polyFactor: 0f, polyUnits: 0f);
                    lines.Bind();
                    _renderContext.DrawElements(_renderContext.Enums.Lines, lines.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

                    WritePost(unlit: 0f, polyFactor: 0f, polyUnits: 0f);
                    _renderContext.Enable(_renderContext.Enums.CullFace);
                    _renderContext.DepthMask(true);
                    _renderContext.Enable(_renderContext.Enums.DepthTest);
                }
            }

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
                _renderContext.BindCamera(view, projection, ghostModel);
                _renderContext.BindTextureSlot(TextureSlot.Color, _renderContext.ImportTexture(ghostTextureId, _renderContext.Enums.Texture2D));
                ghostBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }
            else
            {
                _terrainShader.Use();
                _renderContext.BindCamera(view, projection, ghostModel);
                WriteFrameHasTexture(false);
                WritePost(unlit: 1f, polyFactor: 0f, polyUnits: 0f);
                ghostBuffer.Bind();
                _renderContext.Disable(_renderContext.Enums.LineSmooth);
                _renderContext.DrawElements(_renderContext.Enums.Lines, ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                WritePost(unlit: 0f, polyFactor: 0f, polyUnits: 0f);
            }

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthMask(true);
            _renderContext.Disable(_renderContext.Enums.Blend);
        }

        public void Dispose()
        {
            _terrainShader?.Dispose();
            _spriteShader?.Dispose();
        }

        void WriteFrameHasTexture(bool hasTexture)
        {
            if (!_renderContext.TryGetConstants(ConstantSlot.Frame, out FrameCB frame))
                frame = default;
            frame.HasTexture = hasTexture ? 1 : 0;
            _renderContext.SetConstants(ConstantSlot.Frame, frame);
        }

        void WritePost(float unlit, float polyFactor, float polyUnits)
        {
            if (!_renderContext.TryGetConstants(ConstantSlot.Post, out PostCB post))
                post = default;
            post.Unlit = unlit;
            post.PolyFactor = polyFactor;
            post.PolyUnits = polyUnits;
            _renderContext.SetConstants(ConstantSlot.Post, post);
        }
    }
}
