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
            var textChild = Children.Find(c => c is TextElement) as TextElement;
            if (textChild != null)
            {
                float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
                Vector2 textSize = textRenderer.GetTextSize(textChild.Content, fs, Style.FontFamily ?? "Arial");
                textChild.ComputedContentWidth = this.ComputedContentWidth;
                textChild.ComputedWidth = this.ComputedContentWidth;
                textChild.ComputedContentHeight = textSize.Y;
                textChild.ComputedHeight = textSize.Y;
                textChild.ComputedContentX = this.ComputedContentX;
                textChild.ComputedPosition = new Vector2(textChild.ComputedContentX, this.ComputedContentY + (this.ComputedContentHeight - textSize.Y) / 2);
                textChild.ComputedContentY = textChild.ComputedPosition.Y;
            }
        }
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
            var textChild = Children.Find(c => c is TextElement) as TextElement;
            if (textChild != null)
            {
                float fs = Style.FontSize > 0 ? Style.FontSize : 16f;
                float textW = textRenderer.GetTextSize(textChild.Content, fs, Style.FontFamily ?? "Arial").X;
                float textH = textRenderer.GetTextSize(textChild.Content, fs, Style.FontFamily ?? "Arial").Y;
                float textX = ComputedContentX + (ComputedContentWidth - textW) / 2;
                float textY = ComputedContentY + (ComputedContentHeight - textH) / 2;
                textRenderer.RenderText(textChild.Content, textX, textY, viewportWidth, viewportHeight, fs, Style.TextColor, Style.FontFamily ?? "Arial", parentMatrix);
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