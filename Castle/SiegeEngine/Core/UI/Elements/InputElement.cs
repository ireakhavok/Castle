// Folder: SiegeEngine.Core.UI.Elements
// File: InputElement.cs
using SiegeEngine.Core.Definitions; // Needed for Key enum
using System;
using System.Numerics;
using System.Globalization;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
namespace SiegeEngine.Core.UI.Elements
{
    public class InputElement : HtmlElement
    {
        public string Type { get; set; }
        public string Value { get; set; } = "";
        public string Placeholder { get; set; } = "";
        private bool _cursorVisible = true;
        private float _cursorTimer = 0f;
        private const float CursorBlinkRate = 0.5f;
        public InputElement()
        {
            Tag = "input";
        }
        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            if (this.Type == "radio")
            {
                Style.Display = "none";
            }
            // FIXED: Input elements (text boxes) now take full available width when no explicit width is set
            if (float.IsNaN(forcedWidth) && string.IsNullOrEmpty(Style.WidthStr) && (this.Type == "text" || this.Type == "number"))
            {
                forcedWidth = parentWidth - HtmlLayoutUtils.ParseMargins(Style, parentWidth, viewportWidth, viewportHeight).W - HtmlLayoutUtils.ParseMargins(Style, parentWidth, viewportWidth, viewportHeight).Y;
            }
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
            if (this.Type == "checkbox" || this.Type == "radio")
            {
                float fs = Style.FontSize;
                if (float.IsNaN(ComputedWidth)) ComputedWidth = fs * 1.5f;
                if (float.IsNaN(ComputedHeight)) ComputedHeight = fs;
            }
            else if (this.Type == "text" || this.Type == "number")
            {
                float fs = Style.FontSize;
                if (float.IsNaN(ComputedHeight)) ComputedHeight = fs * 1.5f;
            }
            else if (this.Type == "range")
            {
                if (float.IsNaN(ComputedHeight)) ComputedHeight = 32f;
            }
        }
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
            if (this.Type == "text" || this.Type == "number")
            {
                float fs = Style.FontSize;
                string displayText = string.IsNullOrEmpty(Value) ? Placeholder : Value;
                Vector4 color = string.IsNullOrEmpty(Value) ? new Vector4(0.5f, 0.5f, 0.5f, 1f) : Style.TextColor != Vector4.Zero ? Style.TextColor : new Vector4(0f, 0f, 0f, 1f);
                textRenderer.RenderText(displayText, ComputedContentX, ComputedContentY, viewportWidth, viewportHeight, fs, color, Style.FontFamily ?? "Arial", parentMatrix);
                if (IsFocused && _cursorVisible)
                {
                    float textW = textRenderer.GetTextSize(Value, fs).X;
                    float cursorX = ComputedContentX + textW;
                    float cursorY = ComputedContentY;
                    float cursorH = fs;
                    float cursorW = 2f;
                    float[] cursorNdc = HtmlLayoutUtils.GetNdcQuad(cursorX, cursorY, cursorW, cursorH, parentMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(cursorNdc, Style.TextColor != Vector4.Zero ? Style.TextColor : new Vector4(0f, 0f, 0f, 1f));
                }
            }
            else
            {
                string symbol = "";
                if (this.Type == "checkbox")
                {
                    symbol = Checked ? "✔" : "";
                }
                else if (this.Type == "radio")
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
            if (this.Type == "checkbox" || this.Type == "radio")
            {
                Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
                Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
                float iw = fs + pad.W + pad.Y + borderW.W + borderW.Y;
                float ih = fs + pad.X + pad.Z + borderW.X + borderW.Z;
                return new Vector2(iw, ih);
            }
            if (this.Type == "text" || this.Type == "number")
            {
                string sizeText = string.IsNullOrEmpty(Value) ? string.IsNullOrEmpty(Placeholder) ? " " : Placeholder : Value;
                float textW = textRenderer.GetTextSize(sizeText, fs).X;
                float textH = textRenderer.GetTextSize("A", fs).Y;
                Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
                Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
                float iw = textW + pad.W + pad.Y + borderW.W + borderW.Y;
                float ih = textH + pad.X + pad.Z + borderW.X + borderW.Z;
                return new Vector2(iw, ih);
            }
            return base.ComputeIntrinsicSize(viewportWidth, viewportHeight, textRenderer, fs);
        }
        public override bool HandleClick(Vector2 mousePos, float viewportWidth, float viewportHeight)
        {
            bool over = base.HandleClick(mousePos, viewportWidth, viewportHeight);
            if (this.Type == "range" && this is RangeElement range && over && IsActive)
            {
                // Transform-aware local mapping so absolute + CSS transform elements work correctly
                Vector2 local = ScreenToLocal(mousePos, viewportWidth, viewportHeight);
                float relX = Math.Clamp(local.X - ComputedContentX, 0f, ComputedContentWidth);
                float percent = relX / ComputedContentWidth;
                float newValue = range.Min + percent * (range.Max - range.Min);
                if (range.Step > 0) newValue = (float)Math.Round(newValue / range.Step) * range.Step;
                newValue = Math.Clamp(newValue, range.Min, range.Max);
                if (Math.Abs(range.Value - newValue) > 0.0001f)
                {
                    range.Value = newValue;
                    Value = newValue.ToString(CultureInfo.InvariantCulture);
                    Attributes["value"] = Value;
                }
            }
            return over;
        }
        public bool Update(float deltaTime, IControlContext controlContext, nint window)
        {
            bool valueChanged = false;
            if ((this.Type == "text" || this.Type == "number") && IsFocused)
            {
                _cursorTimer += deltaTime;
                if (_cursorTimer >= CursorBlinkRate)
                {
                    _cursorVisible = !_cursorVisible;
                    _cursorTimer = 0f;
                    valueChanged = true;
                }
            }
            return valueChanged;
        }
        public static char? GetCharFromKey(Key key, bool shiftPressed, string inputType)
        {
            if (inputType == "number")
            {
                if (key >= Key.Key0 && key <= Key.Key9)
                {
                    return (char)((int)key - (int)Key.Key0 + '0');
                }
                if (key == Key.Period)
                {
                    return '.';
                }
                if (key == Key.Minus)
                {
                    return '-';
                }
                return null;
            }
            if (key >= Key.A && key <= Key.Z)
            {
                return (char)((int)key - (int)Key.A + (shiftPressed ? 'A' : 'a'));
            }
            else if (key >= Key.Key0 && key <= Key.Key9)
            {
                char noShift = (char)((int)key - (int)Key.Key0 + '0');
                char withShift = key switch
                {
                    Key.Key0 => ')',
                    Key.Key1 => '!',
                    Key.Key2 => '@',
                    Key.Key3 => '#',
                    Key.Key4 => '$',
                    Key.Key5 => '%',
                    Key.Key6 => '^',
                    Key.Key7 => '&',
                    Key.Key8 => '*',
                    Key.Key9 => '(',
                    _ => noShift
                };
                return shiftPressed ? withShift : noShift;
            }
            else if (key == Key.Space)
            {
                return ' ';
            }
            else if (key == Key.Minus)
            {
                return shiftPressed ? '_' : '-';
            }
            else if (key == Key.Equal)
            {
                return shiftPressed ? '+' : '=';
            }
            else if (key == Key.LeftBracket)
            {
                return shiftPressed ? '{' : '[';
            }
            else if (key == Key.RightBracket)
            {
                return shiftPressed ? '}' : ']';
            }
            else if (key == Key.Backslash)
            {
                return shiftPressed ? '|' : '\\';
            }
            else if (key == Key.Semicolon)
            {
                return shiftPressed ? ':' : ';';
            }
            else if (key == Key.Apostrophe)
            {
                return shiftPressed ? '"' : '\'';
            }
            else if (key == Key.Comma)
            {
                return shiftPressed ? '<' : ',';
            }
            else if (key == Key.Period)
            {
                return shiftPressed ? '>' : '.';
            }
            else if (key == Key.Slash)
            {
                return shiftPressed ? '?' : '/';
            }
            else if (key == Key.GraveAccent)
            {
                return shiftPressed ? '~' : '`';
            }
            return null;
        }
    }
}