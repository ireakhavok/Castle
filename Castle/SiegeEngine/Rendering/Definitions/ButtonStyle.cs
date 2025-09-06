using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
namespace SiegeEngine.Rendering.Definitions
{
    public class ButtonStyle
    {
        private Color _backgroundColor;
        private Color _hoverColor;
        private Color _borderColor;
        [JsonPropertyName("backgroundColor")]
        public Color BackgroundColor
        {
            get => _backgroundColor ??= new Color { R = 0.2f, G = 0.2f, B = 0.2f, A = 0.8f };
            set => _backgroundColor = value;
        }
        [JsonPropertyName("hoverColor")]
        public Color HoverColor
        {
            get => _hoverColor ??= new Color { R = 0.3f, G = 0.3f, B = 0.3f, A = 0.8f };
            set => _hoverColor = value;
        }
        [JsonPropertyName("borderColor")]
        public Color BorderColor
        {
            get => _borderColor ??= new Color { R = 0.5f, G = 0.5f, B = 0.5f, A = 1.0f };
            set => _borderColor = value;
        }
    }
}