// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Bone.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Bone
    {
        public string Name { get; set; }
        public Matrix4x4 BindPose { get; set; } = Matrix4x4.Identity;
        public int ParentIndex { get; set; } = -1;
        public Matrix4x4 LocalRest { get; set; } = Matrix4x4.Identity;
        public Vector3 LclTranslation { get; set; } = Vector3.Zero;
        public Quaternion LclRotation { get; set; } = Quaternion.Identity;
        public Vector3 LclScaling { get; set; } = Vector3.One;
        public List<Bone> Children { get; set; } = new List<Bone>();
    }
}