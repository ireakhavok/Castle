using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
namespace SiegeEngine.Rendering.Definitions
{
    public class TabDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("iconIndex")]
        public int IconIndex { get; set; }
        [JsonPropertyName("action")]
        public string Action { get; set; }
        [JsonPropertyName("buttons")]
        public List<ButtonDefinition> Buttons { get; set; }
        [JsonPropertyName("elements")]
        public List<Dictionary<string, object>> Elements { get; set; }
    }
}