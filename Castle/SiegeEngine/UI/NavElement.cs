// Folder: SiegeEngine.UI
// File: NavElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class NavElement : HtmlElement
    {
        public NavElement()
        {
            Tag = "nav";
            Style.Display = "block";
        }
    }
}