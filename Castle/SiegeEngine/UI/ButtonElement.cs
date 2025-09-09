// SiegeEngine.UI/ButtonElement.cs
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
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight);
            var textChild = Children.Find(c => c is TextElement) as TextElement;
            if (textChild != null)
            {
                float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
                float approxTextWidth = textChild.Content.Length * fs * 0.6f;
                float textX = ComputedPosition.X + (ComputedWidth - approxTextWidth) / 2;
                float textY = ComputedPosition.Y + (ComputedHeight - fs) / 2;
                textRenderer.RenderText(textChild.Content, textX, textY, (int)viewportWidth, (int)viewportHeight, fs, Style.TextColor);
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