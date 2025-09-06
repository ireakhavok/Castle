using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
namespace SiegeEngine.Rendering.Definitions
{
    public class Size
    {
        [JsonPropertyName("width")]
        public float Width { get; set; }
        [JsonPropertyName("height")]
        public float Height { get; set; }
    }
}