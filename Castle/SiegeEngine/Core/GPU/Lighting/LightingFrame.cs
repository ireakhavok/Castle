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
        public bool ShadowSmooth;

        public static int UploadSerial { get; private set; }

        private struct TrackedLight
        {
            public Entity Entity;
            public LightType Type;
            public bool Enabled;
            public Vector3 Color;
            public float Intensity;
            public Vector3 Direction;
            public float Range;
            public float InnerConeDegrees;
            public float OuterConeDegrees;
            public float AttenuationLinear;
            public float AttenuationQuadratic;
            public bool CastShadows;
            public ShadowMode ShadowMode;
            public float ShadowBias;
            public float ShadowNormalBias;
            public Vector3 PhysicsPosition;
        }

        private struct PackedSettingsKey
        {
            public ShadowQuality Quality;
            public bool Smooth;
            public float ShadowDistance;
            public FogMode FogMode;
            public FogQuality FogQuality;
            public Vector3 FogColor;
            public float FogDensity;
            public float FogStart;
            public float FogHeight;
            public float FogFalloff;
            public float Volumetric;
            public Vector3 Ambient;
            public bool SunEnabled;
            public Vector3 SunDir;
            public Vector3 SunColor;
            public float SunIntensity;
            public bool SunCast;
            public bool AllowFallback;
            public Vector3 FallbackDir;
            public int PackedRevision;
            public int EntityCount;
        }

        private static bool _packedValid;
        private static PackedSettingsKey _packedSettings;
        private static GpuDirectionalLight _packedSun;
        private static readonly GpuPointLight[] _packedPoints = new GpuPointLight[MaxPointLights];
        private static readonly GpuSpotLight[] _packedSpots = new GpuSpotLight[MaxSpotLights];
        private static int _packedPointCount;
        private static int _packedSpotCount;
        private static readonly List<TrackedLight> _trackedLights = new List<TrackedLight>();

        public static LightingFrame Build(IReadOnlyList<Entity> entities, EnvironmentSettings environment, Vector3 fallbackSunDirection, bool allowFallbackSun = true, LightingFrame dest = null)
        {
            var frame = dest ?? new LightingFrame();
            UploadSerial++;
            frame.ShadowsReady = false;
            frame.ShadowAtlas = 0;
            frame.PointShadowCube = 0;
            frame.SpotShadowMap = 0;
            frame.CascadeCount = 0;
            ApplyResolvedSettings(frame, environment);

            PackedSettingsKey settings = CaptureSettings(environment, fallbackSunDirection, allowFallbackSun, entities);
            bool membershipSame = _packedValid
                && settings.PackedRevision == _packedSettings.PackedRevision
                && settings.EntityCount == _packedSettings.EntityCount
                && TrackedEntitiesAlive();
            if (membershipSame && SettingsMatch(_packedSettings, settings) && TrackedLightsUnchanged())
            {
                RestorePackedLights(frame);
                return frame;
            }
            if (membershipSame)
            {
                bool trackedSun = PackFromTracked(frame);
                ApplyEnvironmentSun(frame, environment, fallbackSunDirection, allowFallbackSun, trackedSun);
                StorePackedLights(frame, settings);
                return frame;
            }

            frame.PointCount = 0;
            frame.SpotCount = 0;
            _trackedLights.Clear();

            bool hasSun = false;
            if (entities != null)
            {
                foreach (var entity in entities)
                {
                    var light = entity.GetComponent<LightComponent>();
                    if (light == null)
                        continue;

                    var physics = entity.GetComponent<PhysicsComponent>();
                    if (physics != null && light.Type != LightType.Directional)
                        light.Position = physics.Position;

                    RememberTrackedLight(entity, light, physics);
                    if (!light.Enabled)
                        continue;

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

            ApplyEnvironmentSun(frame, environment, fallbackSunDirection, allowFallbackSun, hasSun);
            StorePackedLights(frame, settings);
            return frame;
        }

        private static void ApplyResolvedSettings(LightingFrame frame, EnvironmentSettings environment)
        {
            frame.ShadowQuality = LightingSettings.ResolveShadowQuality();
            frame.ShadowSmooth = LightingSettings.ResolveShadowSmooth();
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
        }

        private static PackedSettingsKey CaptureSettings(EnvironmentSettings environment, Vector3 fallbackSunDirection, bool allowFallbackSun, IReadOnlyList<Entity> entities)
        {
            return new PackedSettingsKey
            {
                Quality = LightingSettings.ResolveShadowQuality(),
                Smooth = LightingSettings.ResolveShadowSmooth(),
                ShadowDistance = LightingSettings.ResolveShadowDistance(),
                FogMode = LightingSettings.ResolveFogMode(),
                FogQuality = LightingSettings.ResolveFogQuality(),
                FogColor = LightingSettings.ResolveFogColor(),
                FogDensity = LightingSettings.ResolveFogDensity(),
                FogStart = LightingSettings.ResolveFogStart(),
                FogHeight = LightingSettings.ResolveFogHeight(),
                FogFalloff = LightingSettings.ResolveFogHeightFalloff(),
                Volumetric = LightingSettings.ResolveVolumetricIntensity(),
                Ambient = environment?.AmbientColor ?? new Vector3(0.45f, 0.45f, 0.48f),
                SunEnabled = environment != null && environment.SunEnabled,
                SunDir = environment != null ? environment.SunDirection : default,
                SunColor = environment != null ? environment.SunColor : default,
                SunIntensity = environment != null ? environment.SunIntensity : 0f,
                SunCast = environment == null || environment.SunCastShadows,
                AllowFallback = allowFallbackSun,
                FallbackDir = fallbackSunDirection,
                PackedRevision = LightComponent.PackedRevision,
                EntityCount = entities != null ? entities.Count : 0
            };
        }

        private static bool SettingsMatch(PackedSettingsKey a, PackedSettingsKey b)
        {
            return a.Quality == b.Quality
                && a.Smooth == b.Smooth
                && a.ShadowDistance == b.ShadowDistance
                && a.FogMode == b.FogMode
                && a.FogQuality == b.FogQuality
                && a.FogColor == b.FogColor
                && a.FogDensity == b.FogDensity
                && a.FogStart == b.FogStart
                && a.FogHeight == b.FogHeight
                && a.FogFalloff == b.FogFalloff
                && a.Volumetric == b.Volumetric
                && a.Ambient == b.Ambient
                && a.SunEnabled == b.SunEnabled
                && a.SunDir == b.SunDir
                && a.SunColor == b.SunColor
                && a.SunIntensity == b.SunIntensity
                && a.SunCast == b.SunCast
                && a.AllowFallback == b.AllowFallback
                && a.FallbackDir == b.FallbackDir
                && a.PackedRevision == b.PackedRevision
                && a.EntityCount == b.EntityCount;
        }

        private static bool TrackedEntitiesAlive()
        {
            for (int i = 0; i < _trackedLights.Count; i++)
            {
                Entity entity = _trackedLights[i].Entity;
                if (entity == null || entity.GetComponent<LightComponent>() == null)
                    return false;
            }
            return true;
        }

        private static bool TrackedLightsUnchanged()
        {
            for (int i = 0; i < _trackedLights.Count; i++)
            {
                TrackedLight t = _trackedLights[i];
                if (t.Entity == null)
                    return false;
                LightComponent light = t.Entity.GetComponent<LightComponent>();
                if (light == null)
                    return false;
                if (light.Type != t.Type
                    || light.Enabled != t.Enabled
                    || light.Color != t.Color
                    || light.Intensity != t.Intensity
                    || light.Direction != t.Direction
                    || light.Range != t.Range
                    || light.InnerConeDegrees != t.InnerConeDegrees
                    || light.OuterConeDegrees != t.OuterConeDegrees
                    || light.AttenuationLinear != t.AttenuationLinear
                    || light.AttenuationQuadratic != t.AttenuationQuadratic
                    || light.CastShadows != t.CastShadows
                    || light.ShadowMode != t.ShadowMode
                    || light.ShadowBias != t.ShadowBias
                    || light.ShadowNormalBias != t.ShadowNormalBias)
                    return false;
                if (light.Type != LightType.Directional)
                {
                    PhysicsComponent physics = t.Entity.GetComponent<PhysicsComponent>();
                    Vector3 pos = physics != null ? physics.Position : t.PhysicsPosition;
                    if (pos != t.PhysicsPosition)
                        return false;
                }
            }
            return true;
        }

        private static bool PackFromTracked(LightingFrame frame)
        {
            frame.PointCount = 0;
            frame.SpotCount = 0;
            bool hasSun = false;
            for (int i = 0; i < _trackedLights.Count; i++)
            {
                TrackedLight t = _trackedLights[i];
                LightComponent light = t.Entity != null ? t.Entity.GetComponent<LightComponent>() : null;
                if (light == null)
                    continue;
                PhysicsComponent physics = t.Entity.GetComponent<PhysicsComponent>();
                if (physics != null && light.Type != LightType.Directional)
                    light.Position = physics.Position;
                RememberTrackedLightAt(i, t.Entity, light, physics);
                if (!light.Enabled)
                    continue;
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
            return hasSun;
        }

        private static void ApplyEnvironmentSun(LightingFrame frame, EnvironmentSettings environment, Vector3 fallbackSunDirection, bool allowFallbackSun, bool hasSun)
        {
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
                    ShadowNormalBias = 0.02f,
                    Technique = cast ? ShadowTechnique.ShadowMap : ShadowTechnique.None
                };
                return;
            }

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
                    ShadowNormalBias = 0.02f,
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
        }

        private static void RememberTrackedLightAt(int index, Entity entity, LightComponent light, PhysicsComponent physics)
        {
            var tracked = new TrackedLight
            {
                Entity = entity,
                Type = light.Type,
                Enabled = light.Enabled,
                Color = light.Color,
                Intensity = light.Intensity,
                Direction = light.Direction,
                Range = light.Range,
                InnerConeDegrees = light.InnerConeDegrees,
                OuterConeDegrees = light.OuterConeDegrees,
                AttenuationLinear = light.AttenuationLinear,
                AttenuationQuadratic = light.AttenuationQuadratic,
                CastShadows = light.CastShadows,
                ShadowMode = light.ShadowMode,
                ShadowBias = light.ShadowBias,
                ShadowNormalBias = light.ShadowNormalBias,
                PhysicsPosition = physics != null ? physics.Position : light.Position
            };
            if (index >= 0 && index < _trackedLights.Count)
                _trackedLights[index] = tracked;
            else
                _trackedLights.Add(tracked);
        }

        private static void RememberTrackedLight(Entity entity, LightComponent light, PhysicsComponent physics)
        {
            RememberTrackedLightAt(-1, entity, light, physics);
        }

        private static void StorePackedLights(LightingFrame frame, PackedSettingsKey settings)
        {
            _packedSun = frame.Sun;
            _packedPointCount = frame.PointCount;
            _packedSpotCount = frame.SpotCount;
            for (int i = 0; i < MaxPointLights; i++)
                _packedPoints[i] = i < frame.PointCount ? frame.Points[i] : default;
            for (int i = 0; i < MaxSpotLights; i++)
                _packedSpots[i] = i < frame.SpotCount ? frame.Spots[i] : default;
            _packedSettings = settings;
            _packedValid = true;
        }

        private static void RestorePackedLights(LightingFrame frame)
        {
            frame.Sun = _packedSun;
            frame.PointCount = _packedPointCount;
            frame.SpotCount = _packedSpotCount;
            for (int i = 0; i < MaxPointLights; i++)
                frame.Points[i] = _packedPoints[i];
            for (int i = 0; i < MaxSpotLights; i++)
                frame.Spots[i] = _packedSpots[i];
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
            shader.SetUniform("uCascadeCount", shadows ? cascadeCount : 0);
            shader.SetUniform("uCascadeSplits", splits.X, splits.Y, splits.Z, splits.W);
            Vector4 zRange = ShadowMapRenderer.WrittenCascadeZRange;
            if (zRange == default && ready != null)
                zRange = ready.CascadeZRange;
            shader.SetUniform("uCascadeZRange", zRange.X, zRange.Y, zRange.Z, zRange.W);
            shader.SetUniform("uShadowBias", ShadowMapRenderer.EsmExponent(ShadowQuality));
            float atlasPx = ShadowMapRenderer.WrittenAtlasSize > 0
                ? ShadowMapRenderer.WrittenAtlasSize
                : ShadowMapRenderer.AtlasSize(ShadowQuality);
            shader.SetUniform("uShadowAtlasSize", atlasPx);
            shader.SetUniform("uShadowStrength", ShadowMapRenderer.ShadowStrength(ShadowQuality));
            shader.SetUniform("uShadowSmooth", ShadowSmooth ? 1 : 0);
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

        public static LightingFrame Studio(Vector3 cameraPos, Vector3 lookTarget)
        {
            Vector3 toModel = lookTarget - cameraPos;
            float dist = toModel.Length();
            if (dist < 1e-4f)
            {
                toModel = new Vector3(0f, 1f, 0f);
                dist = 1f;
            }
            else
                toModel /= dist;

            // Key sits above the model and a little toward the camera (in front of the subject).
            Vector3 keyPos = lookTarget - toModel * (dist * 0.22f) + Vector3.UnitZ * MathF.Max(1.6f, dist * 0.18f);
            Vector3 travel = lookTarget - keyPos;
            if (travel.LengthSquared() < 1e-8f)
                travel = new Vector3(0f, 0f, -1f);
            travel = Vector3.Normalize(travel);

            UploadSerial++;
            var frame = new LightingFrame
            {
                AmbientColor = new Vector3(0.48f, 0.49f, 0.52f),
                Sun = new GpuDirectionalLight
                {
                    Direction = travel,
                    Color = new Vector3(1f, 0.97f, 0.93f),
                    Intensity = 0.42f,
                    CastShadows = false,
                    ShadowBias = 0f,
                    ShadowNormalBias = 0f,
                    Technique = ShadowTechnique.None
                },
                Fog = new GpuFogState { Mode = FogMode.Off, Quality = FogQuality.Off },
                ShadowQuality = ShadowQuality.Off,
                ShadowsReady = false,
                ShadowAtlas = 0,
                PointShadowCube = 0,
                SpotShadowMap = 0,
                PointCount = 1,
                SpotCount = 0,
                CascadeCount = 0,
                ShadowSmooth = false
            };
            frame.Points[0] = new GpuPointLight
            {
                Position = keyPos,
                Color = new Vector3(1f, 0.96f, 0.90f),
                Intensity = 0.22f,
                Range = MathF.Max(8f, dist * 1.4f),
                CastShadows = false,
                Technique = ShadowTechnique.None
            };
            return frame;
        }

        public static LightingFrame BuildStudio(Vector3 keyDirection)
        {
            Vector3 dir = keyDirection.LengthSquared() < 1e-8f ? new Vector3(0f, 1f, 0f) : Vector3.Normalize(keyDirection);
            return Studio(-dir * 8f, Vector3.Zero);
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
