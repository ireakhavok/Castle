// Folder: SiegeEngine/Core/GPU/Lighting
// File: LightingFrame.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Lighting
{
    public struct GpuDirectionalLight
    {
        public Vector3 Direction;
        public Vector3 Color;
        public float Intensity;
        public bool CastShadows;
        public float ShadowBias;
        public float ShadowNormalBias;
        public ShadowTechnique Technique;
    }

    public struct GpuPointLight
    {
        public Vector3 Position;
        public Vector3 Color;
        public float Intensity;
        public float Range;
        public float AttenuationLinear;
        public float AttenuationQuadratic;
        public bool CastShadows;
        public ShadowTechnique Technique;
    }

    public struct GpuSpotLight
    {
        public Vector3 Position;
        public Vector3 Direction;
        public Vector3 Color;
        public float Intensity;
        public float Range;
        public float InnerConeCos;
        public float OuterConeCos;
        public bool CastShadows;
        public ShadowTechnique Technique;
    }

    public struct GpuFogState
    {
        public FogMode Mode;
        public FogQuality Quality;
        public Vector3 Color;
        public float Density;
        public float Height;
        public float HeightFalloff;
        public float VolumetricIntensity;
        public int RaySteps;
    }

    /// <summary>
    /// Renderer-facing packed lighting state for the current frame.
    /// Set by Scene before world drawing. Renderers consume this instead of
    /// hard-coding a sun direction.
    /// </summary>
    public sealed class LightingFrame
    {
        public const int MaxPointLights = 4;
        public const int MaxSpotLights = 2;
        public const int MaxCascades = 4;
        public const int ShadowAtlasUnit = 12;
        public const int PointShadowUnit = 13;
        public const int SpotShadowUnit = 14;

        /// <summary>
        /// Z-up world, 3 o'clock sun: light travels from +X (east) slightly
        /// forward of the origin and downward along -Z. Used whenever a scene
        /// has no enabled directional light.
        /// </summary>
        public static readonly Vector3 DefaultSunDirection = Vector3.Normalize(new Vector3(-0.85f, 0.10f, -0.52f));

        public static LightingFrame Current { get; set; }

        public Vector3 AmbientColor = new Vector3(0.30f, 0.30f, 0.34f);
        public GpuDirectionalLight Sun;
        public GpuPointLight[] Points = new GpuPointLight[MaxPointLights];
        public GpuSpotLight[] Spots = new GpuSpotLight[MaxSpotLights];
        public int PointCount;
        public int SpotCount;
        public GpuFogState Fog;
        public ShadowQuality ShadowQuality = ShadowQuality.Medium;
        public int CascadeCount;
        public Matrix4x4[] CascadeVP = new Matrix4x4[MaxCascades];
        public Vector4 CascadeSplits;
        public uint ShadowAtlas;
        public uint PointShadowCube;
        public uint SpotShadowMap;
        public Matrix4x4 SpotVP = Matrix4x4.Identity;
        public bool ShadowsReady;
        public float ShadowDistance = 80f;

        public static LightingFrame Build(IReadOnlyList<Entity> entities, EnvironmentSettings environment, Vector3 fallbackSunDirection)
        {
            var frame = new LightingFrame();
            frame.ShadowQuality = LightingSettings.ResolveShadowQuality();
            frame.ShadowDistance = LightingSettings.ResolveShadowDistance();
            frame.AmbientColor = environment?.AmbientColor ?? new Vector3(0.30f, 0.30f, 0.34f);
            if (frame.AmbientColor.LengthSquared() < 1e-6f)
                frame.AmbientColor = new Vector3(0.30f, 0.30f, 0.34f);

            frame.Fog = new GpuFogState
            {
                Mode = LightingSettings.ResolveFogMode(),
                Quality = LightingSettings.ResolveFogQuality(),
                Color = LightingSettings.ResolveFogColor(),
                Density = LightingSettings.ResolveFogDensity(),
                Height = LightingSettings.ResolveFogHeight(),
                HeightFalloff = LightingSettings.ResolveFogHeightFalloff(),
                VolumetricIntensity = LightingSettings.ResolveVolumetricIntensity(),
                RaySteps = LightingSettings.ResolveFogQuality() switch
                {
                    FogQuality.High => 24,
                    FogQuality.Medium => 16,
                    FogQuality.Low => 8,
                    _ => 0
                }
            };
            if (frame.Fog.Quality == FogQuality.Off)
                frame.Fog.Mode = FogMode.Off;

            bool hasSun = false;
            if (entities != null)
            {
                foreach (var entity in entities)
                {
                    var light = entity.GetComponent<LightComponent>();
                    if (light == null || !light.Enabled)
                        continue;

                    var physics = entity.GetComponent<PhysicsComponent>();
                    if (physics != null && light.Type != LightType.Directional)
                        light.Position = physics.Position;

                    if (light.Type == LightType.Directional && !hasSun)
                    {
                        frame.Sun = PackDirectional(light);
                        hasSun = true;
                    }
                    else if (light.Type == LightType.Point && frame.PointCount < MaxPointLights)
                    {
                        frame.Points[frame.PointCount++] = PackPoint(light);
                    }
                    else if (light.Type == LightType.Spot && frame.SpotCount < MaxSpotLights)
                    {
                        frame.Spots[frame.SpotCount++] = PackSpot(light);
                    }
                }
            }

            if (!hasSun)
            {
                Vector3 dir = fallbackSunDirection.LengthSquared() > 1e-8f
                    ? Vector3.Normalize(fallbackSunDirection)
                    : DefaultSunDirection;
                frame.Sun = new GpuDirectionalLight
                {
                    Direction = dir,
                    Color = Vector3.One,
                    Intensity = 1f,
                    CastShadows = frame.ShadowQuality != ShadowQuality.Off,
                    ShadowBias = 0.002f,
                    ShadowNormalBias = 0.02f,
                    Technique = frame.ShadowQuality == ShadowQuality.Off ? ShadowTechnique.None : ShadowTechnique.ShadowMap
                };
            }

            return frame;
        }

        public void ApplyTo(ShaderProgram shader, IRenderContext renderContext)
        {
            if (shader == null) return;

            shader.SetUniform("uAmbientColor", AmbientColor.X, AmbientColor.Y, AmbientColor.Z);
            shader.SetUniform("uAmbientStrength", 0.30f);
            shader.SetUniform("uLightDir", Sun.Direction.X, Sun.Direction.Y, Sun.Direction.Z);
            shader.SetUniform("uLightColor", Sun.Color.X, Sun.Color.Y, Sun.Color.Z);
            shader.SetUniform("uLightIntensity", Sun.Intensity);

            shader.SetUniform("uPointCount", PointCount);
            for (int i = 0; i < MaxPointLights; i++)
            {
                GpuPointLight p = i < PointCount ? Points[i] : default;
                shader.SetUniform($"uPointPos[{i}]", p.Position.X, p.Position.Y, p.Position.Z);
                shader.SetUniform($"uPointColor[{i}]", p.Color.X, p.Color.Y, p.Color.Z);
                shader.SetUniform($"uPointIntensity[{i}]", p.Intensity);
                shader.SetUniform($"uPointRange[{i}]", p.Range > 0f ? p.Range : 1f);
            }

            shader.SetUniform("uSpotCount", SpotCount);
            for (int i = 0; i < MaxSpotLights; i++)
            {
                GpuSpotLight s = i < SpotCount ? Spots[i] : default;
                shader.SetUniform($"uSpotPos[{i}]", s.Position.X, s.Position.Y, s.Position.Z);
                shader.SetUniform($"uSpotDir[{i}]", s.Direction.X, s.Direction.Y, s.Direction.Z);
                shader.SetUniform($"uSpotColor[{i}]", s.Color.X, s.Color.Y, s.Color.Z);
                shader.SetUniform($"uSpotIntensity[{i}]", s.Intensity);
                shader.SetUniform($"uSpotRange[{i}]", s.Range > 0f ? s.Range : 1f);
                shader.SetUniform($"uSpotInner[{i}]", s.InnerConeCos);
                shader.SetUniform($"uSpotOuter[{i}]", s.OuterConeCos);
            }

            int fogMode = Fog.Mode == FogMode.Off || Fog.Quality == FogQuality.Off ? 0 : (int)Fog.Mode;
            shader.SetUniform("uFogMode", fogMode);
            shader.SetUniform("uFogColor", Fog.Color.X, Fog.Color.Y, Fog.Color.Z);
            shader.SetUniform("uFogDensity", Fog.Density);
            shader.SetUniform("uFogHeight", Fog.Height);
            shader.SetUniform("uFogHeightFalloff", Fog.HeightFalloff);

            bool shadows = ShadowsReady && ShadowQuality != ShadowQuality.Off && Sun.CastShadows && Sun.Technique == ShadowTechnique.ShadowMap;
            shader.SetUniform("uReceiveShadows", 1);
            shader.SetUniform("uShadowsEnabled", shadows ? 1 : 0);
            shader.SetUniform("uCascadeCount", shadows ? CascadeCount : 0);
            shader.SetUniform("uCascadeSplits", CascadeSplits.X, CascadeSplits.Y, CascadeSplits.Z, CascadeSplits.W);
            shader.SetUniform("uShadowBias", Sun.ShadowBias);
            shader.SetUniform("uShadowNormalBias", Sun.ShadowNormalBias);
            shader.SetUniform("uShadowAtlasSize", ShadowQuality switch
            {
                ShadowQuality.Ultra => 4096f,
                ShadowQuality.Low => 1024f,
                _ => 2048f
            });

            for (int i = 0; i < MaxCascades; i++)
                shader.SetMatrix4($"uCascadeVP[{i}]", CascadeVP[i]);

            shader.SetMatrix4("uSpotVP", SpotVP);
            shader.SetUniform("uSpotShadowsEnabled", shadows && SpotShadowMap != 0 && SpotCount > 0 && Spots[0].CastShadows ? 1 : 0);
            shader.SetUniform("uPointShadowsEnabled", shadows && PointShadowCube != 0 && PointCount > 0 && Points[0].CastShadows ? 1 : 0);

            if (renderContext == null)
                return;

            renderContext.ActiveTexture(renderContext.Enums.Texture0 + ShadowAtlasUnit);
            renderContext.BindTexture(renderContext.Enums.Texture2D, shadows ? ShadowAtlas : 0);
            shader.SetUniform("uShadowAtlas", ShadowAtlasUnit);

            renderContext.ActiveTexture(renderContext.Enums.Texture0 + PointShadowUnit);
            renderContext.BindTexture(renderContext.Enums.TextureCubeMap, PointShadowCube);
            shader.SetUniform("uPointShadowCube", PointShadowUnit);

            renderContext.ActiveTexture(renderContext.Enums.Texture0 + SpotShadowUnit);
            renderContext.BindTexture(renderContext.Enums.Texture2D, SpotShadowMap);
            shader.SetUniform("uSpotShadowMap", SpotShadowUnit);

            renderContext.ActiveTexture(renderContext.Enums.Texture0);
        }

        private static GpuDirectionalLight PackDirectional(LightComponent light)
        {
            return new GpuDirectionalLight
            {
                Direction = light.ResolvedDirection(),
                Color = light.Color,
                Intensity = light.Intensity,
                CastShadows = light.CastShadows,
                ShadowBias = light.ShadowBias,
                ShadowNormalBias = light.ShadowNormalBias,
                Technique = LightingSettings.ResolveTechnique(light)
            };
        }

        private static GpuPointLight PackPoint(LightComponent light)
        {
            return new GpuPointLight
            {
                Position = light.Position,
                Color = light.Color,
                Intensity = light.Intensity,
                Range = light.Range > 0f ? light.Range : 25f,
                AttenuationLinear = light.AttenuationLinear,
                AttenuationQuadratic = light.AttenuationQuadratic,
                CastShadows = light.CastShadows,
                Technique = LightingSettings.ResolveTechnique(light)
            };
        }

        private static GpuSpotLight PackSpot(LightComponent light)
        {
            float inner = MathF.Cos(light.InnerConeDegrees * MathF.PI / 180f);
            float outer = MathF.Cos(light.OuterConeDegrees * MathF.PI / 180f);
            if (outer > inner)
            {
                float tmp = inner;
                inner = outer;
                outer = tmp;
            }
            return new GpuSpotLight
            {
                Position = light.Position,
                Direction = light.ResolvedDirection(),
                Color = light.Color,
                Intensity = light.Intensity,
                Range = light.Range > 0f ? light.Range : 25f,
                InnerConeCos = inner,
                OuterConeCos = outer,
                CastShadows = light.CastShadows,
                Technique = LightingSettings.ResolveTechnique(light)
            };
        }
    }
}
