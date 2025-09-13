// Folder: SiegeEngine.UI
// File: CssParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace SiegeEngine.UI
{
    public class CssParser
    {
        public void Apply(string css, HtmlElement root)
        {
            int i = 0;
            while (i < css.Length)
            {
                SkipWhitespace(css, ref i);
                string selector = ReadUntil(css, ref i, '{').Trim();
                i++; // skip {
                string block = ReadUntil(css, ref i, '}').Trim();
                i++; // skip }
                Dictionary<string, string> props = ParseProperties(block);
                string pseudo = null;
                if (selector.Contains(":"))
                {
                    var parts = selector.Split(':');
                    selector = parts[0].Trim();
                    pseudo = parts[1].Trim();
                }
                ApplyToElements(root, selector, props, pseudo);
            }
        }
        private void SkipWhitespace(string css, ref int i)
        {
            while (i < css.Length && char.IsWhiteSpace(css[i]))
                i++;
        }
        private string ReadUntil(string css, ref int i, char stop)
        {
            string result = "";
            while (i < css.Length && css[i] != stop)
            {
                result += css[i];
                i++;
            }
            return result;
        }
        private Dictionary<string, string> ParseProperties(string block)
        {
            Dictionary<string, string> props = new Dictionary<string, string>();
            int j = 0;
            while (j < block.Length)
            {
                SkipWhitespace(block, ref j);
                string key = ReadUntil(block, ref j, ':').Trim();
                j++; // skip :
                SkipWhitespace(block, ref j);
                string value = ReadUntil(block, ref j, ';').Trim();
                if (j < block.Length && block[j] == ';') j++;
                props[key] = value;
            }
            return props;
        }
        private void ApplyToElements(HtmlElement root, string selector, Dictionary<string, string> props, string pseudo)
        {
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                HtmlElement elem = queue.Dequeue();
                if (Matches(elem, selector))
                {
                    CssStyle targetStyle;
                    if (pseudo != null)
                    {
                        if (!elem.PseudoStyles.ContainsKey(pseudo))
                            elem.PseudoStyles[pseudo] = elem.Style.Clone();
                        targetStyle = elem.PseudoStyles[pseudo];
                    }
                    else
                    {
                        targetStyle = elem.Style;
                    }
                    ApplyProperties(targetStyle, props);
                }
                foreach (var child in elem.Children)
                {
                    queue.Enqueue(child);
                }
            }
        }
        private bool Matches(HtmlElement elem, string selector)
        {
            if (string.IsNullOrEmpty(selector)) return true;
            var parts = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            HtmlElement current = elem;
            for (int k = parts.Length - 1; k >= 0; k--)
            {
                string part = parts[k];
                if (string.IsNullOrEmpty(part)) continue;
                bool match = false;
                if (part == "*")
                {
                    match = true;
                }
                else
                {
                    bool isId = part.StartsWith('#');
                    bool isClass = part.StartsWith('.');
                    string name = isId ? part.Substring(1) : isClass ? part.Substring(1) : part;
                    if (isId)
                    {
                        match = elem.Attributes.GetValueOrDefault("id", "") == name;
                    }
                    else if (isClass)
                    {
                        string classes = current.Attributes.GetValueOrDefault("class", "");
                        match = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name);
                    }
                    else
                    {
                        match = string.Equals(current.Tag, name, StringComparison.OrdinalIgnoreCase);
                    }
                }
                if (!match) return false;
                current = current.Parent;
                if (current == null && k > 0) return false;
            }
            return true;
        }
        private void ApplyProperties(CssStyle style, Dictionary<string, string> props)
        {
            if (props.TryGetValue("position", out string pos))
                style.Position = pos;
            if (props.TryGetValue("left", out string left))
                style.LeftStr = left;
            if (props.TryGetValue("top", out string top))
                style.TopStr = top;
            if (props.TryGetValue("width", out string width))
                style.WidthStr = width;
            if (props.TryGetValue("height", out string height))
                style.HeightStr = height;
            if (props.TryGetValue("min-width", out string minw))
                style.MinWidthStr = minw;
            if (props.TryGetValue("min-height", out string minh))
                style.MinHeightStr = minh;
            if (props.TryGetValue("max-width", out string maxw))
                style.MaxWidthStr = maxw;
            if (props.TryGetValue("max-height", out string maxh))
                style.MaxHeightStr = maxh;
            if (props.TryGetValue("background", out string bg) || props.TryGetValue("background-color", out bg))
            {
                style.Background = bg;
                style.BackgroundColor = ParseColor(bg);
            }
            if (props.TryGetValue("color", out string textColorStr))
            {
                style.Color = textColorStr;
                style.TextColor = ParseColor(textColorStr);
            }
            if (props.TryGetValue("font-size", out string fs))
            {
                style.FontSizeStr = fs;
            }
            if (props.TryGetValue("font-family", out string ff))
            {
                style.FontFamily = ff.Trim('\'', '"');
            }
            if (props.TryGetValue("display", out string disp))
                style.Display = disp;
            if (props.TryGetValue("flex-direction", out string fd))
                style.FlexDirection = fd;
            if (props.TryGetValue("align-items", out string ai))
                style.AlignItems = ai;
            if (props.TryGetValue("justify-content", out string jc))
                style.JustifyContent = jc;
            if (props.TryGetValue("flex", out string flex))
                style.Flex = flex;
            if (props.TryGetValue("padding", out string pad))
            {
                style.PaddingStr = pad;
            }
            if (props.TryGetValue("padding-top", out string ptop))
                style.PaddingTopStr = ptop;
            if (props.TryGetValue("padding-right", out string pright))
                style.PaddingRightStr = pright;
            if (props.TryGetValue("padding-bottom", out string pbottom))
                style.PaddingBottomStr = pbottom;
            if (props.TryGetValue("padding-left", out string pleft))
                style.PaddingLeftStr = pleft;
            if (props.TryGetValue("margin", out string margin))
            {
                style.MarginStr = margin;
            }
            if (props.TryGetValue("gap", out string gap))
                style.GapStr = gap;
            if (props.TryGetValue("text-align", out string ta))
                style.TextAlign = ta;
            if (props.TryGetValue("white-space", out string ws))
                style.WhiteSpace = ws;
            if (props.TryGetValue("text-transform", out string tt))
                style.TextTransform = tt;
            if (props.TryGetValue("border", out string border))
            {
                var parts = border.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    style.BorderWidthStr = parts[0];
                    style.BorderStyle = parts[1];
                    string colorStr = string.Join(" ", parts.Skip(2));
                    style.BorderColor = ParseColor(colorStr);
                }
            }
            if (props.TryGetValue("border-width", out string bw))
                style.BorderWidthStr = bw;
            if (props.TryGetValue("border-style", out string bs))
                style.BorderStyle = bs;
            if (props.TryGetValue("border-color", out string bc))
                style.BorderColor = ParseColor(bc);
            if (props.TryGetValue("border-top", out string btop))
            {
                var parts = btop.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    style.BorderTopWidthStr = parts[0];
                    style.BorderTopStyle = parts[1];
                    string colorStr = string.Join(" ", parts.Skip(2));
                    style.BorderTopColor = ParseColor(colorStr);
                }
            }
            if (props.TryGetValue("border-right", out string bright))
            {
                var parts = bright.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    style.BorderRightWidthStr = parts[0];
                    style.BorderRightStyle = parts[1];
                    string colorStr = string.Join(" ", parts.Skip(2));
                    style.BorderRightColor = ParseColor(colorStr);
                }
            }
            if (props.TryGetValue("border-bottom", out string bbottom))
            {
                var parts = bbottom.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    style.BorderBottomWidthStr = parts[0];
                    style.BorderBottomStyle = parts[1];
                    string colorStr = string.Join(" ", parts.Skip(2));
                    style.BorderBottomColor = ParseColor(colorStr);
                }
            }
            if (props.TryGetValue("border-left", out string bleft))
            {
                var parts = bleft.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    style.BorderLeftWidthStr = parts[0];
                    style.BorderLeftStyle = parts[1];
                    string colorStr = string.Join(" ", parts.Skip(2));
                    style.BorderLeftColor = ParseColor(colorStr);
                }
            }
            if (props.TryGetValue("box-sizing", out string boxs))
                style.BoxSizing = boxs;
        }
        private Vector4 ParseColor(string color)
        {
            if (string.IsNullOrEmpty(color)) return Vector4.Zero;
            color = color.Trim();
            if (color.Contains("gradient"))
            {
                List<Vector4> colors = new List<Vector4>();
                var matches = Regex.Matches(color, @"#[0-9a-fA-F]{3,6}|rgb[a]?\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*(,\s*[0-1](\.\d+)?\s*)?\)?");
                foreach (Match m in matches)
                {
                    var c = ParseSingleColor(m.Value);
                    if (c != Vector4.Zero) colors.Add(c);
                }
                if (colors.Count > 0)
                {
                    Vector4 avg = Vector4.Zero;
                    foreach (var c in colors) avg += c;
                    avg /= colors.Count;
                    return avg;
                }
                return Vector4.Zero;
            }
            return ParseSingleColor(color);
        }
        private Vector4 ParseSingleColor(string color)
        {
            color = color.Trim();
            if (color.StartsWith("#"))
            {
                color = color.Substring(1);
                if (color.Length == 3)
                {
                    color = "" + color[0] + color[0] + color[1] + color[1] + color[2] + color[2];
                }
                if (color.Length == 6)
                {
                    int r = int.Parse(color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    int g = int.Parse(color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    int b = int.Parse(color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    return new Vector4(r / 255f, g / 255f, b / 255f, 1f);
                }
            }
            else if (color.StartsWith("rgba"))
            {
                string inner = color.Substring(5, color.Length - 6).Trim();
                string[] parts = inner.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length == 4)
                {
                    float r = float.Parse(parts[0]) / 255f;
                    float g = float.Parse(parts[1]) / 255f;
                    float b = float.Parse(parts[2]) / 255f;
                    float a = float.Parse(parts[3]);
                    return new Vector4(r, g, b, a);
                }
            }
            else if (color.StartsWith("rgb"))
            {
                string inner = color.Substring(4, color.Length - 5).Trim();
                string[] parts = inner.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length == 3)
                {
                    float r = float.Parse(parts[0]) / 255f;
                    float g = float.Parse(parts[1]) / 255f;
                    float b = float.Parse(parts[2]) / 255f;
                    return new Vector4(r, g, b, 1f);
                }
            }
            return Vector4.Zero;
        }
    }
}