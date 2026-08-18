// Folder: SiegeEngine.Core.UI
// File: NavUlElement.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU;
using System.Numerics;
namespace SiegeEngine.Core.UI.Elements
{
    public class NavUlElement : HtmlElement
    {
        public NavUlElement()
        {
            Tag = "ul";
            Style.Display = "block";
            Style.ListStyleType = "none";
            Style.PaddingStr = "4px 0";
            Style.MarginStr = "0";
        }
    }
}