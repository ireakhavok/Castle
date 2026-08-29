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
        public float Start;
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
        /// forward of the origin and downward along -Z.
        /// </summary>
        public static readonly Vector3 DefaultSunDirection = Vector3.Normalize(new Vector3(-0.85f, 0.10f, -0.52f));

        public static LightingFrame Current { get; set; }

        /// <summary>
        /// Last frame that actually filled a sun atlas. Views that Build()
        /// without running ShadowMapRenderer.Render leave Current with
        /// ShadowAtlas=0 / ShadowsReady=false. Models then sample texture 0
        /// (magenta cap). Inherit this atlas + cascade VPs instead.
        /// </summary>
        public static LightingFrame LastReady { get; set; }

        /// <summary>
        /// Temporary sun-shadow diagnosis. Set back to 0 for normal lighting.
        /// 0 off, 1 shadow factor, 2 cap mask (RED = sun-facing and darkened),
        /// 3 depth delta, 4 cascade index, 5 stored depth, 6 receiver depth.
        /// </summary>
        public static int ShadowDebugMode = 0;

        public Vector3 AmbientColor = new Vector3(0.45f, 0.45f, 0.48f);
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
        public Vector4 CascadeZRange;
        public uint ShadowAtlas;
        public uint PointShadowCube;
        public uint SpotShadowMap;
        public Matrix4x4 SpotVP = Matrix4x4.Identity;
        public bool ShadowsReady;
        public float ShadowDistance = 2048f;

        public static LightingFrame Build(IReadOnlyList<Entity> entities, EnvironmentSettings environment, Vector3 fallbackSunDirection, bool allowFallbackSun = true)
        {
            var frame = new LightingFrame();
            frame.ShadowQuality = LightingSettings.ResolveShadowQuality();
            frame.ShadowDistance = LightingSettings.ResolveShadowDistance();
            frame.AmbientColor = environment?.AmbientColor ?? new Vector3(0.45f, 0.45f, 0.48f);
            if (frame.AmbientColor.LengthSquared() < 1e-6f)
                frame.AmbientColor = new Vector3(0.45f, 0.45f, 0.48f);

            frame.Fog = new GpuFogState
            {
                Mode = LightingSettings.ResolveFogMode(),
                Quality = LightingSettings.ResolveFogQuality(),
                Color = LightingSettings.ResolveFogColor(),
                Density = LightingSettings.ResolveFogDensity(),
                Start = LightingSettings.ResolveFogStart(),
                Height = LightingSettings.ResolveFogHeight(),
                HeightFalloff = LightingSettings.ResolveFogHeightFalloff(),
                VolumetricIntensity = LightingSettings.ResolveVolumetricIntensity(),
                RaySteps = LightingSettings.ResolveFogQuality() switch
                {
                    FogQuality.High => 32,
                    FogQuality.Medium => 24,
                    FogQuality.Low => 16,
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

            // Editor has no implicit sun (AllowRuntimeDefaultSun = false).
            // Apply persists SunEnabled. Casting shadows without the
            // Enable-sun checkbox used to leave this branch dead and the
            // atlas was never drawn.
            bool wantEnvSun = environment != null && environment.SunEnabled;
            if (!hasSun && wantEnvSun)
            {
                Vector3 dir = environment.SunDirection.LengthSquared() > 1e-8f
                    ? Vector3.Normalize(environment.SunDirection)
                    : DefaultSunDirection;
                float intensity = environment.SunIntensity < 0f ? 0f : environment.SunIntensity;
                if (intensity <= 0.001f)
                    intensity = 1f;
                Vector3 color = environment.SunColor.LengthSquared() < 1e-6f ? Vector3.One : environment.SunColor;
                bool cast = environment.SunCastShadows && frame.ShadowQuality != ShadowQuality.Off;
                frame.Sun = new GpuDirectionalLight
                {
                    Direction = dir,
                    Color = color,
                    Intensity = intensity,
                    CastShadows = cast,
                    ShadowBias = 0.0015f,
                    ShadowNormalBias = 0.035f,
                    Technique = cast ? ShadowTechnique.ShadowMap : ShadowTechnique.None
                };
                hasSun = true;
            }

            // Play Game still gets a directional sun even when point lights
            // exist. Point lights must not suppress the cascade atlas.
            // If the authored environment explicitly turned the sun off,
            // do not inject a default sun (editor Post Process toggle).
            bool allowFallback = allowFallbackSun && (environment == null || environment.SunEnabled);
            if (!hasSun && allowFallback)
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
                    ShadowBias = 0.0015f,
                    ShadowNormalBias = 0.035f,
                    Technique = frame.ShadowQuality == ShadowQuality.Off ? ShadowTechnique.None : ShadowTechnique.ShadowMap
                };
            }
            else if (!hasSun)
            {
                frame.Sun = new GpuDirectionalLight
                {
                    Direction = DefaultSunDirection,
                    Color = Vector3.One,
                    Intensity = 0f,
                    CastShadows = false,
                    ShadowBias = 0.002f,
                    ShadowNormalBias = 0.02f,
                    Technique = ShadowTechnique.None
                };
            }

            return frame;
        }

        public void ApplyTo(ShaderProgram shader, IRenderContext renderContext)
        {
            if (shader == null) return;

            shader.SetUniform("uAmbientColor", AmbientColor.X, AmbientColor.Y, AmbientColor.Z);
            shader.SetUniform("uAmbientStrength", 0.16f);
            shader.SetUniform("uLightDir", Sun.Direction.X, Sun.Direction.Y, Sun.Direction.Z);
            shader.SetUniform("uLightColor", Sun.Color.X, Sun.Color.Y, Sun.Color.Z);
            float sunPunch = Sun.Intensity <= 0f ? 0f : MathF.Min(Sun.Intensity * 1.5f, 4f);
            shader.SetUniform("uLightIntensity", sunPunch);

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
            shader.SetUniform("uFogStart", Fog.Start);
            shader.SetUniform("uFogHeight", Fog.Height);
            shader.SetUniform("uFogHeightFalloff", Fog.HeightFalloff);

            LightingFrame ready = (ShadowsReady && ShadowAtlas != 0) ? this : LastReady;
            uint atlas;
            int cascadeCount;
            Vector4 splits;
            Matrix4x4[] cascades;
            if (ShadowMapRenderer.WrittenSunAtlas != 0)
            {
                atlas = ShadowMapRenderer.WrittenSunAtlas;
                cascadeCount = ShadowMapRenderer.WrittenCascadeCount;
                splits = ShadowMapRenderer.WrittenCascadeSplits;
                cascades = ShadowMapRenderer.WrittenCascadeVP;
            }
            else
            {
                atlas = ready != null ? ready.ShadowAtlas : 0;
                cascadeCount = ready != null ? ready.CascadeCount : 0;
                splits = ready != null ? ready.CascadeSplits : default;
                cascades = ready != null ? ready.CascadeVP : CascadeVP;
            }
            bool shadows = atlas != 0 && ShadowQuality != ShadowQuality.Off && Sun.CastShadows && Sun.Technique == ShadowTechnique.ShadowMap;
            shader.SetUniform("uReceiveShadows", 1);
            shader.SetUniform("uShadowsEnabled", shadows ? 1 : 0);
            shader.SetUniform("uShadowDebug", ShadowDebugMode);
            shader.SetUniform("uCascadeCount", shadows ? cascadeCount : 0);
            shader.SetUniform("uCascadeSplits", splits.X, splits.Y, splits.Z, splits.W);
            Vector4 zRange = ShadowMapRenderer.WrittenCascadeZRange;
            if (zRange == default && ready != null)
                zRange = ready.CascadeZRange;
            shader.SetUniform("uShadowBias", Sun.ShadowBias > 0f ? Sun.ShadowBias : 0.0015f);
            shader.SetUniform("uShadowNormalBias", Sun.ShadowNormalBias > 0f ? Sun.ShadowNormalBias : 0.035f);
            shader.SetUniform("uShadowAtlasSize", ShadowQuality switch
            {
                ShadowQuality.Ultra => 4096f,
                ShadowQuality.High => 4096f,
                ShadowQuality.Low => 1024f,
                _ => 2048f
            });
            shader.SetUniform("uShadowStrength", ShadowQuality switch
            {
                ShadowQuality.Low => 0.14f,
                ShadowQuality.Ultra => 0.05f,
                _ => 0.08f
            });
            shader.SetUniform("uShadowPcfRadius", ShadowQuality switch
            {
                ShadowQuality.Ultra => 3,
                ShadowQuality.High => 3,
                ShadowQuality.Low => 1,
                _ => 2
            });
            shader.SetUniform("uPointShadowStrength", 0.15f);

            for (int i = 0; i < MaxCascades; i++)
                shader.SetMatrix4($"uCascadeVP[{i}]", cascades != null && i < cascades.Length ? cascades[i] : Matrix4x4.Identity);

            shader.SetMatrix4("uSpotVP", SpotVP);
            // Point and spot maps are a separate pass from the sun atlas.
            // Turning the sun off must not zero uPointShadowsEnabled.
            bool pointShadows = ShadowQuality != ShadowQuality.Off && PointShadowCube != 0 && PointCount > 0 && Points[0].CastShadows;
            bool spotShadows = ShadowQuality != ShadowQuality.Off && SpotShadowMap != 0 && SpotCount > 0 && Spots[0].CastShadows;
            shader.SetUniform("uSpotShadowsEnabled", spotShadows ? 1 : 0);
            shader.SetUniform("uPointShadowsEnabled", pointShadows ? 1 : 0);
            shader.SetUniform("uPointShadowFar", PointCount > 0 && Points[0].Range > 0f ? Points[0].Range : 1f);

            if (renderContext == null)
                return;

            renderContext.ActiveTexture(renderContext.Enums.Texture0 + ShadowAtlasUnit);
            renderContext.BindTexture(renderContext.Enums.Texture2D, shadows ? atlas : 0);
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
