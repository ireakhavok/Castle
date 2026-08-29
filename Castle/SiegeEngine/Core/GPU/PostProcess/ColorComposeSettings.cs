// Folder: SiegeEngine/Core/GPU/PostProcess
// File: ColorComposeSettings.cs
using SiegeEngine.Core.Definitions;
using System;

namespace SiegeEngine.Core.GPU.PostProcess
{
    public enum TonemapMode
    {
        Off = 0,
        Aces = 1,
        Reinhard = 2
    }

    /// <summary>
    /// Authored exposure / bloom / grade. Bound from EnvironmentSettings
    /// the same way AA and lighting are bound.
    /// </summary>
    public readonly struct ColorComposeState
    {
        public readonly float Exposure;
        public readonly TonemapMode Tonemap;
        public readonly bool BloomEnabled;
        public readonly float BloomThreshold;
        public readonly float BloomIntensity;
        public readonly float Contrast;
        public readonly float Saturation;
        public readonly float Temperature;

        public ColorComposeState(
            float exposure,
            TonemapMode tonemap,
            bool bloomEnabled,
            float bloomThreshold,
            float bloomIntensity,
            float contrast,
            float saturation,
            float temperature)
        {
            Exposure = Math.Clamp(exposure, 0.05f, 8f);
            Tonemap = tonemap;
            BloomEnabled = bloomEnabled;
            BloomThreshold = Math.Clamp(bloomThreshold, 0.05f, 8f);
            BloomIntensity = Math.Clamp(bloomIntensity, 0f, 4f);
            Contrast = Math.Clamp(contrast, 0.2f, 3f);
            Saturation = Math.Clamp(saturation, 0f, 3f);
            Temperature = Math.Clamp(temperature, -1f, 1f);
        }

        public static ColorComposeState Neutral => new ColorComposeState(
            1f, TonemapMode.Off, false, 1f, 0.35f, 1f, 1f, 0f);

        public bool IsIdentity
        {
            get
            {
                if (BloomEnabled && BloomIntensity > 0.001f)
                    return false;
                if (Tonemap != TonemapMode.Off)
                    return false;
                if (MathF.Abs(Exposure - 1f) > 0.001f)
                    return false;
                if (MathF.Abs(Contrast - 1f) > 0.001f)
                    return false;
                if (MathF.Abs(Saturation - 1f) > 0.001f)
                    return false;
                if (MathF.Abs(Temperature) > 0.001f)
                    return false;
                return true;
            }
        }

        public bool NeedsPass => !IsIdentity;
    }

    public static class ColorComposeSettings
    {
        private static EnvironmentSettings _authored;

        public static void BindAuthored(EnvironmentSettings environment)
        {
            _authored = environment;
        }

        public static ColorComposeState Resolve()
        {
            return FromEnvironment(_authored);
        }

        public static ColorComposeState FromEnvironment(EnvironmentSettings env)
        {
            if (env == null)
                return ColorComposeState.Neutral;

            TonemapMode tonemap = ParseTonemap(env.Tonemap);
            return new ColorComposeState(
                env.Exposure <= 0f ? 1f : env.Exposure,
                tonemap,
                env.BloomEnabled,
                env.BloomThreshold <= 0f ? 1f : env.BloomThreshold,
                env.BloomIntensity,
                env.Contrast <= 0f ? 1f : env.Contrast,
                env.Saturation < 0f ? 1f : env.Saturation,
                env.Temperature);
        }

        public static TonemapMode ParseTonemap(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TonemapMode.Off;
            switch (value.Trim().ToLowerInvariant())
            {
                case "aces":
                case "ace":
                    return TonemapMode.Aces;
                case "reinhard":
                    return TonemapMode.Reinhard;
                default:
                    return TonemapMode.Off;
            }
        }

        public static string ToPayloadString(TonemapMode mode)
        {
            return mode switch
            {
                TonemapMode.Off => "Off",
                TonemapMode.Reinhard => "Reinhard",
                _ => "ACES"
            };
        }
    }
}
