using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
namespace SiegeEngine.Rendering.Definitions
{
    public class TextStyle
    {
        private float _fontSize;
        private Color _color;
        [JsonPropertyName("fontSize")]
        public float FontSize
        {
            get => _fontSize != 0 ? _fontSize : 8.0f;
            set => _fontSize = value;
        }
        [JsonPropertyName("color")]
        public Color Color
        {
            get => _color ??= new Color { R = 1.0f, G = 1.0f, B = 1.0f, A = 1.0f };
            set => _color = value;
        }
    }
}