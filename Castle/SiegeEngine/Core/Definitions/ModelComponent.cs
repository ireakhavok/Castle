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

        public List<MeshMaterialOption> MaterialOptions { get; set; } = new List<MeshMaterialOption>();

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

        public MeshMaterialOption GetOrCreateMaterialOption(int meshIndex, int materialIndex)
        {
            if (MaterialOptions == null) MaterialOptions = new List<MeshMaterialOption>();
            for (int i = 0; i < MaterialOptions.Count; i++)
            {
                var o = MaterialOptions[i];
                if (o != null && o.MeshIndex == meshIndex && o.MaterialIndex == materialIndex)
                    return o;
            }
            var created = new MeshMaterialOption { MeshIndex = meshIndex, MaterialIndex = materialIndex };
            MaterialOptions.Add(created);
            return created;
        }

        public MeshMaterialOption FindMaterialOption(int meshIndex, int materialIndex)
        {
            if (MaterialOptions == null) return null;
            for (int i = 0; i < MaterialOptions.Count; i++)
            {
                var o = MaterialOptions[i];
                if (o != null && o.MeshIndex == meshIndex && o.MaterialIndex == materialIndex)
                    return o;
            }
            return null;
        }

        public void SetMaterialField(int meshIndex, int materialIndex, string fieldName, string path)
        {
            var opt = GetOrCreateMaterialOption(meshIndex, materialIndex);
            path = path ?? "";
            if (string.Equals(fieldName, "Opacity", StringComparison.OrdinalIgnoreCase))
            {
                opt.OpacityPath = path;
                return;
            }
            if (opt.Fields == null) opt.Fields = new List<MaterialField>();
            MaterialField field = null;
            for (int i = 0; i < opt.Fields.Count; i++)
            {
                if (opt.Fields[i] != null && string.Equals(opt.Fields[i].Name, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    field = opt.Fields[i];
                    break;
                }
            }
            if (field == null)
            {
                field = new MaterialField { Name = fieldName };
                opt.Fields.Add(field);
            }
            field.Path = path;
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
