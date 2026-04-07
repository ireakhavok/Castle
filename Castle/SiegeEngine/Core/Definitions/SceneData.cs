// Folder: SiegeEngine.Core.Definitions
// File: SceneData.cs
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

namespace SiegeEngine.Core.Definitions
{
    public class SceneData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sceneType")]
        public string SceneType { get; set; } = "Gameplay"; // Gameplay, Menu, TerrainTest, Cutscene, etc. — fully user-defined

        // Terrain with explicit multi-texture references (separate from any heightmap object)
        [JsonPropertyName("terrain")]
        public TerrainData Terrain { get; set; } = new TerrainData();

        // All placed content — 3D models, 3D sprites/billboards, lights, sound emitters, etc.
        [JsonPropertyName("entities")]
        public List<EntityData> Entities { get; set; } = new List<EntityData>();

        // Environment settings (lighting, weather, audio occlusion test bed)
        [JsonPropertyName("environment")]
        public EnvironmentSettings Environment { get; set; } = new EnvironmentSettings();

        // Extensible storage for any future system (animation blends, custom layers, etc.)
        [JsonPropertyName("customData")]
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }

    public class TerrainData
    {
        [JsonPropertyName("heightmapPath")]
        public string HeightmapPath { get; set; }

        [JsonPropertyName("colorTexturePath")]
        public string ColorTexturePath { get; set; }

        [JsonPropertyName("normalTexturePath")]
        public string NormalTexturePath { get; set; }

        [JsonPropertyName("worldScaleX")]
        public float WorldScaleX { get; set; } = 1f;

        [JsonPropertyName("worldScaleZ")]
        public float WorldScaleZ { get; set; } = 1f;

        [JsonPropertyName("verticalExaggeration")]
        public float VerticalExaggeration { get; set; } = 1f;
    }

    public class EnvironmentSettings
    {
        [JsonPropertyName("timeOfDay")]
        public float TimeOfDay { get; set; } = 12f;

        [JsonPropertyName("weather")]
        public string Weather { get; set; } = "Clear";

        [JsonPropertyName("ambientColor")]
        public Vector3 AmbientColor { get; set; } = new Vector3(0.8f, 0.8f, 0.95f);

        [JsonPropertyName("fogDensity")]
        public float FogDensity { get; set; } = 0.01f;
    }
}