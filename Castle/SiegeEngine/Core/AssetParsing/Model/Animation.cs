// Folder: SiegeEngine.Core.AssetParsing.V2.Model
// File: Animation.cs
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class Animation
    {
        public string Name { get; set; }
        public List<Keyframe> Keyframes { get; set; } = new List<Keyframe>();
        public float Duration { get; set; } = 0f;
    }
}