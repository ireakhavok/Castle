// Folder: SiegeEngine.Core.UI
// File: NavUlElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public class NavUlElement : HtmlElement
    {
        public NavUlElement()
        {
            Tag = "ul";
            Style.Display = "block";
            Style.ListStyleType = "none";
            Style.PaddingStr = "4px 0";
            Style.MarginStr = "0";
        }
    }
}