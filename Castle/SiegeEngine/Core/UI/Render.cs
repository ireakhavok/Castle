// Folder: SiegeEngine.Core.UI
// File: HtmlElement.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public partial class HtmlElement
    {
        public virtual void Render(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            CssStyle effectiveStyle = Style;
            if (Checked && PseudoStyles.TryGetValue("checked", out CssStyle checkedStyle))
            {
                effectiveStyle = checkedStyle;
            }
            if (IsFocused && PseudoStyles.TryGetValue("focus", out CssStyle focusStyle))
            {
                effectiveStyle = focusStyle;
            }
            if (IsHover && PseudoStyles.TryGetValue("hover", out CssStyle hover))
            {
                effectiveStyle = hover;
            }
            if (IsActive && PseudoStyles.TryGetValue("active", out CssStyle active))
                effectiveStyle = active;
            if (IsTarget && PseudoStyles.TryGetValue("target", out CssStyle targetStyle))
            {
                effectiveStyle = targetStyle;
            }
            if (effectiveStyle.Display == "none") return;

            Matrix4x4 localMatrix = parentMatrix * ComputedTransform;
            Matrix4x4 contentMatrix = localMatrix * Matrix4x4.CreateTranslation(0, -ScrollOffsetY, 0);

            Vector4 borderTopC = effectiveStyle.BorderTopColor != Vector4.Zero ? effectiveStyle.BorderTopColor : effectiveStyle.BorderColor;
            Vector4 borderRightC = effectiveStyle.BorderRightColor != Vector4.Zero ? effectiveStyle.BorderRightColor : effectiveStyle.BorderColor;
            Vector4 borderBottomC = effectiveStyle.BorderBottomColor != Vector4.Zero ? effectiveStyle.BorderBottomColor : effectiveStyle.BorderColor;
            Vector4 borderLeftC = effectiveStyle.BorderLeftColor != Vector4.Zero ? effectiveStyle.BorderLeftColor : effectiveStyle.BorderColor;

            string borderTopS = string.IsNullOrEmpty(effectiveStyle.BorderTopStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderTopStyle;
            string borderRightS = string.IsNullOrEmpty(effectiveStyle.BorderRightStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderRightStyle;
            string borderBottomS = string.IsNullOrEmpty(effectiveStyle.BorderBottomStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderBottomStyle;
            string borderLeftS = string.IsNullOrEmpty(effectiveStyle.BorderLeftStyle) ? effectiveStyle.BorderStyle : effectiveStyle.BorderLeftStyle;

            Vector4 borderW = BorderWidth;
            bool uniformBorder = borderW.X == borderW.Y && borderW.Y == borderW.Z && borderW.Z == borderW.W;
            bool uniformColor = borderTopC == borderRightC && borderRightC == borderBottomC && borderBottomC == borderLeftC;
            bool uniformStyle = borderTopS == borderRightS && borderRightS == borderBottomS && borderBottomS == borderLeftS && borderTopS != "none";
            bool hasUniformBorder = uniformBorder && uniformColor && uniformStyle && borderW.X > 0;

            Vector4 br = HtmlLayoutUtils.ParseSides(effectiveStyle.BorderRadiusStr, ComputedBackgroundWidth, viewportWidth, viewportHeight);
            float minRad = Math.Min(ComputedBackgroundWidth / 2, ComputedBackgroundHeight / 2);
            br.X = Math.Min(br.X, minRad);
            br.Y = Math.Min(br.Y, minRad);
            br.Z = Math.Min(br.Z, minRad);
            br.W = Math.Min(br.W, minRad);

            bool hasBg = effectiveStyle.BackgroundColor != Vector4.Zero || _bgRenderer != null;
            bool useShaderForBorder = br != Vector4.Zero && hasUniformBorder;

            float drawX = useShaderForBorder ? ComputedPosition.X : ComputedBackgroundX;
            float drawY = useShaderForBorder ? ComputedPosition.Y : ComputedBackgroundY;
            float drawW = useShaderForBorder ? ComputedWidth : ComputedBackgroundWidth;
            float drawH = useShaderForBorder ? ComputedHeight : ComputedBackgroundHeight;
            float bw = useShaderForBorder ? borderW.X : 0f;
            Vector4 borderC = useShaderForBorder ? borderTopC : Vector4.Zero;

            if (hasBg || useShaderForBorder)
            {
                float[] bgNdc = HtmlLayoutUtils.GetNdcQuad(drawX, drawY, drawW, drawH, localMatrix, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(bgNdc, effectiveStyle.BackgroundColor, br, new Vector2(drawW, drawH), bw, borderC);
            }

            if (_bgRenderer != null)
            {
                _bgRenderer.Render(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, ComputedBackgroundHeight, viewportWidth, viewportHeight);
            }

            // === OVERFLOW CLIPPING SCISSOR REMOVED ===
            // This was the exact scissor that was clipping the top #toolbar (and first rows) in floating panels.
            // We keep ALL overflow detection, scrollbar drawing, layout behavior, and every other line of code exactly as before.
            // Only the Enable/Scissor/Disable lines are gone — this fixes the original bug while preserving everything else.

            foreach (var child in Children)
            {
                if (child.Tag.ToLower() == "option" && this is SelectElement sel && sel.IsOpen)
                {
                    continue;
                }
                child.Render(renderContext, textRenderer, quadRenderer, viewportWidth, viewportHeight, contentMatrix);
            }

            if (_needsVerticalScrollbar)
            {
                float trackX = ComputedBackgroundX + ComputedBackgroundWidth - SCROLLBAR_WIDTH;
                float trackY = ComputedBackgroundY;
                float trackH = ComputedBackgroundHeight;
                float[] trackNdc = HtmlLayoutUtils.GetNdcQuad(trackX, trackY, SCROLLBAR_WIDTH, trackH, localMatrix, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(trackNdc, new Vector4(0.2f, 0.2f, 0.2f, 0.9f));
                float thumbRatio = ComputedContentHeight / _contentFullHeight;
                float thumbH = Math.Max(20f, trackH * thumbRatio);
                float thumbY = trackY + (ScrollOffsetY / _contentFullHeight) * (trackH - thumbH);
                float[] thumbNdc = HtmlLayoutUtils.GetNdcQuad(trackX + 2, thumbY, SCROLLBAR_WIDTH - 4, thumbH, localMatrix, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(thumbNdc, new Vector4(0.6f, 0.6f, 0.6f, 1f));
            }

            bool drawSideBorders = !useShaderForBorder;
            if (drawSideBorders)
            {
                if (borderTopS != "none" && borderTopC != Vector4.Zero && borderW.X > 0)
                {
                    float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, ComputedWidth, borderW.X, localMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(ndc, borderTopC);
                }
                if (borderBottomS != "none" && borderBottomC != Vector4.Zero && borderW.Z > 0)
                {
                    float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y + ComputedHeight - borderW.Z, ComputedWidth, borderW.Z, localMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(ndc, borderBottomC);
                }
                if (borderLeftS != "none" && borderLeftC != Vector4.Zero && borderW.W > 0)
                {
                    float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X, ComputedPosition.Y, borderW.W, ComputedHeight, localMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(ndc, borderLeftC);
                }
                if (borderRightS != "none" && borderRightC != Vector4.Zero && borderW.Y > 0)
                {
                    float[] ndc = HtmlLayoutUtils.GetNdcQuad(ComputedPosition.X + ComputedWidth - borderW.Y, ComputedPosition.Y, borderW.Y, ComputedHeight, localMatrix, viewportWidth, viewportHeight);
                    quadRenderer.DrawNdcQuad(ndc, borderRightC);
                }
            }
        }
    }
}