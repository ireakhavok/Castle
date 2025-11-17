// Folder: SiegeEngine.UI
// File: OptionElement.cs
using System;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class OptionElement : HtmlElement
    {
        public OptionElement()
        {
            Tag = "option";
        }

        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            var select = Parent as SelectElement;
            if (select != null && !select.IsOpen)
            {
                return false; // Let the parent select handle the click to open
            }
            return base.HandleClick(mousePos, viewportWidth, viewportHeight);
        }
    }
}