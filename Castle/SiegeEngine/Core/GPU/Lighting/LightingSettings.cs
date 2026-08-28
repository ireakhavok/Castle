// Folder: SiegeEngine/Core/GPU/Lighting
// File: LightingSettings.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Managers;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Lighting
{
    public static class LightingSettings
    {
        private static UISettingsManager _machine;
        private static EnvironmentSettings _authored;
        private static ShadowQuality? _shadowOverride;
        private static FogQuality? _fogQualityOverride;
        private static FogMode? _fogModeOverride;

        public static bool RayTracingAvailable { get; set; }

        public static void BindMachine(UISettingsManager settings) { _machine = settings; }
        public static void BindAuthored(EnvironmentSettings environment) { _authored = environment; }
        public static void SetShadowOverride(ShadowQuality? quality) { _shadowOverride = quality; }
        public static void SetFogOverride(FogMode? mode, FogQuality? quality) { _fogModeOverride = mode; _fogQualityOverride = quality; }

        public static ShadowQuality ResolveShadowQuality()
        {
            if (_shadowOverride.HasValue) return _shadowOverride.Value;
            if (_machine != null && _machine.HasShadowQualityOverride) return _machine.ShadowQuality;
            if (ShadowQualityParser.TryParse(_authored?.ShadowQuality, out ShadowQuality authored)) return authored;
            return ShadowQuality.Medium;
        }

        public static FogMode ResolveFogMode()
        {
            if (_fogModeOverride.HasValue) return _fogModeOverride.Value;
            if (_machine != null && _machine.HasFogOverride) return _machine.FogMode;
            if (FogModeParser.TryParse(_authored?.FogMode, out FogMode authored)) return authored;
            if ((_authored?.FogDensity ?? 0f) > 0.001f) return FogMode.Exponential;
            return FogMode.Off;
        }

        public static FogQuality ResolveFogQuality()
        {
            if (_fogQualityOverride.HasValue) return _fogQualityOverride.Value;
            if (_machine != null && _machine.HasFogOverride) return _machine.FogQuality;
            if (FogQualityParser.TryParse(_authored?.FogQuality, out FogQuality authored)) return authored;
            return FogQuality.Medium;
        }

        public static float ResolveShadowDistance()
        {
            float authored = _authored?.ShadowDistance ?? 0f;
            return authored > 1f ? authored : 80f;
        }

        public static Vector3 ResolveFogColor()
        {
            Vector3 color = _authored?.FogColor ?? default;
            if (color.LengthSquared() < 1e-6f) return new Vector3(0.62f, 0.70f, 0.82f);
            return color;
        }

        public static float ResolveFogDensity()
        {
            float density = _authored?.FogDensity ?? 0.01f;
            return density < 0f ? 0f : density;
        }

        public static float ResolveFogHeight() => _authored?.FogHeight ?? 8f;

        public static float ResolveFogHeightFalloff()
        {
            float falloff = _authored?.FogHeightFalloff ?? 0.08f;
            return falloff < 0.0001f ? 0.08f : falloff;
        }

        public static float ResolveVolumetricIntensity()
        {
            float intensity = _authored?.VolumetricIntensity ?? 0.45f;
            return intensity < 0f ? 0f : intensity;
        }

        public static ShadowTechnique ResolveTechnique(LightComponent light)
        {
            if (light == null || !light.Enabled || !light.CastShadows) return ShadowTechnique.None;
            ShadowMode requested = light.ShadowMode;
            if (requested == ShadowMode.Off) return ShadowTechnique.None;
            if (requested == ShadowMode.RayTraced) return RayTracingAvailable ? ShadowTechnique.RayTraced : ShadowTechnique.ShadowMap;
            if (requested == ShadowMode.ShadowMap) return ShadowTechnique.ShadowMap;
            return RayTracingAvailable ? ShadowTechnique.RayTraced : ShadowTechnique.ShadowMap;
        }
    }

    public enum ShadowTechnique
    {
        None = 0,
        ShadowMap = 1,
        RayTraced = 2
    }
}
