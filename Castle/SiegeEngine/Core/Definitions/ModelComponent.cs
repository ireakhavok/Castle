using SiegeEngine.Core.AssetParsing.Model;
using Silk.NET.Maths;
using System;
using System.Numerics;
namespace SiegeEngine.Core.Definitions
{
    public class ModelComponent : IComponent
    {
        public FBXModel Model { get; set; }
        public string Key { get; set; } // Added to store model identifier
        public Matrix3x3[] NormalBoneTransforms { get; set; }
    }
}