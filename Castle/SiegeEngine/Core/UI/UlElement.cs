// Folder: SiegeEngine.UI
// File: UlElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System.Numerics;

namespace SiegeEngine.Core.UI
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