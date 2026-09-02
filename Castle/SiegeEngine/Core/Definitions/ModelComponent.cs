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

        public Material Material { get; set; }

        public bool CastShadows { get; set; } = true;
        public bool ReceiveShadows { get; set; } = true;

        public List<int> HiddenMeshIndices { get; set; } = new List<int>();

        public bool IsMeshHidden(int meshIndex)
        {
            return HiddenMeshIndices != null && HiddenMeshIndices.Contains(meshIndex);
        }

        public void SetMeshHidden(int meshIndex, bool hidden)
        {
            if (HiddenMeshIndices == null) HiddenMeshIndices = new List<int>();
            if (hidden)
            {
                if (!HiddenMeshIndices.Contains(meshIndex)) HiddenMeshIndices.Add(meshIndex);
            }
            else
            {
                HiddenMeshIndices.Remove(meshIndex);
            }
        }

        public object ToSerializableData()
        {
            return new ModelComponentData
            {
                Key = Key,
                CastShadows = CastShadows,
                ReceiveShadows = ReceiveShadows,
                HiddenMeshIndices = HiddenMeshIndices != null ? new List<int>(HiddenMeshIndices) : new List<int>(),
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
                CastShadows = m.CastShadows;
                ReceiveShadows = m.ReceiveShadows;
                HiddenMeshIndices = m.HiddenMeshIndices != null ? new List<int>(m.HiddenMeshIndices) : new List<int>();
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
            public bool CastShadows { get; set; } = true;
            public bool ReceiveShadows { get; set; } = true;
            public List<int> HiddenMeshIndices { get; set; } = new List<int>();
            public MaterialData MaterialData { get; set; }
        }
    }
}
