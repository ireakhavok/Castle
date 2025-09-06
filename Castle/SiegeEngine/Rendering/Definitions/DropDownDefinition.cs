using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SiegeEngine.Rendering.Definitions
{
    public class DropdownDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        private string _name;
        [JsonPropertyName("name")]
        public string Name
        {
            get => _name ??= "Unnamed Dropdown";
            set => _name = value;
        }
        private Position _position;
        [JsonPropertyName("position")]
        public Position Position
        {
            get => _position ??= new Position { X = 0, Y = 0 };
            set => _position = value;
        }
        [JsonPropertyName("size")]
        public Size Size { get; set; }
        [JsonPropertyName("iconIndex")]
        public int IconIndex { get; set; }
        [JsonPropertyName("options")]
        public List<string> Options { get; set; }
        [JsonPropertyName("selectedIndex")]
        public int SelectedIndex { get; set; }
        [JsonPropertyName("action")]
        public string Action { get; set; }
        [JsonPropertyName("textStyle")]
        public TextStyle TextStyle { get; set; }
        [JsonPropertyName("buttonStyle")]
        public ButtonStyle ButtonStyle { get; set; }
        [JsonPropertyName("isOptionsBelow")]
        public bool IsOptionsBelow { get; set; } // True for below-aligned options, false for right-aligned
        public Vector2 GetPositionVector() => Position != null ? new Vector2(Position.X, Position.Y) : Vector2.Zero;
        public Vector2 GetSizeVector() => Size != null ? new Vector2(Size.Width, Size.Height) : Vector2.Zero;
    }
}
