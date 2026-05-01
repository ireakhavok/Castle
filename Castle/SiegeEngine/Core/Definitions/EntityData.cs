using System.Numerics;
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
    }
}