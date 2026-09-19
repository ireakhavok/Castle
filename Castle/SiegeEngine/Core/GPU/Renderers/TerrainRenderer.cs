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
        private GpuHandle _wirePipeline;

        public TerrainRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        public void Initialize()
        {
            _terrainShader = ShaderProgram.FromId(_renderContext, ShaderId.Terrain);
            _spriteShader = ShaderProgram.FromId(_renderContext, ShaderId.Sprite);
            PipelineDesc wire = ShaderCatalog.Describe(ShaderId.Terrain, _renderContext);
            GpuRenderState state = wire.State;
            state.Primitive = _renderContext.Enums.Lines;
            state.DepthWrite = false;
            state.CullMode = _renderContext.Enums.None;
            wire.State = state;
            if (_wirePipeline.IsValid)
                _renderContext.Destroy(_wirePipeline);
            _wirePipeline = _renderContext.CreatePipeline(wire);
        }

        public void RenderTerrain(Matrix4x4 view, Matrix4x4 projection, bool hasColorTexture, GpuHandle terrainTexture, VertexBuffer terrainBuffer, float[,] heightmap = null, bool drawWireframe = true)
        {
            RenderTerrain(view, projection, hasColorTexture, terrainTexture, terrainBuffer, null, heightmap, drawWireframe);
        }

        public void RenderTerrain(Matrix4x4 view, Matrix4x4 projection, bool hasColorTexture, GpuHandle terrainTexture, VertexBuffer terrainBuffer, VertexBuffer wireframeBuffer, float[,] heightmap, bool drawWireframe)
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

            bool textured = hasColorTexture && terrainTexture.IsValid && terrainBuffer != null;

            if (terrainBuffer != null)
            {
                _renderContext.Disable(_renderContext.Enums.CullFace);
                WriteFrameHasTexture(textured);
                if (textured)
                    _renderContext.BindTextureSlot(TextureSlot.Color, terrainTexture);
                else
                    _renderContext.BindTextureSlot(TextureSlot.Color, default);

                WritePost(unlit: 0f, polyFactor: 1f, polyUnits: 2f);
                terrainBuffer.Bind();
                _renderContext.DrawIndexed((int)terrainBuffer.GetIndexCount());
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
                    if (_wirePipeline.IsValid)
                        _renderContext.BindPipeline(_wirePipeline);
                    lines.Bind();
                    _renderContext.DrawIndexed((int)lines.GetIndexCount());

                    WritePost(unlit: 0f, polyFactor: 0f, polyUnits: 0f);
                    _terrainShader.Use();
                    _renderContext.Enable(_renderContext.Enums.CullFace);
                    _renderContext.DepthMask(true);
                    _renderContext.Enable(_renderContext.Enums.DepthTest);
                }
            }

            _renderContext.BindTextureSlot(TextureSlot.Color, default);
            WriteFrameHasTexture(false);
        }

        public void RenderGhost(ShaderProgram spriteShader, Matrix4x4 view, Matrix4x4 projection, Matrix4x4 ghostModel, GpuHandle ghostTexture, VertexBuffer ghostBuffer, bool isPaintMode)
        {
            if (ghostBuffer == null) return;

            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);

            if (isPaintMode && ghostTexture.IsValid)
            {
                spriteShader.Use();
                _renderContext.BindCamera(view, projection, ghostModel);
                _renderContext.BindTextureSlot(TextureSlot.Color, ghostTexture);
                ghostBuffer.Bind();
                _renderContext.DrawIndexed((int)ghostBuffer.GetIndexCount());
            }
            else
            {
                if (_wirePipeline.IsValid)
                    _renderContext.BindPipeline(_wirePipeline);
                else
                    _terrainShader.Use();
                _renderContext.BindCamera(view, projection, ghostModel);
                WriteFrameHasTexture(false);
                WritePost(unlit: 1f, polyFactor: 0f, polyUnits: 0f);
                ghostBuffer.Bind();
                _renderContext.Disable(_renderContext.Enums.LineSmooth);
                _renderContext.DrawIndexed((int)ghostBuffer.GetIndexCount());
                WritePost(unlit: 0f, polyFactor: 0f, polyUnits: 0f);
                _terrainShader.Use();
            }

            _renderContext.BindTextureSlot(TextureSlot.Color, default);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthMask(true);
            _renderContext.Disable(_renderContext.Enums.Blend);
        }

        public void Dispose()
        {
            if (_wirePipeline.IsValid)
            {
                _renderContext.Destroy(_wirePipeline);
                _wirePipeline = default;
            }
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
