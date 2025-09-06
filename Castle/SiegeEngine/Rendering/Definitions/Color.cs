using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SiegeEngine.Rendering.Definitions
{
    public class Color
    {
        [JsonPropertyName("r")]
        public float R { get; set; }
        [JsonPropertyName("g")]
        public float G { get; set; }
        [JsonPropertyName("b")]
        public float B { get; set; }
        [JsonPropertyName("a")]
        public float A { get; set; }
        public Vector4 ToVector4() => new Vector4(R, G, B, A);
    }
}
