// Folder: SiegeEngine/Core/Definitions
// File: ModelComponent.cs
using SiegeEngine.Core.AssetParsing.Model;
using Silk.NET.Maths;
using System;
using System.Numerics;
using SiegeEngine.Core.Interfaces;

namespace SiegeEngine.Core.Definitions
{
    public class ModelComponent : IComponent
    {
        public FBXModel Model { get; set; }
        public string Key { get; set; }

        public Matrix3x3[] NormalBoneTransforms { get; set; }

        // Material (with full world-aligned TextureSlot support)
        // This is the per-entity override. Base material from FBX is in FBXModel.
        public Material Material { get; set; }
    }
}