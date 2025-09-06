// SiegeEngine/Rendering/Definitions/MenuDefinition.cs
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

namespace SiegeEngine.Rendering.Definitions
{
    public class MenuDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("background")]
        public string Background { get; set; }
        [JsonPropertyName("positioningMode")]
        public string PositioningMode { get; set; }
        [JsonPropertyName("tabs")]
        public List<TabDefinition> Tabs { get; set; }
        [JsonPropertyName("buttons")]
        public List<ButtonDefinition> Buttons { get; set; }
        [JsonPropertyName("elements")]
        public List<Dictionary<string, object>> Elements { get; set; }
    }



    public class MenuConfig
    {
        [JsonPropertyName("menus")]
        public List<MenuDefinition> Menus { get; set; }
    }



}