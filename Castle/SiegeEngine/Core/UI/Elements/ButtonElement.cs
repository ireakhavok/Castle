// Folder: SiegeEngine.UI
// File: ButtonElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Numerics;
using System.Reflection;
namespace SiegeEngine.Core.UI.Elements
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
                textChild.ComputedContentWidth = ComputedContentWidth;
                textChild.ComputedWidth = ComputedContentWidth;
                textChild.ComputedContentHeight = textSize.Y;
                textChild.ComputedHeight = textSize.Y;
                textChild.ComputedContentX = ComputedContentX;
                textChild.ComputedPosition = new Vector2(textChild.ComputedContentX, ComputedContentY + (ComputedContentHeight - textSize.Y) / 2);
                textChild.ComputedContentY = textChild.ComputedPosition.Y;
            }
        }
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
        }
        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            if (base.HandleClick(mousePos, viewportWidth, viewportHeight))
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