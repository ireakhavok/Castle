// Folder: SiegeEngine.Definitions
// File: AnimationComponent.cs
namespace SiegeEngine.Core.Definitions
{
    public class AnimationComponent : IComponent, IComponentData
    {
        public string CurrentAnimation { get; set; }
        public float Time { get; set; }
        public bool Playing { get; set; }

        // NEW: IComponentData support for round-tripping
        public object ToSerializableData()
        {
            return new AnimationComponentData
            {
                CurrentAnimation = CurrentAnimation,
                Time = Time,
                Playing = Playing
            };
        }

        public void FromSerializableData(object data)
        {
            if (data is AnimationComponentData a)
            {
                CurrentAnimation = a.CurrentAnimation;
                Time = a.Time;
                Playing = a.Playing;
            }
        }

        private class AnimationComponentData
        {
            public string CurrentAnimation { get; set; }
            public float Time { get; set; }
            public bool Playing { get; set; }
        }
    }
}