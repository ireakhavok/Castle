// Folder: SiegeEngine.Core.UI
// File: UlElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public class UlElement : HtmlElement
    {
        public UlElement()
        {
            Tag = "ul";
            Style.Display = "flex";
            Style.FlexDirection = "row";
            Style.AlignItems = "center";
            Style.ListStyleType = "none";
            Style.PaddingStr = "0";
            Style.MarginStr = "0";
            Style.HeightStr = "28px";
        }
    }
}