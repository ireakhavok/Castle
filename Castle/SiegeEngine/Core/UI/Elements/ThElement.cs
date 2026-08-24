// Folder: SiegeEngine.UI
// File: ThElement.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
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