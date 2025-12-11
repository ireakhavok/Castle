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
        public static string DefaultUserAgentCss = @"
* {
    box-sizing: border-box;
}
body {
    color: black;
    background-color: white;
    margin: 8px;
    display: block;
}
div {
    display: block;
}
a {
    color: blue;
    text-decoration: underline;
    cursor: pointer;
}
button {
    display: inline-block;
    padding: 1px 6px;
    min-height: 20px;
    min-width: 50px;
    border: 2px outset buttonface;
    border-radius: 2px;
    background-color: buttonface;
    color: buttontext;
    text-align: center;
    cursor: default;
}
select {
    display: inline-block;
    padding: 1px 6px;
    min-height: 20px;
    border: 2px inset buttonface;
    background-color: rgba(51, 51, 51, 0.8);
    color: windowtext;
    overflow: hidden;
    position: relative;
}
input {
    display: inline-block;
}
input[type=""text""] {
    padding: 1px 2px;
    min-height: 20px;
    border: 2px inset;
}
input[type=""checkbox""] {
    width: 13px;
    height: 13px;
    margin: 3px 3px 3px 4px;
    border: 2px inset;
    background-color: window;
}
input[type=""radio""] {
    width: 13px;
    height: 13px;
    margin: 3px 3px 3px 4px;
    border: 2px inset;
    background-color: window;
}
label {
    display: inline;
    cursor: default;
}
option {
    display: block;
    padding: 2px 5px;
    background-color: inherit;
}
option[selected] {
    background-color: rgba(0, 128, 128, 0.8);
}
option:hover {
    background-color: rgba(77, 77, 77, 0.8);
}
table {
    display: table;
    border-collapse: separate;
    border-spacing: 2px;
}
thead, tbody, tfoot {
    display: table-row-group;
}
tr {
    display: table-row;
}
th, td {
    display: table-cell;
    padding: 1px;
}
th {
    font-weight: bold;
    text-align: center;
}
ul, ol {
    display: block;
    list-style-type: disc;
    margin: 1em 0;
    padding-left: 40px;
}
ol {
    list-style-type: decimal;
}
li {
    display: list-item;
}
nav {
    display: block;
}
";
        private List<(string Selector, Dictionary<string, string> Props)> _allRules = new List<(string, Dictionary<string, string>)>();
        public void Apply(string css)
        {
            int i = 0;
            while (i < css.Length)
            {
                SkipWhitespaceAndComments(css, ref i);
                string selector = ReadUntil(css, ref i, '{').Trim();
                i++; // skip {
                string block = ReadUntil(css, ref i, '}').Trim();
                i++; // skip }
                Dictionary<string, string> props = ParseProperties(block);
                var selectors = selector.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                foreach (var s in selectors)
                {
                    _allRules.Add((s, props));
                }
            }
        }
        public void ApplyAll(HtmlElement root)
        {
            ApplyInlineStyles(root);
            foreach (var rule in _allRules)
            {
                string selector = rule.Selector;
                string pseudo = null;
                if (!selector.Contains(" ") && !selector.Contains("~") && selector.Contains(":"))
                {
                    var parts = selector.Split(new char[] { ':' }, 2);
                    selector = parts[0].Trim();
                    pseudo = parts[1].Trim();
                }
                if (pseudo != null && (pseudo == "hover" || pseudo == "active" || pseudo == "target" || pseudo == "checked" || pseudo == "focus"))
                {
                    ApplyToElements(root, selector, rule.Props, pseudo);
                }
                else
                {
                    ApplyToElements(root, selector, rule.Props, null);
                }
            }
        }
        public void ApplyInlineStyles(HtmlElement root)
        {
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                if (elem.Attributes.TryGetValue("style", out string inline) && !string.IsNullOrEmpty(inline))
                {
                    ApplyInline(inline, elem.Style);
                }
                foreach (var child in elem.Children)
                {
                    queue.Enqueue(child);
                }
            }
        }
        private void SkipWhitespaceAndComments(string css, ref int i)
        {
            while (i < css.Length)
            {
                if (char.IsWhiteSpace(css[i]))
                {
                    i++;
                    continue;
                }
                if (i + 1 < css.Length && css[i] == '/' && css[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < css.Length && !(css[i] == '*' && css[i + 1] == '/'))
                    {
                        i++;
                    }
                    if (i + 1 < css.Length) i += 2;
                    continue;
                }
                break;
            }
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
                SkipWhitespaceAndComments(block, ref j);
                string key = ReadUntil(block, ref j, ':').Trim();
                j++; // skip :
                SkipWhitespaceAndComments(block, ref j);
                string value = ReadUntil(block, ref j, ';').Trim();
                if (j < block.Length && block[j] == ';') j++;
                props[key] = value;
            }
            return props;
        }
        public void ApplyInline(string inline, CssStyle style)
        {
            var props = ParseProperties(inline);
            ApplyProperties(style, props);
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
        public bool Matches(HtmlElement elem, string selector)
        {
            if (string.IsNullOrEmpty(selector)) return true;
            selector = selector.Trim();
            if (selector.Contains("~"))
            {
                var parts = selector.Split(new[] { '~' }, StringSplitOptions.TrimEntries);
                if (parts.Length != 2) return false;
                string leftSelector = parts[0].Trim();
                string rightSelector = parts[1].Trim();
                if (!SimpleMatches(elem, rightSelector)) return false;
                if (elem.Parent == null) return false;
                var siblings = elem.Parent.Children;
                int idx = siblings.IndexOf(elem);
                for (int k = idx - 1; k >= 0; k--)
                {
                    if (SimpleMatches(siblings[k], leftSelector))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                var parts = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToArray();
                HtmlElement current = elem;
                for (int k = parts.Length - 1; k >= 0; k--)
                {
                    string part = parts[k];
                    bool found = false;
                    if (k == parts.Length - 1)
                    {
                        if (SimpleMatches(current, part))
                        {
                            found = true;
                        }
                    }
                    else
                    {
                        while (current != null)
                        {
                            if (SimpleMatches(current, part))
                            {
                                found = true;
                                break;
                            }
                            current = current.Parent;
                        }
                    }
                    if (!found) return false;
                    current = current?.Parent;
                }
                return true;
            }
        }
        private bool SimpleMatches(HtmlElement elem, string simple)
        {
            if (string.IsNullOrEmpty(simple)) return true;
            simple = simple.Trim();
            string pseudo = null;
            if (simple.Contains(":"))
            {
                var p = simple.Split(new char[] { ':' }, 2);
                simple = p[0];
                pseudo = p[1];
            }
            bool match = false;
            if (simple.Contains("["))
            {
                int bracket = simple.IndexOf('[');
                string tag = simple.Substring(0, bracket).Trim();
                if (!string.IsNullOrEmpty(tag) && !string.Equals(elem.Tag, tag, StringComparison.OrdinalIgnoreCase)) return false;
                string attrStr = simple.Substring(bracket + 1, simple.Length - bracket - 2).Trim();
                bool attrMatch = false;
                if (attrStr.Contains("="))
                {
                    var parts = attrStr.Split(new char[] { '=' }, 2);
                    string attr = parts[0].Trim();
                    string val = parts[1].Trim().Trim('"', '\'');
                    attrMatch = elem.Attributes.TryGetValue(attr, out string eVal) && eVal == val;
                }
                else
                {
                    string attr = attrStr.Trim();
                    attrMatch = elem.Attributes.ContainsKey(attr);
                }
                match = attrMatch;
            }
            else if (simple == "*")
            {
                match = true;
            }
            else if (simple.StartsWith("#"))
            {
                string id = simple.Substring(1);
                match = elem.Attributes.GetValueOrDefault("id", "") == id;
            }
            else if (simple.StartsWith("."))
            {
                string cls = simple.Substring(1);
                string classes = elem.Attributes.GetValueOrDefault("class", "");
                match = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(cls);
            }
            else
            {
                match = string.Equals(elem.Tag, simple, StringComparison.OrdinalIgnoreCase);
            }
            if (match && pseudo != null)
            {
                match = CheckPseudo(elem, pseudo);
            }
            return match;
        }
        private bool CheckPseudo(HtmlElement elem, string pseudo)
        {
            switch (pseudo)
            {
                case "hover":
                    return elem.IsHover;
                case "active":
                    return elem.IsActive;
                case "checked":
                    return elem.Checked;
                case "target":
                    return elem.IsTarget;
                case "focus":
                    return elem.IsFocused;
                default:
                    return false;
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
            if (props.TryGetValue("right", out string right))
                style.RightStr = right;
            if (props.TryGetValue("bottom", out string bottom))
                style.BottomStr = bottom;
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
                if (bg != "inherit")
                    style.BackgroundColor = ParseColor(bg);
            }
            if (props.TryGetValue("background-image", out string bgImg))
            {
                var urlMatch = Regex.Match(bgImg, @"url\(['""]?(.*?)['""]?\)");
                if (urlMatch.Success)
                {
                    style.BackgroundImage = urlMatch.Groups[1].Value;
                }
            }
            if (props.TryGetValue("color", out string textColorStr))
            {
                style.Color = textColorStr;
                if (textColorStr != "inherit")
                    style.TextColor = ParseColor(textColorStr);
            }
            if (props.TryGetValue("font-size", out string fs))
            {
                style.FontSizeStr = fs;
            }
            if (props.TryGetValue("font-family", out string ff))
                style.FontFamily = ff.Trim('\'', '"');
            if (props.TryGetValue("font-weight", out string fw))
                style.FontWeight = fw;
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
            if (props.TryGetValue("border-radius", out string bradius))
                style.BorderRadiusStr = bradius;
            if (props.TryGetValue("border-collapse", out string bcollapse))
                style.BorderCollapse = bcollapse;
            if (props.TryGetValue("border-spacing", out string bspacing))
                style.BorderSpacing = bspacing;
            if (props.TryGetValue("list-style-type", out string lstype))
                style.ListStyleType = lstype;
            if (props.TryGetValue("box-sizing", out string boxs))
                style.BoxSizing = boxs;
            if (props.TryGetValue("transform", out string tr))
                style.Transform = tr;
            if (props.TryGetValue("overflow", out string ov))
                style.Overflow = ov;
        }
        public Vector4 ParseColor(string color)
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
            color = color.Trim().ToLower();
            var namedColors = new Dictionary<string, Vector4>
            {
                { "black", new Vector4(0f, 0f, 0f, 1f) },
                { "white", new Vector4(1f, 1f, 1f, 1f) },
                { "red", new Vector4(1f, 0f, 0f, 1f) },
                { "lime", new Vector4(0f, 1f, 0f, 1f) },
                { "blue", new Vector4(0f, 0f, 1f, 1f) },
                { "yellow", new Vector4(1f, 1f, 0f, 1f) },
                { "cyan", new Vector4(0f, 1f, 1f, 1f) },
                { "magenta", new Vector4(1f, 0f, 1f, 1f) },
                { "silver", new Vector4(0.75f, 0.75f, 0.75f, 1f) },
                { "gray", new Vector4(0.5f, 0.5f, 0.5f, 1f) },
                { "maroon", new Vector4(0.5f, 0f, 0f, 1f) },
                { "olive", new Vector4(0.5f, 0.5f, 0f, 1f) },
                { "green", new Vector4(0f, 0.5f, 0f, 1f) },
                { "purple", new Vector4(0.5f, 0f, 0.5f, 1f) },
                { "teal", new Vector4(0f, 0.5f, 0.5f, 1f) },
                { "navy", new Vector4(0f, 0f, 0.5f, 1f) },
                { "transparent", new Vector4(0f, 0f, 0f, 0f) },
                { "buttonface", new Vector4(0.867f, 0.867f, 0.867f, 1f) },
                { "buttontext", new Vector4(0f, 0f, 0f, 1f) },
                { "window", new Vector4(1f, 1f, 1f, 1f) },
                { "windowtext", new Vector4(0f, 0f, 0f, 1f) }
            };
            if (namedColors.TryGetValue(color, out var col))
            {
                return col;
            }
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