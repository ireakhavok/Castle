// Folder: SiegeEngine/Core/AssetParsing.V2/Model
// File: Material.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public enum TextureMappingMode
    {
        UV,
        WorldPlanar,
        Triplanar
    }

    public struct TextureSlot
    {
        [JsonPropertyName("slotName")]
        public string SlotName { get; set; }

        [JsonPropertyName("texturePath")]
        public string TexturePath { get; set; }

        [JsonPropertyName("mappingMode")]
        public TextureMappingMode MappingMode { get; set; }

        [JsonPropertyName("tiling")]
        public Vector2 Tiling { get; set; }

        [JsonPropertyName("offset")]
        public Vector2 Offset { get; set; }

        [JsonPropertyName("rotation")]
        public float Rotation { get; set; }

        [JsonPropertyName("blendSharpness")]
        public float BlendSharpness { get; set; }

        public TextureSlot(string slotName, string texturePath)
        {
            SlotName = slotName;
            TexturePath = texturePath;
            MappingMode = TextureMappingMode.UV;
            Tiling = new Vector2(1f, 1f);
            Offset = new Vector2(0f, 0f);
            Rotation = 0f;
            BlendSharpness = 0.1f;
        }
    }

    public class Material
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        [JsonPropertyName("textures")]
        public Dictionary<string, TextureInfo> Textures { get; set; } = new Dictionary<string, TextureInfo>();

        // World-aligned texture support - fully serialized and reflection-visible
        [JsonPropertyName("textureSlots")]
        public List<TextureSlot> TextureSlots { get; set; } = new List<TextureSlot>();
    }
}