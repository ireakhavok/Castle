using System;
using System.Numerics;
using SiegeEngine.Rendering.Definitions;
namespace SiegeEngine.Rendering.Definitions
{
    public class Label
    {
        private readonly LabelDefinition _def;
        public Label(LabelDefinition def)
        {
            _def = def;
        }
        public LabelDefinition Def => _def;
        public string Text => _def.Text;
        public Vector2 Position => _def.GetPositionVector();
        public TextStyle TextStyle => _def.TextStyle;
        public void Update(Vector2 adjustedPos, Vector2 mousePos)
        {
            // No interaction
        }
    }
}