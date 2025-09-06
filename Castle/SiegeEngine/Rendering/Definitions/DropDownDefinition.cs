// SiegeEngine/Rendering/Definitions/DropdownDefinition.cs
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
            get => _position ??= new Position();
            set => _position = value;
        }
        private Size _size;
        [JsonPropertyName("size")]
        public Size Size
        {
            get => _size ??= new Size { Width = 250, Height = 30 };
            set => _size = value;
        }
        [JsonPropertyName("iconIndex")]
        public int IconIndex { get; set; }
        private List<string> _options;
        [JsonPropertyName("options")]
        public List<string> Options
        {
            get => _options ??= new List<string>();
            set => _options = value;
        }
        [JsonPropertyName("selectedIndex")]
        public int SelectedIndex { get; set; }
        [JsonPropertyName("action")]
        public string Action { get; set; }
        private TextStyle _textStyle;
        [JsonPropertyName("textStyle")]
        public TextStyle TextStyle
        {
            get => _textStyle ??= new TextStyle();
            set => _textStyle = value;
        }
        private ButtonStyle _buttonStyle;
        [JsonPropertyName("buttonStyle")]
        public ButtonStyle ButtonStyle
        {
            get => _buttonStyle ??= new ButtonStyle();
            set => _buttonStyle = value;
        }
        [JsonPropertyName("isOptionsBelow")]
        public bool IsOptionsBelow { get; set; } // True for below-aligned options, false for right-aligned
        public Vector2 GetPositionVector() => new Vector2(Position.X, Position.Y);
        public Vector2 GetSizeVector() => new Vector2(Size.Width, Size.Height);
    }
}