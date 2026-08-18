// Folder: SiegeEngine.Core.UI.Elements
// File: RangeElement.cs
using System;
using System.Numerics;
using System.Globalization;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
namespace SiegeEngine.Core.UI.Elements
{
    public class RangeElement : InputElement
    {
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 100f;
        public float Step { get; set; } = 0.5f;
        public float Value { get; set; } = 10f;
        public RangeElement()
        {
            Tag = "input";
            Type = "range";
        }
        public override Vector2 ComputeIntrinsicSize(float viewportWidth, float viewportHeight, TextRenderer textRenderer, float fs)
        {
            Vector4 pad = HtmlLayoutUtils.ParsePaddings(Style, 0, viewportWidth, viewportHeight);
            Vector4 borderW = HtmlLayoutUtils.ParseBorderWidths(Style, 0, viewportWidth, viewportHeight);
            float iw = 220f + pad.W + pad.Y + borderW.W + borderW.Y;
            float ih = 32f + pad.X + pad.Z + borderW.X + borderW.Z;
            return new Vector2(iw, ih);
        }
        public override void ComputeLayout(float parentPositionX, float parentPositionY, float parentWidth, float parentHeight, float viewportWidth, float viewportHeight, TextRenderer textRenderer, float parentFs, float forcedWidth = float.NaN, float forcedHeight = float.NaN)
        {
            if (Attributes.TryGetValue("min", out string minStr) && float.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float minVal))
            {
                Min = minVal;
            }
            if (Attributes.TryGetValue("max", out string maxStr) && float.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float maxVal))
            {
                Max = maxVal;
            }
            if (Attributes.TryGetValue("step", out string stepStr) && float.TryParse(stepStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float stepVal) && stepVal > 0f)
            {
                Step = stepVal;
            }
            if (Attributes.TryGetValue("value", out string valStr) && float.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsed))
            {
                Value = parsed;
            }
            base.ComputeLayout(parentPositionX, parentPositionY, parentWidth, parentHeight, viewportWidth, viewportHeight, textRenderer, parentFs, forcedWidth, forcedHeight);
        }
        public override void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            base.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, parentMatrix);
            float trackY = ComputedContentY + (ComputedContentHeight - 6f) / 2f;
            float[] trackNdc = HtmlLayoutUtils.GetNdcQuad(ComputedContentX, trackY, ComputedContentWidth, 6f, parentMatrix, viewportWidth, viewportHeight);
            quadRenderer.DrawNdcQuad(trackNdc, new Vector4(0.35f, 0.35f, 0.35f, 1f));
            float percent = Math.Clamp((Value - Min) / (Max - Min), 0f, 1f);
            float filledW = percent * ComputedContentWidth;
            float[] filledNdc = HtmlLayoutUtils.GetNdcQuad(ComputedContentX, trackY, filledW, 6f, parentMatrix, viewportWidth, viewportHeight);
            quadRenderer.DrawNdcQuad(filledNdc, new Vector4(0.2f, 0.6f, 1f, 1f));
            float thumbX = ComputedContentX + filledW - 8f;
            float[] thumbNdc = HtmlLayoutUtils.GetNdcQuad(thumbX, ComputedContentY + 4f, 16f, ComputedContentHeight - 8f, parentMatrix, viewportWidth, viewportHeight);
            quadRenderer.DrawNdcQuad(thumbNdc, new Vector4(0.95f, 0.95f, 0.95f, 1f));
        }
    }
}