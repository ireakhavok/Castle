// Folder: SiegeEngine/Core/Definitions
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
        public string SceneType { get; set; } = "Gameplay";

        [JsonPropertyName("terrain")]
        public TerrainData Terrain { get; set; } = new TerrainData();

        [JsonPropertyName("entities")]
        public List<EntityData> Entities { get; set; } = new List<EntityData>();

        [JsonPropertyName("environment")]
        public EnvironmentSettings Environment { get; set; } = new EnvironmentSettings();

        [JsonPropertyName("skybox")]
        public SkyboxData Skybox { get; set; } = null;

        [JsonPropertyName("customData")]
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();

        [JsonPropertyName("settings")]
        public SceneSettings Settings { get; set; }

        [JsonIgnore]
        public string LiveStateGuid { get; set; } // used internally by ProjectStateManager
    }

    public class TerrainData
    {
        [JsonPropertyName("heightmapPath")]
        public string HeightmapPath { get; set; }

        [JsonPropertyName("colorTexturePath")]
        public string ColorTexturePath { get; set; }

        [JsonPropertyName("normalTexturePath")]
        public string NormalTexturePath { get; set; }

        [JsonPropertyName("splatMapPath")]
        public string SplatMapPath { get; set; }

        [JsonPropertyName("materials")]
        public List<TerrainMaterial> Materials { get; set; } = new List<TerrainMaterial>();

        [JsonPropertyName("worldScaleX")]
        public float WorldScaleX { get; set; } = 1f;

        [JsonPropertyName("worldScaleZ")]
        public float WorldScaleZ { get; set; } = 1f;

        [JsonPropertyName("verticalExaggeration")]
        public float VerticalExaggeration { get; set; } = 1f;

        // Transfer-only: populated only for pure in-memory Play when heightmap is dirty / never-saved.
        // Explicit Save always clears these so project.json remains path-only.
        [JsonPropertyName("embeddedHeightmapWidth")]
        public int EmbeddedHeightmapWidth { get; set; }

        [JsonPropertyName("embeddedHeightmapHeight")]
        public int EmbeddedHeightmapHeight { get; set; }

        [JsonPropertyName("embeddedHeightmapData")]
        public float[] EmbeddedHeightmapData { get; set; }
    }

    public class TerrainMaterial
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Unnamed Material";

        [JsonPropertyName("albedoPath")]
        public string AlbedoPath { get; set; }

        [JsonPropertyName("normalPath")]
        public string NormalPath { get; set; }

        [JsonPropertyName("roughness")]
        public float Roughness { get; set; } = 0.8f;
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

    public class SkyboxData
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "Cubemap";

        [JsonPropertyName("cubemapPath")]
        public string CubemapPath { get; set; } = "";

        [JsonPropertyName("faces")]
        public List<string> Faces { get; set; } = new List<string>();

        [JsonPropertyName("rotationSpeed")]
        public float RotationSpeed { get; set; } = 0.05f;

        [JsonPropertyName("intensity")]
        public float Intensity { get; set; } = 1.0f;
    }
}