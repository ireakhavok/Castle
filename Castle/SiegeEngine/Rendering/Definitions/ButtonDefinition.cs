using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SiegeEngine.Rendering.Definitions
{
    public class ButtonDefinition
    {
        private string _text;
        [JsonPropertyName("text")]
        public string Text
        {
            get => _text ??= "Unnamed Button";
            set => _text = value;
        }
        [JsonPropertyName("position")]
        public Position Position { get; set; }
        [JsonPropertyName("size")]
        public Size Size { get; set; }
        [JsonPropertyName("iconIndex")]
        public int IconIndex { get; set; }
        [JsonPropertyName("action")]
        public string Action { get; set; }
        [JsonPropertyName("textStyle")]
        public TextStyle TextStyle { get; set; }
        [JsonPropertyName("buttonStyle")]
        public ButtonStyle ButtonStyle { get; set; }
        public Vector2 GetPositionVector() => Position != null ? new Vector2(Position.X, Position.Y) : Vector2.Zero;
        public Vector2 GetSizeVector() => Size != null ? new Vector2(Size.Width, Size.Height) : Vector2.Zero;
    }
}
