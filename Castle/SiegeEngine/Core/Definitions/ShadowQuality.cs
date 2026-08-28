// Folder: SiegeEngine/Core/Definitions
// File: ShadowQuality.cs
namespace SiegeEngine.Core.Definitions
{
    public enum ShadowQuality
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4
    }

    public static class ShadowQualityParser
    {
        public static bool TryParse(string value, out ShadowQuality quality)
        {
            quality = ShadowQuality.Medium;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToUpperInvariant())
            {
                case "OFF":
                case "NONE":
                case "0":
                    quality = ShadowQuality.Off;
                    return true;
                case "LOW":
                case "1":
                    quality = ShadowQuality.Low;
                    return true;
                case "MEDIUM":
                case "MED":
                case "2":
                    quality = ShadowQuality.Medium;
                    return true;
                case "HIGH":
                case "3":
                    quality = ShadowQuality.High;
                    return true;
                case "ULTRA":
                case "4":
                    quality = ShadowQuality.Ultra;
                    return true;
                default:
                    return false;
            }
        }

        public static string ToPayloadString(ShadowQuality quality)
        {
            return quality switch
            {
                ShadowQuality.Off => "Off",
                ShadowQuality.Low => "Low",
                ShadowQuality.High => "High",
                ShadowQuality.Ultra => "Ultra",
                _ => "Medium"
            };
        }
    }
}
