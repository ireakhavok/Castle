// Folder: SiegeEngine.UI
// File: TdElement.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU;
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