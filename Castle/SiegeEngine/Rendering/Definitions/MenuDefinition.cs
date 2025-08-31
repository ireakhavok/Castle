// SiegeEngine/Rendering/Definitions/MenuDefinition.cs
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

namespace SiegeEngine.Rendering.Definitions
{
    public class MenuDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("background")]
        public string Background { get; set; }
        [JsonPropertyName("positioningMode")]
        public string PositioningMode { get; set; }
        [JsonPropertyName("tabs")]
        public List<TabDefinition> Tabs { get; set; }
        [JsonPropertyName("buttons")]
        public List<ButtonDefinition> Buttons { get; set; }
        [JsonPropertyName("elements")]
        public List<Dictionary<string, object>> Elements { get; set; }
    }

    public class TabDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("iconIndex")]
        public int IconIndex { get; set; }
        [JsonPropertyName("action")]
        public string Action { get; set; }
        [JsonPropertyName("buttons")]
        public List<ButtonDefinition> Buttons { get; set; }
        [JsonPropertyName("elements")]
        public List<Dictionary<string, object>> Elements { get; set; }
    }

    public class MenuConfig
    {
        [JsonPropertyName("menus")]
        public List<MenuDefinition> Menus { get; set; }
    }

    public class Position
    {
        [JsonPropertyName("x")]
        public float X { get; set; }
        [JsonPropertyName("y")]
        public float Y { get; set; }
    }

    public class Size
    {
        [JsonPropertyName("width")]
        public float Width { get; set; }
        [JsonPropertyName("height")]
        public float Height { get; set; }
    }

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
            get => _color ??= new Color { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f };
            set => _color = value;
        }
    }

    public class ButtonStyle
    {
        private Color _backgroundColor;
        private Color _hoverColor;
        private Color _borderColor;
        [JsonPropertyName("backgroundColor")]
        public Color BackgroundColor
        {
            get => _backgroundColor ??= new Color { R = 1.0f, G = 0.0f, B = 0.0f, A = 0.8f };
            set => _backgroundColor = value;
        }
        [JsonPropertyName("hoverColor")]
        public Color HoverColor
        {
            get => _hoverColor ??= new Color { R = 1.0f, G = 1.0f, B = 1.0f, A = 0.8f };
            set => _hoverColor = value;
        }
        [JsonPropertyName("borderColor")]
        public Color BorderColor
        {
            get => _borderColor ??= new Color { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f };
            set => _borderColor = value;
        }
    }

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

    public class ToggleDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        private string _name;
        [JsonPropertyName("name")]
        public string Name
        {
            get => _name ??= "Unnamed Toggle";
            set => _name = value;
        }
        [JsonPropertyName("position")]
        public Position Position { get; set; }
        [JsonPropertyName("size")]
        public Size Size { get; set; }
        [JsonPropertyName("iconIndex")]
        public int IconIndex { get; set; }
        [JsonPropertyName("state")]
        public bool State { get; set; }
        [JsonPropertyName("action")]
        public string Action { get; set; }
        [JsonPropertyName("textStyle")]
        public TextStyle TextStyle { get; set; }
        [JsonPropertyName("buttonStyle")]
        public ButtonStyle ButtonStyle { get; set; }
        public Vector2 GetPositionVector() => Position != null ? new Vector2(Position.X, Position.Y) : Vector2.Zero;
        public Vector2 GetSizeVector() => Size != null ? new Vector2(Size.Width, Size.Height) : Vector2.Zero;
    }

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