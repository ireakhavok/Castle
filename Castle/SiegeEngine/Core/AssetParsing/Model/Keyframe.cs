using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class Keyframe
    {
        public float Time { get; set; }
        public List<Matrix4x4> BoneTransforms { get; set; }
    }
}
