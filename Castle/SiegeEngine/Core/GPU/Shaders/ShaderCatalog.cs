// Folder: SiegeEngine/Core/GPU/Shaders
// File: ShaderCatalog.cs
using System;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders.OpenGL;

namespace SiegeEngine.Core.GPU.Shaders
{
    public static class ShaderCatalog
    {
        public static ShaderSourceSet Get(ShaderId id, RenderBackend backend)
        {
            if (backend != RenderBackend.OpenGL)
                throw new NotSupportedException($"ShaderId '{id}' has no sources for {backend}.");

            switch (id)
            {
                case ShaderId.Point:
                    return VsFs(PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
                case ShaderId.Water:
                    return VsFs(WaterShader.VertexShaderSource, WaterShader.FragmentShaderSource);
                case ShaderId.Grid:
                    return VsFs(SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
                case ShaderId.Model:
                    return VsFs(ModelShader.VertexShaderSource, ModelShader.FragmentShaderSource);
                case ShaderId.Animation:
                    return VsFs(AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);
                case ShaderId.Skybox:
                    return VsFs(SkyboxShader.VertexShaderSource, SkyboxShader.FragmentShaderSource);
                case ShaderId.SkyboxPreview:
                    return VsFs(SkyboxPreviewShader.VertexShaderSource, SkyboxPreviewShader.FragmentShaderSource);
                case ShaderId.Sprite:
                    return VsFs(SpriteShader.VertexShaderSource, SpriteShader.FragmentShaderSource);
                case ShaderId.Text:
                    return VsFs(TextShader.VertexShaderSource, TextShader.FragmentShaderSource);
                case ShaderId.Ui:
                    return VsFs(UiShader.VertexSource, UiShader.FragmentSource);
                case ShaderId.Asset:
                    return VsFs(AssetShader.VertexShaderSource, AssetShader.FragmentShaderSource);
                case ShaderId.EditorScene:
                    return VsFs(EditorSceneShader.VertexShaderSource, EditorSceneShader.FragmentShaderSource);
                case ShaderId.ShadowDepth:
                    return VsFs(ShadowShaders.DepthVertex, ShadowShaders.DepthFragment);
                case ShaderId.Fog:
                    return VsFs(FogShaders.FullscreenVertex, FogShaders.VolumetricFragment);
                case ShaderId.AcousticId:
                    return VsFs(AcousticIdShader.VertexSource, AcousticIdShader.FragmentSource);
                case ShaderId.AcousticResidual:
                    throw new NotSupportedException("ShaderId.AcousticResidual is a compute body prepended by AcousticRayTracer. Catalog it with that header, not as a standalone source.");
                case ShaderId.AntiAliasing:
                    throw new NotSupportedException("ShaderId.AntiAliasing is multiple programs (Copy/FXAA/SMAA/TAA). Name the pass before cataloging it.");
                case ShaderId.ColorCompose:
                    throw new NotSupportedException("ShaderId.ColorCompose is multiple programs (Extract/Downsample/Upsample/Compose/Luma/Adapt). Name the pass before cataloging it.");
                case ShaderId.Terrain:
                    return VsFs(TerrainShader.VertexShaderSource, TerrainShader.FragmentShaderSource);
                case ShaderId.AaCopy:
                    return VsFs(AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.CopyFragment);
                case ShaderId.AaFxaa:
                    return VsFs(AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.FxaaFragment);
                case ShaderId.AaSmaa:
                    return VsFs(AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.SmaaEdgeFragment);
                case ShaderId.AaSmaaWeight:
                    return VsFs(AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.SmaaWeightFragment);
                case ShaderId.AaSmaaBlend:
                    return VsFs(AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.SmaaBlendFragment);
                case ShaderId.AaTaa:
                    return VsFs(AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.TaaFragment);
                case ShaderId.CcExtract:
                    return VsFs(ColorComposeShaders.FullscreenVertex, ColorComposeShaders.ExtractFragment);
                case ShaderId.CcDownsample:
                    return VsFs(ColorComposeShaders.FullscreenVertex, ColorComposeShaders.DownsampleFragment);
                case ShaderId.CcUpsample:
                    return VsFs(ColorComposeShaders.FullscreenVertex, ColorComposeShaders.UpsampleFragment);
                case ShaderId.CcCompose:
                    return VsFs(ColorComposeShaders.FullscreenVertex, ColorComposeShaders.ComposeFragment);
                case ShaderId.CcLuma:
                    return VsFs(ColorComposeShaders.FullscreenVertex, ColorComposeShaders.LumaFragment);
                case ShaderId.CcAdapt:
                    return VsFs(ColorComposeShaders.FullscreenVertex, ColorComposeShaders.AdaptFragment);
                case ShaderId.CcLumaDown:
                    return VsFs(ColorComposeShaders.FullscreenVertex, ColorComposeShaders.LumaDownFragment);
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown ShaderId.");
            }
        }

        public static PipelineDesc Describe(ShaderId id, IRenderContext ctx)
        {
            if (ctx == null)
                throw new ArgumentNullException(nameof(ctx));
            ShaderSourceSet src = Get(id, ctx.Backend);
            return new PipelineDesc
            {
                ShaderId = id,
                VertexSource = src.Vertex,
                FragmentSource = src.Fragment,
                ComputeSource = src.Compute,
                Layout = DefaultLayout(id, ctx.Enums),
                State = DefaultState(id, ctx.Enums)
            };
        }

        static VertexLayout DefaultLayout(ShaderId id, AbstractRenderEnums enums)
        {
            if (id == ShaderId.Point)
            {
                return new VertexLayout(28, new[]
                {
                    new VertexAttribute(VertexSemantic.Position, enums.Float, 3, 0, 0),
                    new VertexAttribute(VertexSemantic.Color, enums.Float, 4, 12, 0)
                });
            }
            if (id == ShaderId.Skybox || id == ShaderId.SkyboxPreview)
            {
                return new VertexLayout(36, new[]
                {
                    new VertexAttribute(VertexSemantic.Position, enums.Float, 3, 0, 0)
                });
            }
            if (id == ShaderId.Water)
            {
                return new VertexLayout(12, new[]
                {
                    new VertexAttribute(VertexSemantic.Position, enums.Float, 3, 0, 0)
                });
            }
            if (id == ShaderId.Grid || id == ShaderId.Sprite || id == ShaderId.Terrain)
            {
                return new VertexLayout(36, new[]
                {
                    new VertexAttribute(VertexSemantic.Position, enums.Float, 3, 0, 0),
                    new VertexAttribute(VertexSemantic.Color, enums.Float, 4, 12, 0),
                    new VertexAttribute(VertexSemantic.TexCoord, enums.Float, 2, 28, 0)
                });
            }
            if (id == ShaderId.Model || id == ShaderId.Animation || id == ShaderId.Asset || id == ShaderId.ShadowDepth)
            {
                return new VertexLayout(80, new[]
                {
                    new VertexAttribute(VertexSemantic.Position, enums.Float, 3, 0, 0),
                    new VertexAttribute(VertexSemantic.Normal, enums.Float, 3, 12, 0),
                    new VertexAttribute(VertexSemantic.TexCoord, enums.Float, 2, 24, 0),
                    new VertexAttribute(VertexSemantic.MaterialIndex, enums.Float, 1, 32, 0),
                    new VertexAttribute(VertexSemantic.Tangent, enums.Float, 3, 36, 0),
                    new VertexAttribute(VertexSemantic.BoneIds, enums.Float, 4, 48, 0),
                    new VertexAttribute(VertexSemantic.BoneWeights, enums.Float, 4, 64, 0)
                });
            }
            if (id == ShaderId.Text || id == ShaderId.Ui)
            {
                return new VertexLayout(32, new[]
                {
                    new VertexAttribute(VertexSemantic.Position, enums.Float, 2, 0, 0),
                    new VertexAttribute(VertexSemantic.Color, enums.Float, 4, 8, 0),
                    new VertexAttribute(VertexSemantic.TexCoord, enums.Float, 2, 24, 0)
                });
            }
            return null;
        }

        static ShaderSourceSet VsFs(string vertex, string fragment)
        {
            return new ShaderSourceSet { Vertex = vertex, Fragment = fragment };
        }

        static GpuRenderState DefaultState(ShaderId id, AbstractRenderEnums enums)
        {
            switch (id)
            {
                case ShaderId.Skybox:
                    return new GpuRenderState { DepthTest = false, DepthWrite = false, CullMode = enums.None, Primitive = enums.Triangles };
                case ShaderId.SkyboxPreview:
                    return new GpuRenderState { DepthTest = true, DepthWrite = true, CullMode = enums.None, Primitive = enums.Triangles };
                case ShaderId.Ui:
                case ShaderId.Text:
                    return new GpuRenderState { DepthTest = false, DepthWrite = false, Blend = true, CullMode = enums.None, Primitive = enums.Triangles };
                case ShaderId.Point:
                    return new GpuRenderState { DepthTest = true, DepthWrite = true, Primitive = enums.Points };
                case ShaderId.Water:
                    return new GpuRenderState { DepthTest = true, DepthWrite = true, Blend = true, Primitive = enums.TriangleFan };
                default:
                    return new GpuRenderState { DepthTest = true, DepthWrite = true, CullMode = enums.Back, Primitive = enums.Triangles };
            }
        }
    }
}
