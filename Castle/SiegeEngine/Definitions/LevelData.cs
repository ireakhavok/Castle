using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

namespace SiegeEngine.Definitions
{
    public class LevelData
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }
        [JsonPropertyName("height")]
        public int Height { get; set; }
        [JsonPropertyName("entities")]
        public List<EntityData> Entities { get; set; } = new List<EntityData>();
    }

    public class EntityData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("position")]
        public Vector3 Position { get; set; }
        [JsonPropertyName("textureId")]
        public int TextureId { get; set; }
        [JsonPropertyName("height")]
        public float Height { get; set; }
    }
}