// Folder: Keystone
// File: ProjectData.cs
using SiegeEngine.Core.Definitions;
using System.Collections.Generic;
using System.Text.Json;

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

        // PanelStates dictionary marries per-panel UI runtime state with live backend content
        // (terrain heightmap paths, selected entities, brush settings, tree expansions, etc.).
        // Keyed by IDataAwarePanel.DataKey. Stored in project.json automatically on Save.
        // Memory-first until explicit FlushAllToDisk. Existing projects ignore the field gracefully.
        public Dictionary<string, JsonElement> PanelStates { get; set; } = new Dictionary<string, JsonElement>();

        /// <summary>
        /// Centralized, consistent JsonSerializerOptions used for ALL project.json read/write operations.
        /// Guarantees Vector3/Quaternion deserialization works (PascalCase inner fields + camelCase outer keys via JsonPropertyName).
        /// PropertyNameCaseInsensitive = true is required for robust System.Numerics struct binding.
        /// </summary>
        public static readonly JsonSerializerOptions ProjectJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };
    }
}