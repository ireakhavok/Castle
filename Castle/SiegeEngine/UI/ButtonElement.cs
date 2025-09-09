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

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, Vector2 parentPosition, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight)
        {
            base.Render(renderContext, textRenderer, quadRenderer, parentPosition, parentWidth, parentHeight, viewportWidth, viewportHeight);

            // Render text centered
            var textChild = Children.Find(c => c is TextElement) as TextElement;
            if (textChild != null)
            {
                float approxTextWidth = textChild.Content.Length * Style.FontSize * 0.6f;
                float textX = parentPosition.X + (parentWidth - approxTextWidth) / 2;
                float textY = parentPosition.Y + (parentHeight - Style.FontSize) / 2;
                textRenderer.RenderText(textChild.Content, textX, textY, (int)parentWidth, (int)parentHeight, Style.FontSize, Style.TextColor);
            }
        }

        public override bool HandleClick(Vector2 mousePos)
        {
            float left = ParseSize(Style.LeftStr, 0, 0, 0);
            float top = ParseSize(Style.TopStr, 0, 0, 0);
            float w = ParseSize(Style.WidthStr, 0, 0, 0);
            float h = ParseSize(Style.HeightStr, 0, 0, 0);
            Vector2 pos = new Vector2(left, top); // Assume absolute for simplicity
            if (mousePos.X >= pos.X && mousePos.X <= pos.X + w &&
                mousePos.Y >= pos.Y && mousePos.Y <= pos.Y + h)
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