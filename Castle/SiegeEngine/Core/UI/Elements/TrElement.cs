// Folder: SiegeEngine.UI
// File: TrElement.cs
using SiegeEngine.Core.Rendering.ContextManagement;
using System.Numerics;
using SiegeEngine.Core.Rendering.Renderers;

namespace SiegeEngine.Core.UI.Elements
{
    public class TrElement : HtmlElement
    {
        public TrElement()
        {
            Tag = "tr";
            Style.Display = "table-row";
            Style.FlexDirection = "row";
        }

        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            Style.Display = "flex";
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }
    }
}