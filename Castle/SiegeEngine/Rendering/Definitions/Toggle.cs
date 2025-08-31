using System;
using System.Numerics;

namespace SiegeEngine.Rendering.Definitions
{
    public class Toggle
    {
        public string Name { get; }
        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public TextStyle TextStyle { get; }
        public ButtonStyle ButtonStyle { get; }
        public bool State { get; private set; }
        public Action<bool> OnToggle { get; }
        public bool IsHovered { get; private set; }

        public Toggle(ToggleDefinition def, Action<bool> onToggle)
        {
            Name = def.Name;
            Position = def.GetPositionVector();
            Size = def.GetSizeVector();
            TextStyle = def.TextStyle ?? new TextStyle { FontSize = 8.0f, Color = new Color { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f } };
            ButtonStyle = def.ButtonStyle;
            State = def.State;
            OnToggle = onToggle;
        }

        public void Update(Vector2 adjustedPos, Vector2 mousePos)
        {
            IsHovered = mousePos.X >= adjustedPos.X && mousePos.X <= adjustedPos.X + Size.X &&
                        mousePos.Y >= adjustedPos.Y && mousePos.Y <= adjustedPos.Y + Size.Y;
        }

        public void ToggleState()
        {
            State = !State;
            OnToggle?.Invoke(State);
            Console.WriteLine($"Toggle: Toggled {Name} to state: {State}");
        }
    }
}