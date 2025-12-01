// Folder: SiegeEngine.UI
// File: ThElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class ThElement : HtmlElement
    {
        public ThElement()
        {
            Tag = "th";
            Style.Display = "table-cell";
            Style.FontWeight = "bold";
            Style.TextAlign = "center";
        }
    }
}