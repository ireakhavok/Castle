// SiegeEngine.UI/CssParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.UI
{
    public class CssParser
    {
        public void Apply(string css, HtmlElement root)
        {
            // Simple parser: split by { }
            int i = 0;
            while (i < css.Length)
            {
                SkipWhitespace(css, ref i);
                string selector = ReadUntil(css, ref i, '{').Trim();
                i++; // skip {
                string block = ReadUntil(css, ref i, '}').Trim();
                i++; // skip }
                Dictionary<string, string> props = ParseProperties(block);
                ApplyToElements(root, selector, props);
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
        private void ApplyToElements(HtmlElement root, string selector, Dictionary<string, string> props)
        {
            bool isId = selector.StartsWith('#');
            bool isClass = selector.StartsWith('.');
            string name = isId ? selector.Substring(1) : isClass ? selector.Substring(1) : selector;
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                HtmlElement elem = queue.Dequeue();
                bool match = false;
                if (isId)
                {
                    match = elem.Attributes.GetValueOrDefault("id", "") == name;
                }
                else if (isClass)
                {
                    string classes = elem.Attributes.GetValueOrDefault("class", "");
                    match = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name);
                }
                else
                {
                    match = elem.Tag.ToLower() == name.ToLower();
                }
                if (match)
                {
                    ApplyProperties(elem.Style, props);
                }
                foreach (var child in elem.Children)
                {
                    queue.Enqueue(child);
                }
            }
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
            if (props.TryGetValue("background", out string bg) || props.TryGetValue("background-color", out bg))
            {
                style.Background = bg;
                style.BackgroundColor = ParseColor(bg);
            }
            if (props.TryGetValue("color", out string color))
            {
                style.Color = color;
                style.TextColor = ParseColor(color);
            }
            if (props.TryGetValue("font-size", out string fs))
            {
                style.FontSizeStr = fs;
                style.FontSize = ParseSize(fs, 0); // For font, no parent
            }
            if (props.TryGetValue("display", out string disp))
                style.Display = disp;
            if (props.TryGetValue("flex-direction", out string fd))
                style.FlexDirection = fd;
            if (props.TryGetValue("align-items", out string ai))
                style.AlignItems = ai;
            if (props.TryGetValue("justify-content", out string jc))
                style.JustifyContent = jc;
            if (props.TryGetValue("padding", out string pad))
            {
                style.PaddingStr = pad;
            }
            if (props.TryGetValue("text-align", out string ta))
                style.TextAlign = ta;
            // Ignore overflow, etc.
        }
        private Vector4 ParseColor(string color)
        {
            if (string.IsNullOrEmpty(color)) return Vector4.Zero;
            color = color.Trim();
            if (color.Contains("gradient"))
            {
                // Extract first color
                int hashIndex = color.IndexOf('#');
                string firstColor = "";
                if (hashIndex != -1)
                {
                    firstColor = color.Substring(hashIndex, Math.Min(7, color.Length - hashIndex));
                }
                else
                {
                    int rgbIndex = color.IndexOf("rgb", StringComparison.OrdinalIgnoreCase);
                    if (rgbIndex != -1)
                    {
                        string sub = color.Substring(rgbIndex);
                        int end = sub.IndexOf(')');
                        if (end != -1)
                        {
                            firstColor = sub.Substring(0, end + 1);
                        }
                    }
                }
                if (!string.IsNullOrEmpty(firstColor))
                {
                    return ParseColor(firstColor);
                }
                return Vector4.Zero;
            }
            if (color.StartsWith("#"))
            {
                color = color.Substring(1);
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
        private float ParseSize(string s, float parent)
        {
            if (string.IsNullOrEmpty(s) || s == "auto") return float.NaN;
            s = s.Trim();
            if (s.EndsWith("%"))
            {
                return float.Parse(s.Replace("%", "")) / 100 * parent;
            }
            else if (s.EndsWith("vh"))
            {
                // vh relative to viewport height
                return float.Parse(s.Replace("vh", "")) / 100 * 1080; // Assume height, replace with actual
            }
            else if (s.EndsWith("vw"))
            {
                return float.Parse(s.Replace("vw", "")) / 100 * 1920; // Assume width
            }
            else if (s.EndsWith("px"))
            {
                return float.Parse(s.Replace("px", ""));
            }
            else
            {
                return float.Parse(s);
            }
        }
    }
}