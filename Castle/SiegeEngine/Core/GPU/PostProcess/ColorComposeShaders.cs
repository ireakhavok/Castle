// Moved to SiegeEngine/Core/GPU/Shaders/ColorComposeShaders.cs
namespace SiegeEngine.Core.GPU.PostProcess
{
    public static class ColorComposeShaders
    {
        public const string FullscreenVertex = SiegeEngine.Core.GPU.Shaders.ColorComposeShaders.FullscreenVertex;
        public const string ExtractFragment = SiegeEngine.Core.GPU.Shaders.ColorComposeShaders.ExtractFragment;
        public const string DownsampleFragment = SiegeEngine.Core.GPU.Shaders.ColorComposeShaders.DownsampleFragment;
        public const string UpsampleFragment = SiegeEngine.Core.GPU.Shaders.ColorComposeShaders.UpsampleFragment;
        public const string ComposeFragment = SiegeEngine.Core.GPU.Shaders.ColorComposeShaders.ComposeFragment;
        public const string LumaFragment = SiegeEngine.Core.GPU.Shaders.ColorComposeShaders.LumaFragment;
        public const string LumaDownFragment = SiegeEngine.Core.GPU.Shaders.ColorComposeShaders.LumaDownFragment;
        public const string AdaptFragment = SiegeEngine.Core.GPU.Shaders.ColorComposeShaders.AdaptFragment;
    }
}
