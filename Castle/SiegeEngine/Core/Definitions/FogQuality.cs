// Folder: SiegeEngine/Core/Definitions
// File: FogQuality.cs
namespace SiegeEngine.Core.Definitions
{
    public enum FogQuality
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public static class FogQualityParser
    {
        public static bool TryParse(string value, out FogQuality quality)
        {
            quality = FogQuality.Medium;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToUpperInvariant())
            {
                case "OFF":
                case "NONE":
                case "0":
                    quality = FogQuality.Off;
                    return true;
                case "LOW":
                case "1":
                    quality = FogQuality.Low;
                    return true;
                case "MEDIUM":
                case "MED":
                case "2":
                    quality = FogQuality.Medium;
                    return true;
                case "HIGH":
                case "3":
                    quality = FogQuality.High;
                    return true;
                default:
                    return false;
            }
        }

        public static string ToPayloadString(FogQuality quality)
        {
            return quality switch
            {
                FogQuality.Off => "Off",
                FogQuality.Low => "Low",
                FogQuality.High => "High",
                _ => "Medium"
            };
        }
    }
}
