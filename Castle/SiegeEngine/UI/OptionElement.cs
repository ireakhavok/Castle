// Folder: SiegeEngine.UI
// File: OptionElement.cs
using System;
using System.Numerics;
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
namespace SiegeEngine.UI
{
    public class OptionElement : HtmlElement
    {
        public OptionElement()
        {
            Tag = "option";
        }
        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            var select = Parent as SelectElement;
            if (select != null && !select.IsOpen)
            {
                return false; // Let the parent select handle the click to open
            }
            return base.HandleClick(mousePos, viewportWidth, viewportHeight);
        }
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            var select = Parent as SelectElement;
            if (select != null && !select.IsOpen)
            {
                // Skip drawing background for displayed option in closed select
                foreach (var child in Children)
                {
                    child.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
                }
                return;
            }
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }
    }
}