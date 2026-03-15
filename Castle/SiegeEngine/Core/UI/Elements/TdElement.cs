// Folder: SiegeEngine.UI
// File: TdElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System.Numerics;

namespace SiegeEngine.Core.UI.Elements
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