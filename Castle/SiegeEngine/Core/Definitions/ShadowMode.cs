// Folder: SiegeEngine/Core/Definitions
// File: ShadowMode.cs
namespace SiegeEngine.Core.Definitions
{
    /// <summary>
    /// Per-light shadow technique. Auto uses ray tracing when the platform
    /// exposes it and falls back to shadow maps otherwise. Gameplay and
    /// level content should prefer Auto so a future RT path requires no
    /// content changes.
    /// </summary>
    public enum ShadowMode
    {
        Auto = 0,
        ShadowMap = 1,
        RayTraced = 2,
        Off = 3
    }

    public static class ShadowModeParser
    {
        public static bool TryParse(string value, out ShadowMode mode)
        {
            mode = ShadowMode.Auto;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToUpperInvariant())
            {
                case "AUTO":
                case "0":
                    mode = ShadowMode.Auto;
                    return true;
                case "SHADOWMAP":
                case "SHADOW_MAP":
                case "MAP":
                case "1":
                    mode = ShadowMode.ShadowMap;
                    return true;
                case "RAYTRACED":
                case "RAY_TRACED":
                case "RT":
                case "2":
                    mode = ShadowMode.RayTraced;
                    return true;
                case "OFF":
                case "NONE":
                case "3":
                    mode = ShadowMode.Off;
                    return true;
                default:
                    return false;
            }
        }

        public static string ToPayloadString(ShadowMode mode)
        {
            return mode switch
            {
                ShadowMode.ShadowMap => "ShadowMap",
                ShadowMode.RayTraced => "RayTraced",
                ShadowMode.Off => "Off",
                _ => "Auto"
            };
        }
    }
}
