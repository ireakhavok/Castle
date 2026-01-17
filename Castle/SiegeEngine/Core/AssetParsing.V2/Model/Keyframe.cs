// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Keyframe.cs
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Keyframe
    {
        public float Time { get; set; }
        public List<Matrix4x4> BoneTransforms { get; set; } = new List<Matrix4x4>();
    }
}