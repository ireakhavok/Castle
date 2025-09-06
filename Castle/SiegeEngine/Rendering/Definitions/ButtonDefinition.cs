// SiegeEngine/Rendering/Definitions/ButtonDefinition.cs
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
            get => _size ??= new Size { Width = 250, Height = 50 };
            set => _size = value;
        }
        [JsonPropertyName("iconIndex")]
        public int IconIndex { get; set; }
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
        public Vector2 GetPositionVector() => new Vector2(Position.X, Position.Y);
        public Vector2 GetSizeVector() => new Vector2(Size.Width, Size.Height);
    }
}