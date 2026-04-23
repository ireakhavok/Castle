using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class BlendedAnimationComponent : IComponent
    {
        public AnimationBlendStack BlendStack { get; set; }
        public Vector3 CurrentBlendParams { get; set; } = Vector3.Zero;
        public float GlobalTime { get; set; }
        public bool Playing { get; set; } = true;
        public float MasterSpeed { get; set; } = 1.0f;
    }
}