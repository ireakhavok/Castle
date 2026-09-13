// Folder: SiegeEngine/Core/GPU/PostProcess
// File: AntiAliasingShaders.cs
// Moved to SiegeEngine/Core/GPU/Shaders/AntiAliasingShaders.cs — this file is a namespace alias.
namespace SiegeEngine.Core.GPU.PostProcess
{
    public static class AntiAliasingShaders
    {
        public const string FullscreenVertex = SiegeEngine.Core.GPU.Shaders.AntiAliasingShaders.FullscreenVertex;
        public const string CopyFragment = SiegeEngine.Core.GPU.Shaders.AntiAliasingShaders.CopyFragment;
        public const string FxaaFragment = SiegeEngine.Core.GPU.Shaders.AntiAliasingShaders.FxaaFragment;
        public const string SmaaEdgeFragment = SiegeEngine.Core.GPU.Shaders.AntiAliasingShaders.SmaaEdgeFragment;
        public const string SmaaWeightFragment = SiegeEngine.Core.GPU.Shaders.AntiAliasingShaders.SmaaWeightFragment;
        public const string SmaaBlendFragment = SiegeEngine.Core.GPU.Shaders.AntiAliasingShaders.SmaaBlendFragment;
        public const string TaaFragment = SiegeEngine.Core.GPU.Shaders.AntiAliasingShaders.TaaFragment;
    }
}
