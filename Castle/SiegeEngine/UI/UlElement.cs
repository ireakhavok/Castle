// Folder: SiegeEngine.UI
// File: UlElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class UlElement : HtmlElement
    {
        public UlElement()
        {
            Tag = "ul";
            Style.Display = "block";
            Style.ListStyleType = "disc";
            Style.PaddingLeftStr = "40px";
        }
    }
}