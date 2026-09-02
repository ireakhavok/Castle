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
}