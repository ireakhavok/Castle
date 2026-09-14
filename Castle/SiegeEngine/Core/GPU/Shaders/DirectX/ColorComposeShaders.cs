// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: ColorComposeShaders.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class ColorComposeShaders
    {
        public const string FullscreenVertex = AntiAliasingShaders.FullscreenVertex;
        public const string ExtractFragment = AntiAliasingShaders.CopyFragment;
        public const string DownsampleFragment = AntiAliasingShaders.CopyFragment;
        public const string UpsampleFragment = AntiAliasingShaders.CopyFragment;
        public const string ComposeFragment = AntiAliasingShaders.CopyFragment;
        public const string LumaFragment = AntiAliasingShaders.CopyFragment;
        public const string LumaDownFragment = AntiAliasingShaders.CopyFragment;
        public const string AdaptFragment = AntiAliasingShaders.CopyFragment;
    }
}
