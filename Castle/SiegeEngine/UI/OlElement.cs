// Folder: SiegeEngine.UI
// File: OlElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class OlElement : HtmlElement
    {
        public OlElement()
        {
            Tag = "ol";
            Style.Display = "block";
            Style.ListStyleType = "decimal";
            Style.PaddingLeftStr = "40px";
        }
    }
}