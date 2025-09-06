// SiegeEngine/Rendering/Definitions/Dropdown.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Rendering.Definitions;
namespace SiegeEngine.Rendering.Definitions
{
    public class Dropdown
    {
        private readonly DropdownDefinition _def;
        private readonly Action<int> _onSelect;
        public Dropdown(DropdownDefinition def, Action<int> onSelect)
        {
            _def = def;
            _onSelect = onSelect;
            SelectedIndex = _def.SelectedIndex;
        }
        public DropdownDefinition Def => _def;
        public string Name => _def.Name;
        public Vector2 Position => _def.GetPositionVector();
        public Vector2 Size => _def.GetSizeVector();
        public List<string> Options => _def.Options;
        public int SelectedIndex { get; private set; }
        public bool IsExpanded { get; private set; }
        public bool IsHovered { get; private set; }
        public TextStyle TextStyle => _def.TextStyle;
        public ButtonStyle ButtonStyle => _def.ButtonStyle;
        public void Update(Vector2 adjustedPos, Vector2 mousePos)
        {
            float xMax = adjustedPos.X + Size.X;
            float yMax = adjustedPos.Y + Size.Y;
            IsHovered = mousePos.X >= adjustedPos.X && mousePos.X <= xMax && mousePos.Y >= adjustedPos.Y && mousePos.Y <= yMax;
        }
        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }
        public int GetOptionIndexAt(Vector2 mousePos, Vector2 adjustedPos)
        {
            for (int i = 0; i < Options.Count; i++)
            {
                Vector2 optionPos = new Vector2(adjustedPos.X, adjustedPos.Y + Size.Y * (i + 1));
                float xMax = optionPos.X + Size.X;
                float yMax = optionPos.Y + Size.Y;
                if (mousePos.X >= optionPos.X && mousePos.X <= xMax && mousePos.Y >= optionPos.Y && mousePos.Y <= yMax)
                {
                    return i;
                }
            }
            return -1;
        }
        public void SelectOption(int index)
        {
            SelectedIndex = index;
            _onSelect?.Invoke(index);
            ToggleExpanded();
        }
        public void UpdateOptions(List<string> newOptions)
        {
            Options.Clear();
            Options.AddRange(newOptions);
        }
    }
}