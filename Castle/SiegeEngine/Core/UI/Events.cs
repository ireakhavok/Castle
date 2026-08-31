// file: Events.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public partial class HtmlElement
    {
        public virtual bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none") return false;
            float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, ComputedHeight, ComputedFullTransform, viewportWidth, viewportHeight);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                float nx = ndc[k * 2];
                float ny = ndc[k * 2 + 1];
                minX = Math.Min(minX, nx);
                maxX = Math.Max(maxX, nx);
                minY = Math.Min(minY, ny);
                maxY = Math.Max(maxY, ny);
            }
            float mx = 2 * mousePos.X / viewportWidth - 1;
            float my = 1 - 2 * mousePos.Y / viewportHeight;
            if (mx < minX || mx > maxX || my < minY || my > maxY) return false;
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i].HandleClick(mousePos, viewportWidth, viewportHeight)) return true;
            }
            string classes = "";
            Attributes.TryGetValue("class", out classes);
            bool isClickable = classes.Contains("button") || classes.Contains("toggle") || Tag == "select" || Tag == "label" || Tag == "a" || Attributes.ContainsKey("data-hook") || Attributes.ContainsKey("onclick") || classes.Contains("select-option") || Tag == "option" || Attributes.ContainsKey("onchange") || Attributes.ContainsKey("onmouseenter") || Attributes.ContainsKey("onmouseleave") || Attributes.ContainsKey("onmouseover") || Attributes.ContainsKey("onmouseout") || Attributes.ContainsKey("onmousedown") || Attributes.ContainsKey("onmouseup") || Attributes.ContainsKey("onfocus") || Attributes.ContainsKey("onblur") || Tag.ToLower() == "input";
            return isClickable;
        }

        public virtual bool UpdateHover(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (Style.Display == "none") return false;
            float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, ComputedHeight, ComputedFullTransform, viewportWidth, viewportHeight);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                float nx = ndc[k * 2];
                float ny = ndc[k * 2 + 1];
                minX = Math.Min(minX, nx);
                maxX = Math.Max(maxX, nx);
                minY = Math.Min(minY, ny);
                maxY = Math.Max(maxY, ny);
            }
            float mx = 2 * mousePos.X / viewportWidth - 1;
            float my = 1 - 2 * mousePos.Y / viewportHeight;
            bool over = !(mx < minX || mx > maxX || my < minY || my > maxY);
            bool changed = false;
            if (over && !IsHover)
            {
                IsHover = true;
                changed = true;
            }
            else if (!over && IsHover)
            {
                IsHover = false;
                changed = true;
            }
            bool childHit = false;
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i].UpdateHover(mousePos, viewportWidth, viewportHeight))
                    childHit = true;
            }
            return over || childHit;
        }
    }
}