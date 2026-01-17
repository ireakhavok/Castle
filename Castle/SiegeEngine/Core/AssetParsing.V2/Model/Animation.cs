// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Animation.cs
using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Animation
    {
        public string Name { get; set; }
        public List<Keyframe> Keyframes { get; set; } = new List<Keyframe>();
    }
}