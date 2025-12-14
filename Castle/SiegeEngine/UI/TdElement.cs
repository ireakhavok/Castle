// Folder: SiegeEngine.UI
// File: TdElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class TdElement : HtmlElement
    {
        public TdElement()
        {
            Tag = "td";
            Style.Display = "table-cell";
        }
    }
}