// Folder: SiegeEngine.Core.AssetParsing.V2.Model
// File: Animation.cs
using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Animation
    {
        public string Name { get; set; }
        public List<Keyframe> Keyframes { get; set; } = new List<Keyframe>();
        public float Duration { get; set; } = 0f;
    }
}