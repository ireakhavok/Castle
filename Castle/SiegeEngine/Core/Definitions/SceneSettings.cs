// Folder: SiegeEngine/Core/Definitions
// File: SceneSettings.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiegeEngine.Core.Definitions
{
    public class SceneSettings
    {
        [JsonPropertyName("avatarPackKey")]
        public string AvatarPackKey { get; set; }

        [JsonPropertyName("controllerTypeName")]
        public string ControllerTypeName { get; set; }

        [JsonPropertyName("preferredSpawnPointIds")]
        public List<int> PreferredSpawnPointIds { get; set; } = new List<int>();

        [JsonPropertyName("cameraMode")]
        public string CameraMode { get; set; }
    }
}