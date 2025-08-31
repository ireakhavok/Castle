using System.Numerics;

namespace SiegeEngine.Rendering.Definitions
{
    public class Label
    {
        public string Text { get; }
        public Vector2 Position { get; }
        public TextStyle TextStyle { get; }

        public Label(LabelDefinition def)
        {
            Text = def.Text;
            Position = def.GetPositionVector();
            TextStyle = def.TextStyle ?? new TextStyle { FontSize = 8.0f, Color = new Color { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f } };
        }

        // Labels are static and don't need update logic
        public void Update(Vector2 adjustedPos, Vector2 mousePos) { }
    }
}