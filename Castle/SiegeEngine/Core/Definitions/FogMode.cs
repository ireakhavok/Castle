// Folder: SiegeEngine/Core/Definitions
// File: FogMode.cs
namespace SiegeEngine.Core.Definitions
{
    public enum FogMode
    {
        Off = 0,
        Exponential = 1,
        Height = 2,
        Volumetric = 3
    }

    public static class FogModeParser
    {
        public static bool TryParse(string value, out FogMode mode)
        {
            mode = FogMode.Off;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToUpperInvariant())
            {
                case "OFF":
                case "NONE":
                case "0":
                    mode = FogMode.Off;
                    return true;
                case "EXPONENTIAL":
                case "EXP":
                case "1":
                    mode = FogMode.Exponential;
                    return true;
                case "HEIGHT":
                case "HEIGHTBASED":
                case "HEIGHT_BASED":
                case "2":
                    mode = FogMode.Height;
                    return true;
                case "VOLUMETRIC":
                case "VOLUME":
                case "3":
                    mode = FogMode.Volumetric;
                    return true;
                default:
                    return false;
            }
        }

        public static string ToPayloadString(FogMode mode)
        {
            return mode switch
            {
                FogMode.Exponential => "Exponential",
                FogMode.Height => "Height",
                FogMode.Volumetric => "Volumetric",
                _ => "Off"
            };
        }
    }
}
