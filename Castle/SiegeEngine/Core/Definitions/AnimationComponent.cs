// Folder: SiegeEngine.Definitions
// File: AnimationComponent.cs
namespace SiegeEngine.Core.Definitions
{
    public class AnimationComponent : IComponent
    {
        public string CurrentAnimation { get; set; }
        public float Time { get; set; }
        public bool Playing { get; set; }
    }
}