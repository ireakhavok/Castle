// Folder: SiegeEngine/Core/Definitions
// File: AntiAliasingMode.cs
namespace SiegeEngine.Core.Definitions
{
    public enum AntiAliasingMode
    {
        Off = 0,
        FXAA = 1,
        SMAA = 2,
        TAA = 3
    }

    public static class AntiAliasingModeParser
    {
        public static bool TryParse(string value, out AntiAliasingMode mode)
        {
            mode = AntiAliasingMode.SMAA;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToUpperInvariant())
            {
                case "OFF":
                case "NONE":
                case "0":
                    mode = AntiAliasingMode.Off;
                    return true;
                case "FXAA":
                case "1":
                    mode = AntiAliasingMode.FXAA;
                    return true;
                case "SMAA":
                case "2":
                    mode = AntiAliasingMode.SMAA;
                    return true;
                case "TAA":
                case "3":
                    mode = AntiAliasingMode.TAA;
                    return true;
                default:
                    return false;
            }
        }

        public static string ToPayloadString(AntiAliasingMode mode)
        {
            return mode switch
            {
                AntiAliasingMode.Off => "Off",
                AntiAliasingMode.FXAA => "FXAA",
                AntiAliasingMode.SMAA => "SMAA",
                AntiAliasingMode.TAA => "TAA",
                _ => "SMAA"
            };
        }
    }
}