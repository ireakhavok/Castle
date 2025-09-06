using System;
using System.Numerics;
using SiegeEngine.Rendering.Definitions;
namespace SiegeEngine.Rendering.Definitions
{
    public class Toggle
    {
        private readonly ToggleDefinition _def;
        private readonly Action<bool> _onToggle;
        public Toggle(ToggleDefinition def, Action<bool> onToggle)
        {
            _def = def;
            _onToggle = onToggle;
            State = def.State;
        }
        public ToggleDefinition Def => _def;
        public string Name => _def.Name;
        public Vector2 Position => _def.GetPositionVector();
        public Vector2 Size => _def.GetSizeVector();
        public bool State { get; private set; }
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
        public void ToggleState()
        {
            State = !State;
            _onToggle?.Invoke(State);
        }
    }
}