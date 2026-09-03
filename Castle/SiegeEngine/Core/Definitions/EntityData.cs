// Folder: SiegeEngine/Core/Definitions
// File: EntityData.cs
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SiegeEngine.Core.AssetParsing.Model;

namespace SiegeEngine.Core.Definitions
{
    public class EntityData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("position")]
        public Vector3 Position { get; set; }

        [JsonPropertyName("rotation")]
        public Quaternion Rotation { get; set; }

        [JsonPropertyName("scale")]
        public Vector3 Scale { get; set; }

        [JsonPropertyName("assetPackKey")]
        public string AssetPackKey { get; set; }

        [JsonPropertyName("textureId")]
        public int TextureId { get; set; }

        [JsonPropertyName("height")]
        public float Height { get; set; }

        [JsonPropertyName("material")]
        public MaterialData MaterialData { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("hiddenMeshIndices")]
        public List<int> HiddenMeshIndices { get; set; }

        [JsonPropertyName("materialOptions")]
        public List<MeshMaterialOption> MaterialOptions { get; set; }

        // NEW: extensible component data list (supports mods, future components, unknown types are gracefully skipped)
        [JsonPropertyName("components")]
        public List<ComponentEntry> Components { get; set; } = new List<ComponentEntry>();

        public class ComponentEntry
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("data")]
            public object Data { get; set; }
        }

        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            IncludeFields = true
        };
    }

    public class MaterialData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("textureSlots")]
        public List<TextureSlot> TextureSlots { get; set; } = new List<TextureSlot>();
    }

    public class MaterialField
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }
    }

    public class MeshMaterialOption
    {
        [JsonPropertyName("meshIndex")]
        public int MeshIndex { get; set; }

        [JsonPropertyName("materialIndex")]
        public int MaterialIndex { get; set; }

        [JsonPropertyName("opacityPath")]
        public string OpacityPath { get; set; }

        [JsonPropertyName("fields")]
        public List<MaterialField> Fields { get; set; } = new List<MaterialField>();

        public bool HasContent()
        {
            if (!string.IsNullOrWhiteSpace(OpacityPath)) return true;
            if (Fields == null) return false;
            for (int i = 0; i < Fields.Count; i++)
            {
                var f = Fields[i];
                if (f != null && !string.IsNullOrWhiteSpace(f.Path))
                    return true;
            }
            return false;
        }

        public static MeshMaterialOption Clone(MeshMaterialOption src)
        {
            if (src == null) return null;
            var copy = new MeshMaterialOption
            {
                MeshIndex = src.MeshIndex,
                MaterialIndex = src.MaterialIndex,
                OpacityPath = src.OpacityPath
            };
            if (src.Fields != null)
            {
                copy.Fields = new List<MaterialField>();
                for (int i = 0; i < src.Fields.Count; i++)
                {
                    var f = src.Fields[i];
                    if (f == null) continue;
                    copy.Fields.Add(new MaterialField { Name = f.Name, Path = f.Path });
                }
            }
            return copy;
        }

        public static List<MeshMaterialOption> CloneList(List<MeshMaterialOption> src)
        {
            if (src == null) return new List<MeshMaterialOption>();
            var list = new List<MeshMaterialOption>();
            for (int i = 0; i < src.Count; i++)
            {
                var c = Clone(src[i]);
                if (c != null) list.Add(c);
            }
            return list;
        }
    }
}
