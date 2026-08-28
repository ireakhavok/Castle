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

        [JsonPropertyName("customSceneClass")]
        public string CustomSceneClass { get; set; }

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
        public string LiveStateGuid { get; set; }
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
        public Vector3 AmbientColor { get; set; } = new Vector3(0.45f, 0.45f, 0.48f);

        [JsonPropertyName("fogDensity")]
        public float FogDensity { get; set; } = 0.003f;

        [JsonPropertyName("antiAliasing")]
        public string AntiAliasing { get; set; } = "SMAA";

        [JsonPropertyName("fogMode")]
        public string FogMode { get; set; } = "Off";

        [JsonPropertyName("fogQuality")]
        public string FogQuality { get; set; } = "Off";

        [JsonPropertyName("fogColor")]
        public Vector3 FogColor { get; set; } = new Vector3(0.62f, 0.70f, 0.82f);

        [JsonPropertyName("fogHeight")]
        public float FogHeight { get; set; } = 8f;

        [JsonPropertyName("fogHeightFalloff")]
        public float FogHeightFalloff { get; set; } = 0.08f;

        [JsonPropertyName("fogStart")]
        public float FogStart { get; set; } = 40f;

        [JsonPropertyName("volumetricIntensity")]
        public float VolumetricIntensity { get; set; } = 0.25f;

        [JsonPropertyName("shadowQuality")]
        public string ShadowQuality { get; set; } = "Medium";

        [JsonPropertyName("shadowDistance")]
        public float ShadowDistance { get; set; } = 400f;

        [JsonPropertyName("sunEnabled")]
        public bool SunEnabled { get; set; } = false;

        [JsonPropertyName("sunDirection")]
        public Vector3 SunDirection { get; set; } = new Vector3(-0.85f, 0.10f, -0.52f);

        [JsonPropertyName("sunColor")]
        public Vector3 SunColor { get; set; } = new Vector3(1f, 1f, 1f);

        [JsonPropertyName("sunIntensity")]
        public float SunIntensity { get; set; } = 1f;

        [JsonPropertyName("sunCastShadows")]
        public bool SunCastShadows { get; set; } = true;
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

        [JsonPropertyName("verticalOffset")]
        public float VerticalOffset { get; set; } = 0f;

        [JsonPropertyName("orientation")]
        public Quaternion Orientation { get; set; } = Quaternion.Identity;
    }
}
