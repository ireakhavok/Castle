using System;
using System.Numerics;

namespace SiegeEngine.Rendering.Definitions
{
    public class Button
    {
        public string Text { get; }
        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public TextStyle TextStyle { get; }
        public Vector4 BackgroundColor { get; }
        public Vector4 HoverColor { get; }
        public Vector4 BorderColor { get; }
        public Action OnClick { get; }
        public bool IsHovered { get; private set; }

        public Button(ButtonDefinition def, Action onClick)
        {
            Text = def.Text ?? "Unnamed Button"; // Should never be null due to ButtonDefinition, but double-checking
            Position = def.GetPositionVector();
            Size = def.GetSizeVector();
            TextStyle = def.TextStyle ?? new TextStyle { FontSize = 8.0f, Color = new Color { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f } };
            BackgroundColor = def.ButtonStyle?.BackgroundColor?.ToVector4() ?? new Vector4(1.0f, 0.0f, 0.0f, 0.8f);
            HoverColor = def.ButtonStyle?.HoverColor?.ToVector4() ?? new Vector4(1.0f, 1.0f, 1.0f, 0.8f);
            BorderColor = def.ButtonStyle?.BorderColor?.ToVector4() ?? new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
            OnClick = onClick;
        }

        public void Update(Vector2 adjustedPos, Vector2 mousePos)
        {
            IsHovered = mousePos.X >= adjustedPos.X && mousePos.X <= adjustedPos.X + Size.X &&
                        mousePos.Y >= adjustedPos.Y && mousePos.Y <= adjustedPos.Y + Size.Y;
        }

        public void TriggerClick()
        {
            OnClick?.Invoke();
        }
    }
}