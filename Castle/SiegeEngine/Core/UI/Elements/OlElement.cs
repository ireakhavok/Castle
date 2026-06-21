// Folder: SiegeEngine.UI
// File: OlElement.cs
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
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