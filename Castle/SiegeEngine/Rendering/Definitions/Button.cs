using System;
using System.Numerics;
using SiegeEngine.Rendering.Definitions;
namespace SiegeEngine.Rendering.Definitions
{
    public class Button
    {
        private readonly ButtonDefinition _def;
        private readonly Action _onClick;
        public Button(ButtonDefinition def, Action onClick)
        {
            _def = def;
            _onClick = onClick;
        }
        public ButtonDefinition Def => _def;
        public Vector2 Position => _def.GetPositionVector();
        public Vector2 Size => _def.GetSizeVector();
        public string Text => _def.Text;
        public int IconIndex => _def.IconIndex;
        public string Action => _def.Action;
        public TextStyle TextStyle => _def.TextStyle;
        public ButtonStyle ButtonStyle => _def.ButtonStyle;
        public bool IsHovered { get; private set; }
        public void Update(Vector2 adjustedPos, Vector2 mousePos)
        {
            float xMax = adjustedPos.X + Size.X;
            float yMax = adjustedPos.Y + Size.Y;
            IsHovered = mousePos.X >= adjustedPos.X && mousePos.X <= xMax && mousePos.Y >= adjustedPos.Y && mousePos.Y <= yMax;
        }
        public void TriggerClick()
        {
            _onClick?.Invoke();
        }
    }
}