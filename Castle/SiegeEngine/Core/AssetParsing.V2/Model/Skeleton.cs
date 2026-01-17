// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Skeleton.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Skeleton
    {
        public List<Bone> Bones { get; set; } = new List<Bone>();
        // Methods like ComputeGlobalTransforms will be added iteratively
    }
}