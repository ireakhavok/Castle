// Folder: SiegeEngine.Core.UI
// File: Render.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
namespace SiegeEngine.Core.UI
{
    public partial class HtmlElement
    {
        public virtual void RenderBackgroundOnly(IRenderContext renderContext, TextRenderer textRenderer, UIQuadRenderer quadRenderer, float viewportWidth, float viewportHeight, Matrix4x4 parentMatrix)
        {
            CssStyle effectiveStyle = Style;
            if (Checked && PseudoStyles.TryGetValue("checked", out CssStyle checkedStyle))
                effectiveStyle = checkedStyle;
            if (IsFocused && PseudoStyles.TryGetValue("focus", out CssStyle focusStyle))
                effectiveStyle = focusStyle;
            if (IsTarget && PseudoStyles.TryGetValue("target", out CssStyle targetStyle))
                effectiveStyle = targetStyle;
            if (effectiveStyle.Display == "none") return;

            Matrix4x4 rootBgMatrix = parentMatrix;
            Matrix4x4 contentMatrix = _needsVerticalScrollbar
                ? parentMatrix * Matrix4x4.CreateTranslation(0, -ScrollOffsetY, 0)
                : parentMatrix;

            float backgroundHeight = (_needsVerticalScrollbar && _contentFullHeight > ComputedBackgroundHeight + 0.1f)
                ? _contentFullHeight
                : ComputedBackgroundHeight;

            Vector4 borderTopC = effectiveStyle.BorderTopColor != Vector4.Zero ? effectiveStyle.BorderTopColor : effectiveStyle.BorderColor;
            Vector4 borderRightC = effectiveStyle.BorderRightColor != Vector4.Zero ? effectiveStyle.BorderRightColor : effectiveStyle.BorderColor;
            Vector4 borderBottomC = effectiveStyle.BorderBottomColor != Vector4.Zero ? effectiveStyle.BorderBottomColor : effectiveStyle.BorderColor;
            Vector4 borderLeftC = effectiveStyle.BorderLeftColor != Vector4.Zero ? effectiveStyle.BorderLeftColor : effectiveStyle.BorderColor;
            Vector4 borderW = BorderWidth;
            bool uniformBorder = borderW.X == borderW.Y && borderW.Y == borderW.Z && borderW.Z == borderW.W;
            bool uniformColor = borderTopC == borderRightC && borderRightC == borderBottomC && borderBottomC == borderLeftC;
            bool hasUniformBorder = uniformBorder && uniformColor && borderW.X > 0;

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
            float drawH = useShaderForBorder ? ComputedHeight : backgroundHeight;
            float bw = useShaderForBorder ? borderW.X : 0f;
            Vector4 borderC = useShaderForBorder ? borderTopC : Vector4.Zero;
            Vector4 fillColor = effectiveStyle.BackgroundColor;
            if (effectiveStyle.Background != null && effectiveStyle.Background.Contains("linear-gradient"))
            {
                fillColor = Vector4.Zero;
            }

            if (hasBg || useShaderForBorder)
            {
                float[] bgNdc = HtmlLayoutUtils.GetNdcQuad(drawX, drawY, drawW, drawH, rootBgMatrix, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(bgNdc, fillColor, br, new Vector2(drawW, drawH), bw, borderC);
            }

            if (effectiveStyle.Background != null && effectiveStyle.Background.Contains("linear-gradient"))
            {
                Vector4 gridColor = new Vector4(0.267f, 0.267f, 0.267f, 1f);
                float step = 28f;
                var sizeMatch = Regex.Match(effectiveStyle.Background, @"/\s*(\d+(?:\.\d+)?)px");
                if (sizeMatch.Success) float.TryParse(sizeMatch.Groups[1].Value, out step);
                var colorMatch = Regex.Match(effectiveStyle.Background, @"#([0-9a-fA-F]{3,6})");
                if (colorMatch.Success)
                {
                    string hex = colorMatch.Groups[1].Value;
                    if (hex.Length == 3) hex = "" + hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
                    if (hex.Length == 6)
                    {
                        int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                        int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                        int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                        gridColor = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
                    }
                }
                for (float x = 0; x <= drawW; x += step)
                {
                    quadRenderer.DrawLine(drawX + x, drawY, drawX + x, drawY + drawH, 1f, gridColor, viewportWidth, viewportHeight);
                }
                for (float y = 0; y <= drawH; y += step)
                {
                    quadRenderer.DrawLine(drawX, drawY + y, drawX + drawW, drawY + y, 1f, gridColor, viewportWidth, viewportHeight);
                }
            }

            if (_bgRenderer != null)
            {
                _bgRenderer.Render(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, backgroundHeight, viewportWidth, viewportHeight);
            }

            // FIXED: Do NOT recurse to children in the background-only pass.
            // Child backgrounds are already rendered naturally in the full Render() pass.
            // This eliminates the duplicate "ghost" HTML elements behind the form content.
            // (Only the root/container background is drawn here.)
            // foreach (var child in Children)
            // {
            //     child.RenderBackgroundOnly(...);
            // }
        }

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
            Matrix4x4 contentMatrix = _needsVerticalScrollbar
                ? localMatrix * Matrix4x4.CreateTranslation(0, -ScrollOffsetY, 0)
                : localMatrix;

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
            Vector4 fillColor = effectiveStyle.BackgroundColor;
            if (effectiveStyle.Background != null && effectiveStyle.Background.Contains("linear-gradient"))
            {
                fillColor = Vector4.Zero;
            }

            if (hasBg || useShaderForBorder)
            {
                float[] bgNdc = HtmlLayoutUtils.GetNdcQuad(drawX, drawY, drawW, drawH, localMatrix, viewportWidth, viewportHeight);
                quadRenderer.DrawNdcQuad(bgNdc, fillColor, br, new Vector2(drawW, drawH), bw, borderC);
            }

            if (effectiveStyle.Background != null && effectiveStyle.Background.Contains("linear-gradient"))
            {
                Vector4 gridColor = new Vector4(0.267f, 0.267f, 0.267f, 1f);
                float step = 28f;
                var sizeMatch = Regex.Match(effectiveStyle.Background, @"/\s*(\d+(?:\.\d+)?)px");
                if (sizeMatch.Success) float.TryParse(sizeMatch.Groups[1].Value, out step);
                var colorMatch = Regex.Match(effectiveStyle.Background, @"#([0-9a-fA-F]{3,6})");
                if (colorMatch.Success)
                {
                    string hex = colorMatch.Groups[1].Value;
                    if (hex.Length == 3) hex = "" + hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
                    if (hex.Length == 6)
                    {
                        int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                        int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                        int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                        gridColor = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
                    }
                }
                for (float x = 0; x <= drawW; x += step)
                {
                    quadRenderer.DrawLine(drawX + x, drawY, drawX + x, drawY + drawH, 1f, gridColor, viewportWidth, viewportHeight);
                }
                for (float y = 0; y <= drawH; y += step)
                {
                    quadRenderer.DrawLine(drawX, drawY + y, drawX + drawW, drawY + y, 1f, gridColor, viewportWidth, viewportHeight);
                }
            }

            if (_bgRenderer != null)
            {
                _bgRenderer.Render(ComputedBackgroundX, ComputedBackgroundY, ComputedBackgroundWidth, ComputedBackgroundHeight, viewportWidth, viewportHeight);
            }

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