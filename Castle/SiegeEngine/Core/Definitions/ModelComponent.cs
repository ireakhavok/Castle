// Folder: SiegeEngine/Core/Definitions
// File: ModelComponent.cs
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Interfaces;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class ModelComponent : IComponent, IComponentData
    {
        public FBXModel Model { get; set; }
        public string Key { get; set; }

        public Matrix4x4[] BoneMatrices { get; set; }
        public Matrix3x3[] NormalBoneTransforms { get; set; }

        // Material (with full world-aligned TextureSlot support)
        // This is the per-entity override. Base material from FBX is in FBXModel.
        public Material Material { get; set; }

        // NEW: IComponentData support for round-tripping
        public object ToSerializableData()
        {
            return new ModelComponentData
            {
                Key = Key,
                MaterialData = Material != null ? new MaterialData
                {
                    Name = Material.Name,
                    TextureSlots = Material.TextureSlots
                } : null
            };
        }

        public void FromSerializableData(object data)
        {
            if (data is ModelComponentData m)
            {
                Key = m.Key;
                if (m.MaterialData != null)
                {
                    Material = new Material
                    {
                        Name = m.MaterialData.Name ?? "DefaultMaterial",
                        TextureSlots = m.MaterialData.TextureSlots ?? new List<TextureSlot>()
                    };
                }
            }
        }

        private class ModelComponentData
        {
            public string Key { get; set; }
            public MaterialData MaterialData { get; set; }
        }
    }
}