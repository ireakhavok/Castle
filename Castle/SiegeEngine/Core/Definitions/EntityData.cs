// Folder: SiegeEngine/Core/Definitions
// File: EntityData.cs
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        /// <summary>
        /// Shared, engine-neutral JsonSerializerOptions used for ALL project.json / SceneData / EntityData serialization.
        /// Guarantees correct deserialization of System.Numerics.Vector3 and Quaternion (PascalCase inner fields).
        /// PropertyNameCaseInsensitive = true is required for robust binding of the saved JSON format.
        /// Placed here in core so Level.cs and other engine code never reference Keystone.
        /// </summary>
        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            IncludeFields = true
        };
    }
}