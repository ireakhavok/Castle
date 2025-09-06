using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SiegeEngine.Rendering.Definitions
{
    public class LabelDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        private string _text;
        [JsonPropertyName("text")]
        public string Text
        {
            get => _text ??= "Unnamed Label";
            set => _text = value;
        }
        [JsonPropertyName("position")]
        public Position Position { get; set; }
        [JsonPropertyName("textStyle")]
        public TextStyle TextStyle { get; set; }
        public Vector2 GetPositionVector() => Position != null ? new Vector2(Position.X, Position.Y) : Vector2.Zero;
    }
}
