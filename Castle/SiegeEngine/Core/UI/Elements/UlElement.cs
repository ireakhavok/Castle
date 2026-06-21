// Folder: SiegeEngine.Core.UI
// File: UlElement.cs
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
{
    public class UlElement : HtmlElement
    {
        public UlElement()
        {
            Tag = "ul";
            // Default to standard vertical block list (NavElement overrides its ul to flex row)
            Style.Display = "block";
            Style.ListStyleType = "disc";
            Style.PaddingStr = "0 0 0 40px";
            Style.MarginStr = "8px 0";
        }
    }
}