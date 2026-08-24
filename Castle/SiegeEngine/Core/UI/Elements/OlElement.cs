// Folder: SiegeEngine.UI
// File: OlElement.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU;
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