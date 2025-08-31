using System.Collections.Generic;
using System;
using System.Numerics;

namespace SiegeEngine.Rendering.Definitions
{
    public class Dropdown
    {
        public string Name { get; }
        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public TextStyle TextStyle { get; }
        public ButtonStyle ButtonStyle { get; }
        public List<string> Options { get; private set; }
        public int SelectedIndex { get; private set; }
        public Action<int> OnSelect { get; }
        public bool IsHovered { get; private set; }
        public bool IsExpanded { get; private set; }
        private readonly bool _isOptionsBelow;

        public Dropdown(DropdownDefinition def, Action<int> onSelect)
        {
            Name = def.Name;
            Position = def.GetPositionVector();
            Size = def.GetSizeVector();
            TextStyle = def.TextStyle ?? new TextStyle { FontSize = 8.0f, Color = new Color { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f } };
            ButtonStyle = def.ButtonStyle;
            Options = def.Options ?? new List<string> { "None" };
            SelectedIndex = def.SelectedIndex >= 0 && def.SelectedIndex < Options.Count ? def.SelectedIndex : 0;
            OnSelect = onSelect;
            IsExpanded = false;
            _isOptionsBelow = def.IsOptionsBelow;
        }

        public void Update(Vector2 adjustedPos, Vector2 mousePos)
        {
            IsHovered = mousePos.X >= adjustedPos.X && mousePos.X <= adjustedPos.X + Size.X &&
                        mousePos.Y >= adjustedPos.Y && mousePos.Y <= adjustedPos.Y + Size.Y;
            Console.WriteLine($"Dropdown: Updating {Name} - AdjustedPos: ({adjustedPos.X}, {adjustedPos.Y}), Size: ({Size.X}, {Size.Y}), MousePos: ({mousePos.X}, {mousePos.Y}), IsHovered: {IsHovered}");
        }

        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
            Console.WriteLine($"Dropdown: Toggled {Name} expanded state to: {IsExpanded}");
        }

        public int GetOptionIndexAt(Vector2 mousePos, Vector2 adjustedPos)
        {
            if (!IsExpanded)
            {
                Console.WriteLine($"Dropdown: {Name} is not expanded, cannot select option.");
                return -1;
            }

            for (int i = 0; i < Options.Count; i++)
            {
                if (_isOptionsBelow)
                {
                    float optionX = adjustedPos.X;
                    float optionY = adjustedPos.Y + Size.Y + i * 30;
                    bool inBounds = mousePos.X >= optionX && mousePos.X <= optionX + Size.X &&
                                    mousePos.Y >= optionY && mousePos.Y <= optionY + 30;
                    Console.WriteLine($"Dropdown: Checking option {i} ({Options[i]}) at ({optionX}, {optionY}) to ({optionX + Size.X}, {optionY + 30}) - Mouse: ({mousePos.X}, {mousePos.Y}), InBounds: {inBounds}");
                    if (inBounds)
                    {
                        return i;
                    }
                }
                else
                {
                    float optionX = adjustedPos.X + Size.X + 5;
                    float optionY = adjustedPos.Y + Size.Y * i;
                    bool inBounds = mousePos.X >= optionX && mousePos.X <= optionX + Size.X &&
                                    mousePos.Y >= optionY && mousePos.Y <= optionY + Size.Y;
                    Console.WriteLine($"Dropdown: Checking option {i} ({Options[i]}) at ({optionX}, {optionY}) to ({optionX + Size.X}, {optionY + Size.Y}) - Mouse: ({mousePos.X}, {mousePos.Y}), InBounds: {inBounds}");
                    if (inBounds)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public void SelectOption(int index)
        {
            if (index >= 0 && index < Options.Count)
            {
                SelectedIndex = index;
                IsExpanded = false;
                OnSelect?.Invoke(index);
                Console.WriteLine($"Dropdown: Selected option {index}: {Options[index]} for {Name}");
            }
            else
            {
                Console.WriteLine($"Dropdown: Invalid option index {index} for {Name}, Options count: {Options.Count}");
            }
        }

        public void UpdateOptions(List<string> newOptions)
        {
            Options = newOptions ?? new List<string> { "None" };
            SelectedIndex = 0;
            Console.WriteLine($"Dropdown: Updated options for {Name}: {string.Join(", ", Options)}");
        }
    }
}