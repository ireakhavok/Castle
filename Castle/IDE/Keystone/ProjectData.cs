// Folder: Keystone
// File: ProjectData.cs
using SiegeEngine.Core.Definitions;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Keystone
{
    public class ProjectData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Mode { get; set; }
        public bool AllowMods { get; set; }
        public Dictionary<string, SceneData> Scenes { get; set; } = new Dictionary<string, SceneData>();
        public string Version { get; set; } = "1.0";
        public string LastOpenedScene { get; set; } = string.Empty;
        public string CameraType { get; set; } = "Perspective";
        public string LastContext { get; set; } = "Scene Editor";

        public Dictionary<string, JsonElement> PanelStates { get; set; } = new Dictionary<string, JsonElement>();

        public List<string> CustomAssemblies { get; set; } = new List<string>();
        public bool ScriptsEnabled { get; set; } = true;
        public string ScriptsDir { get; set; } = "Scripts";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? UseFixedTimestep { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? StepRateHz { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? GravityZ { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? FrameCapHz { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? MouseSensitivityX { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? MouseSensitivityY { get; set; }

        public static readonly JsonSerializerOptions ProjectJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
