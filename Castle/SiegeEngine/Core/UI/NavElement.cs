// Folder: SiegeEngine.UI
// File: NavElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System.Numerics;

namespace SiegeEngine.Core.UI
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