using SiegeEngine.Core.Rendering;
using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace SiegeEngine.Core.UI
{
    public static class HtmlLayoutUtils
    {
        public static float ParseSize(string s, float parent, float vw, float vh)
        {
            if (string.IsNullOrEmpty(s) || s == "auto") return float.NaN;
            s = s.Trim();
            float value;
            if (float.TryParse(s, out value)) return value;
            if (s.EndsWith("%"))
            {
                value = float.Parse(s.Replace("%", ""));
                return value / 100 * parent;
            }
            else if (s.EndsWith("vh"))
            {
                value = float.Parse(s.Replace("vh", ""));
                return value / 100 * vh;
            }
            else if (s.EndsWith("vw"))
            {
                value = float.Parse(s.Replace("vw", ""));
                return value / 100 * vw;
            }
            else if (s.EndsWith("px"))
            {
                value = float.Parse(s.Replace("px", ""));
                return value;
            }
            else
            {
                return float.NaN;
            }
        }
        public static Vector4 ParsePaddings(CssStyle style, float parent, float vw, float vh)
        {
            string allStr = style.PaddingStr;
            Vector4 values = string.IsNullOrEmpty(allStr) ? Vector4.Zero : ParseSides(allStr, parent, vw, vh);
            string topStr = style.PaddingTopStr;
            string rightStr = style.PaddingRightStr;
            string bottomStr = style.PaddingBottomStr;
            string leftStr = style.PaddingLeftStr;
            if (!string.IsNullOrEmpty(topStr)) values.X = ParseSize(topStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(rightStr)) values.Y = ParseSize(rightStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(bottomStr)) values.Z = ParseSize(bottomStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(leftStr)) values.W = ParseSize(leftStr, parent, vw, vh);
            if (float.IsNaN(values.X)) values.X = 0;
            if (float.IsNaN(values.Y)) values.Y = 0;
            if (float.IsNaN(values.Z)) values.Z = 0;
            if (float.IsNaN(values.W)) values.W = 0;
            return values;
        }
        public static Vector4 ParseMargins(CssStyle style, float parent, float vw, float vh)
        {
            string allStr = style.MarginStr;
            Vector4 values = string.IsNullOrEmpty(allStr) ? Vector4.Zero : ParseSides(allStr, parent, vw, vh);
            string topStr = style.MarginTopStr;
            string rightStr = style.MarginRightStr;
            string bottomStr = style.MarginBottomStr;
            string leftStr = style.MarginLeftStr;
            if (!string.IsNullOrEmpty(topStr)) values.X = ParseSize(topStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(rightStr)) values.Y = ParseSize(rightStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(bottomStr)) values.Z = ParseSize(bottomStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(leftStr)) values.W = ParseSize(leftStr, parent, vw, vh);
            return values;
        }
        public static Vector4 ParseBorderWidths(CssStyle style, float parent, float vw, float vh)
        {
            Vector4 values = string.IsNullOrEmpty(style.BorderWidthStr) ? Vector4.Zero : ParseSides(style.BorderWidthStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(style.BorderTopWidthStr)) values.X = ParseSize(style.BorderTopWidthStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(style.BorderRightWidthStr)) values.Y = ParseSize(style.BorderRightWidthStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(style.BorderBottomWidthStr)) values.Z = ParseSize(style.BorderBottomWidthStr, parent, vw, vh);
            if (!string.IsNullOrEmpty(style.BorderLeftWidthStr)) values.W = ParseSize(style.BorderLeftWidthStr, parent, vw, vh);
            if (float.IsNaN(values.X)) values.X = 0;
            if (float.IsNaN(values.Y)) values.Y = 0;
            if (float.IsNaN(values.Z)) values.Z = 0;
            if (float.IsNaN(values.W)) values.W = 0;
            return values;
        }
        public static Vector4 ParseSides(string s, float parent, float vw, float vh)
        {
            if (string.IsNullOrEmpty(s)) return Vector4.Zero;
            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float GetVal(int index, float defaultVal)
            {
                if (index < parts.Length)
                {
                    return ParseSize(parts[index], parent, vw, vh);
                }
                return defaultVal;
            }
            float val1 = GetVal(0, 0);
            float val2 = GetVal(1, val1);
            float val3 = GetVal(2, val1);
            float val4 = GetVal(3, val2);
            return new Vector4(val1, val2, val3, val4);
        }
        public static Matrix4x4 ComputeTransform(HtmlElement elem, float viewportWidth, float viewportHeight)
        {
            if (string.IsNullOrEmpty(elem.Style.Transform) || elem.Style.Transform == "none") return Matrix4x4.Identity;
            Matrix4x4 mat = Matrix4x4.Identity;
            var matches = Regex.Matches(elem.Style.Transform, @"(\w+)\((.+?)\)");
            foreach (Match m in matches)
            {
                string func = m.Groups[1].Value.ToLower();
                string args = m.Groups[2].Value;
                var argParts = args.Split(',').Select(a => a.Trim()).ToArray();
                Matrix4x4 fmat = Matrix4x4.Identity;
                switch (func)
                {
                    case "translate":
                        {
                            float tx = ParseSize(argParts[0], elem.ComputedWidth, viewportWidth, viewportHeight);
                            float ty = argParts.Length > 1 ? ParseSize(argParts[1], elem.ComputedHeight, viewportWidth, viewportHeight) : 0;
                            fmat = Matrix4x4.CreateTranslation(tx, ty, 0);
                            break;
                        }
                    case "translatex":
                        {
                            float tx = ParseSize(argParts[0], elem.ComputedWidth, viewportWidth, viewportHeight);
                            fmat = Matrix4x4.CreateTranslation(tx, 0, 0);
                            break;
                        }
                    case "rotate":
                        {
                            float angle = ParseAngle(argParts[0]);
                            fmat = Matrix4x4.CreateRotationZ(angle);
                            break;
                        }
                    case "scale":
                        {
                            float sx = float.Parse(argParts[0]);
                            float sy = argParts.Length > 1 ? float.Parse(argParts[1]) : sx;
                            fmat = Matrix4x4.CreateScale(sx, sy, 1);
                            break;
                        }
                    case "skew":
                        {
                            float ax = ParseAngle(argParts[0]);
                            float ay = argParts.Length > 1 ? ParseAngle(argParts[1]) : 0;
                            fmat = new Matrix4x4(
                                1, MathF.Tan(ay), 0, 0,
                                MathF.Tan(ax), 1, 0, 0,
                                0, 0, 1, 0,
                                0, 0, 0, 1);
                            break;
                        }
                    case "matrix":
                        {
                            float a = float.Parse(argParts[0]);
                            float b = float.Parse(argParts[1]);
                            float c = float.Parse(argParts[2]);
                            float d = float.Parse(argParts[3]);
                            float tx = float.Parse(argParts[4]);
                            float ty = float.Parse(argParts[5]);
                            fmat = new Matrix4x4(
                                a, b, 0, 0,
                                c, d, 0, 0,
                                0, 0, 1, 0,
                                tx, ty, 0, 1);
                            break;
                        }
                }
                mat = mat * fmat;
            }
            Vector3 origin = new Vector3(elem.ComputedWidth / 2, elem.ComputedHeight / 2, 0);
            Matrix4x4 toOrigin = Matrix4x4.CreateTranslation(-origin);
            Matrix4x4 fromOrigin = Matrix4x4.CreateTranslation(origin);
            mat = fromOrigin * mat * toOrigin;
            return mat;
        }
        public static float ParseAngle(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            float val;
            if (s.EndsWith("deg"))
            {
                val = float.Parse(s.Replace("deg", ""));
                return val * MathF.PI / 180;
            }
            else if (s.EndsWith("rad"))
            {
                val = float.Parse(s.Replace("rad", ""));
                return val;
            }
            else if (s.EndsWith("turn"))
            {
                val = float.Parse(s.Replace("turn", ""));
                return val * MathF.PI * 2;
            }
            else if (s.EndsWith("grad"))
            {
                val = float.Parse(s.Replace("grad", ""));
                return val * MathF.PI / 200;
            }
            val = float.Parse(s);
            return val * MathF.PI / 180;
        }
        public static float[] GetNdcQuad(float x, float y, float w, float h, Matrix4x4 trans, float vw, float vh)
        {
            Vector4 bl = Vector4.Transform(new Vector4(x, y + h, 0, 1), trans);
            Vector4 br = Vector4.Transform(new Vector4(x + w, y + h, 0, 1), trans);
            Vector4 tr = Vector4.Transform(new Vector4(x + w, y, 0, 1), trans);
            Vector4 tl = Vector4.Transform(new Vector4(x, y, 0, 1), trans);
            bl /= bl.W;
            br /= br.W;
            tr /= tr.W;
            tl /= tl.W;
            float blx = 2 * bl.X / vw - 1;
            float bly = 1 - 2 * bl.Y / vh;
            float brx = 2 * br.X / vw - 1;
            float bry = 1 - 2 * br.Y / vh;
            float trx = 2 * tr.X / vw - 1;
            float try_ = 1 - 2 * tr.Y / vh;
            float tlx = 2 * tl.X / vw - 1;
            float tly = 1 - 2 * tl.Y / vh;
            return new float[] { blx, bly, brx, bry, trx, try_, tlx, tly };
        }
    }
}