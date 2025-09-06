// SiegeEngine/Rendering/Definitions/LabelDefinition.cs
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
        private Position _position;
        [JsonPropertyName("position")]
        public Position Position
        {
            get => _position ??= new Position();
            set => _position = value;
        }
        private TextStyle _textStyle;
        [JsonPropertyName("textStyle")]
        public TextStyle TextStyle
        {
            get => _textStyle ??= new TextStyle();
            set => _textStyle = value;
        }
        public Vector2 GetPositionVector() => new Vector2(Position.X, Position.Y);
    }
}