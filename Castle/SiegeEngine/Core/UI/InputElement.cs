// Folder: SiegeEngine.Core.UI
// File: InputElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using System;
using System.Numerics;
using System.Globalization;

namespace SiegeEngine.Core.UI
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
            if (Type == "radio")
            {
                Style.Display = "none";
            }

            // Range value sync from attribute (for JS .value = ... from number field)
            if (Type == "range" && this is RangeElement range && Attributes.TryGetValue("value", out string valStr))
            {
                if (float.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsed))
                {
                    range.Value = parsed;
                }
            }

            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);

            if (Type == "checkbox" || Type == "radio")
            {
                float fs = Style.FontSize;
                if (float.IsNaN(ComputedWidth)) ComputedWidth = fs * 1.5f;
                if (float.IsNaN(ComputedHeight)) ComputedHeight = fs;
            }
            else if (Type == "text" || Type == "number")
            {
                float fs = Style.FontSize;
                if (float.IsNaN(ComputedHeight)) ComputedHeight = fs * 1.5f;
            }
            else if (Type == "range")
            {
                if (float.IsNaN(ComputedHeight)) ComputedHeight = 32f;
            }
        }

        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
            if (Type == "text" || Type == "number")
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
                    float[] cursorNdc = GetNdcQuad(cursorX, cursorY, cursorW, cursorH, parentMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(cursorNdc, Style.TextColor != Vector4.Zero ? Style.TextColor : new Vector4(0f, 0f, 0f, 1f));
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
            if (Type == "text" || Type == "number")
            {
                string sizeText = string.IsNullOrEmpty(Value) ? string.IsNullOrEmpty(Placeholder) ? " " : Placeholder : Value;
                float textW = textRenderer.GetTextSize(sizeText, fs).X;
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
            bool over = base.HandleClick(mousePos, viewportWidth, viewportHeight);

            // ALL range drag handling now lives here in InputElement class (as requested)
            if (Type == "range" && this is RangeElement range && over && IsActive)
            {
                float relX = Math.Clamp(mousePos.X - ComputedContentX, 0f, ComputedContentWidth);
                float percent = relX / ComputedContentWidth;
                float newValue = range.Min + percent * (range.Max - range.Min);
                if (range.Step > 0) newValue = (float)Math.Round(newValue / range.Step) * range.Step;
                newValue = Math.Clamp(newValue, range.Min, range.Max);

                if (Math.Abs(range.Value - newValue) > 0.0001f)
                {
                    range.Value = newValue;
                    Value = newValue.ToString(CultureInfo.InvariantCulture); // sync string Value (used by JS)
                    Attributes["value"] = Value;
                }
            }

            return over;
        }

        public bool Update(float deltaTime, IControlContext controlContext, nint window)
        {
            bool valueChanged = false;
            if ((Type == "text" || Type == "number") && IsFocused)
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
    }
}