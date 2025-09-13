// Folder: SiegeEngine.UI
// File: ButtonElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering;
using System;
using System.Numerics;
using System.Reflection;
namespace SiegeEngine.UI
{
    public class ButtonElement : HtmlElement
    {
        public Action OnClick { get; set; }
        public ButtonElement()
        {
            Tag = "button";
        }
        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight);
            var textChild = Children.Find(c => c is TextElement) as TextElement;
            if (textChild != null)
            {
                float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
                float approxTextWidth = textRenderer.GetTextSize(textChild.Content, fs, Style.FontFamily ?? "Arial").X;
                float textX = ComputedContentX;
                if (Style.TextAlign == "center")
                {
                    textX += (ComputedContentWidth - approxTextWidth) / 2;
                }
                else if (Style.TextAlign == "right")
                {
                    textX += ComputedContentWidth - approxTextWidth;
                }
                float textY = ComputedContentY + (ComputedContentHeight - fs) / 2;
                textRenderer.RenderText(textChild.Content, textX, textY, (int)viewportWidth, (int)viewportHeight, fs, Style.TextColor, Style.FontFamily ?? "Arial");
            }
        }
        public override bool HandleClick(Vector2 mousePos)
        {
            if (mousePos.X >= ComputedPosition.X && mousePos.X <= ComputedPosition.X + ComputedWidth &&
                mousePos.Y >= ComputedPosition.Y && mousePos.Y <= ComputedPosition.Y + ComputedHeight)
            {
                OnClick?.Invoke();
                return true;
            }
            return false;
        }
        public void AttachHook(string hookStr)
        {
            var parts = hookStr.Split('.');
            if (parts.Length != 3) return;
            string ns = parts[0];
            string cls = parts[1];
            string method = parts[2];
            Type type = Type.GetType(ns + "." + cls);
            if (type == null) return;
            MethodInfo mi = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public);
            if (mi == null) return;
            OnClick = () => mi.Invoke(null, null);
        }
    }
}