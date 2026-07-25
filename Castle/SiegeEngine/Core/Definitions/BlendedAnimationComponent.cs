// Folder: SiegeEngine.Core.Definitions
// File: BlendedAnimationComponent.cs
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Numerics;
namespace SiegeEngine.Core.Definitions
{
    public class BlendedAnimationComponent : IComponent, IComponentData
    {
        public AnimationPack Pack { get; set; }
        public Vector3 CurrentBlendParams { get; set; } = Vector3.Zero;
        public float GlobalTime { get; set; }
        public bool Playing { get; set; } = true;
        public float MasterSpeed { get; set; } = 1.0f;
        public bool IsStatic { get; set; } = false;

        // Long-lived stack owned by the component. Created once from Pack.CreateBlendStack().
        // LocalTime advances on this instance only; the pack itself remains immutable.
        [System.Text.Json.Serialization.JsonIgnore]
        public AnimationBlendStack RuntimeStack { get; set; }

        // NEW: IComponentData support for round-tripping
        public object ToSerializableData()
        {
            return new BlendedAnimationComponentData
            {
                CurrentBlendParams = CurrentBlendParams,
                GlobalTime = GlobalTime,
                Playing = Playing,
                MasterSpeed = MasterSpeed,
                IsStatic = IsStatic
            };
        }
        public void FromSerializableData(object data)
        {
            if (data is BlendedAnimationComponentData b)
            {
                CurrentBlendParams = b.CurrentBlendParams;
                GlobalTime = b.GlobalTime;
                Playing = b.Playing;
                MasterSpeed = b.MasterSpeed;
                IsStatic = b.IsStatic;
            }
        }
        private class BlendedAnimationComponentData
        {
            public Vector3 CurrentBlendParams { get; set; }
            public float GlobalTime { get; set; }
            public bool Playing { get; set; }
            public float MasterSpeed { get; set; }
            public bool IsStatic { get; set; }
        }
    }
}