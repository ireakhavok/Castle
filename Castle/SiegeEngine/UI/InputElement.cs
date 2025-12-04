// Folder: SiegeEngine.UI
// File: InputElement.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.UI
{
    public class InputElement : HtmlElement
    {
        public string Type { get; set; }
        public string Value { get; set; } = "";
        private bool _cursorVisible = true;
        private float _cursorTimer = 0f;
        private const float CursorBlinkRate = 0.5f;
        public InputElement()
        {
            Tag = "input";
        }
        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            if (Type == "radio")
            {
                Style.Display = "none";
            }
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
            if (Type == "checkbox" || Type == "radio")
            {
                float fs = Style.FontSize;
                if (float.IsNaN(ComputedWidth)) ComputedWidth = fs * 1.5f;
                if (float.IsNaN(ComputedHeight)) ComputedHeight = fs;
            }
            else if (Type == "text")
            {
                float fs = Style.FontSize;
                if (float.IsNaN(ComputedHeight)) ComputedHeight = fs * 1.5f;
            }
        }
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
            if (Type == "text")
            {
                float fs = Style.FontSize;
                Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : new Vector4(0f, 0f, 0f, 1f);
                textRenderer.RenderText(Value, ComputedContentX, ComputedContentY, viewportWidth, viewportHeight, fs, color, Style.FontFamily ?? "Arial", parentMatrix);
                if (IsFocused && _cursorVisible)
                {
                    float textW = textRenderer.GetTextSize(Value, fs).X;
                    float cursorX = ComputedContentX + textW;
                    float cursorY = ComputedContentY;
                    float cursorH = fs;
                    float cursorW = 2f;
                    float[] cursorNdc = GetNdcQuad(cursorX, cursorY, cursorW, cursorH, parentMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(cursorNdc, color);
                }
            }
            else
            {
                string symbol = "";
                if (Type == "checkbox")
                {
                    symbol = Checked ? "✔" : "";
                }
                else if (Type == "radio")
                {
                    symbol = Checked ? "●" : "○";
                }
                if (!string.IsNullOrEmpty(symbol))
                {
                    float fs = Style.FontSize;
                    float symbolWidth = textRenderer.GetTextSize(symbol, fs).X;
                    float symbolHeight = textRenderer.GetTextSize(symbol, fs).Y;
                    float textX = ComputedContentX + (ComputedContentWidth - symbolWidth) / 2;
                    float textY = ComputedContentY + (ComputedContentHeight - symbolHeight) / 2;
                    Vector4 color = Style.TextColor != Vector4.Zero ? Style.TextColor : new Vector4(0f, 0f, 0f, 1f);
                    textRenderer.RenderText(symbol, textX, textY, viewportWidth, viewportHeight, fs, color, Style.FontFamily ?? "Arial", parentMatrix);
                }
            }
        }
        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            if (Type == "checkbox" || Type == "radio")
            {
                Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
                Vector4 borderW = ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
                float iw = fs + pad.W + pad.Y + borderW.W + borderW.Y;
                float ih = fs + pad.X + pad.Z + borderW.X + borderW.Z;
                return new Vector2(iw, ih);
            }
            if (Type == "text")
            {
                float textW = textRenderer.GetTextSize(Value, fs).X;
                float textH = textRenderer.GetTextSize("A", fs).Y;
                Vector4 pad = ParsePaddings(Style, 0, viewportWidth, viewportHeight);
                Vector4 borderW = ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
                float iw = textW + pad.W + pad.Y + borderW.W + borderW.Y;
                float ih = textH + pad.X + pad.Z + borderW.X + borderW.Z;
                return new Vector2(iw, ih);
            }
            return base.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
        }
        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            return base.HandleClick(mousePos, viewportWidth, viewportHeight);
        }
        public bool Update(float deltaTime, IControlContext controlContext, IntPtr window)
        {
            bool valueChanged = false;
            if (Type == "text" && IsFocused)
            {
                _cursorTimer += deltaTime;
                if (_cursorTimer >= CursorBlinkRate)
                {
                    _cursorVisible = !_cursorVisible;
                    _cursorTimer = 0f;
                }
                // Handle keyboard input
                bool shiftPressed = controlContext.GetKey(window, Key.LeftShift) == InputAction.Press || controlContext.GetKey(window, Key.RightShift) == InputAction.Press;
                for (Key key = Key.A; key <= Key.Z; key = (Key)((int)key + 1))
                {
                    if (controlContext.GetKey(window, key) == InputAction.Press)
                    {
                        char ch = (char)((int)key - (int)Key.A + (shiftPressed ? 'A' : 'a'));
                        Value += ch;
                        valueChanged = true;
                    }
                }
                for (Key key = Key.Key0; key <= Key.Key9; key = (Key)((int)key + 1))
                {
                    if (controlContext.GetKey(window, key) == InputAction.Press)
                    {
                        char ch = (char)((int)key - (int)Key.Key0 + '0');
                        Value += ch;
                        valueChanged = true;
                    }
                }
                if (controlContext.GetKey(window, Key.Space) == InputAction.Press)
                {
                    Value += ' ';
                    valueChanged = true;
                }
                if (controlContext.GetKey(window, Key.Backspace) == InputAction.Press || controlContext.GetKey(window, Key.Backspace) == InputAction.Repeat)
                {
                    if (Value.Length > 0)
                    {
                        Value = Value.Substring(0, Value.Length - 1);
                        valueChanged = true;
                    }
                }
            }
            return valueChanged;
        }
    }
}