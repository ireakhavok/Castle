// SiegeEngine/Managers/HtmlMenuParser.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SiegeEngine.Rendering.Definitions;
using System.Numerics;

namespace SiegeEngine.HtmlParsing
{
    public class HtmlMenuParser
    {
        public MenuDefinition ParseMenu(string html, string name)
        {
            html = Regex.Replace(html, @"\r?\n|\r", " ");
            var bodyRegex = new Regex(@"<body\s+([^>]*)>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Match bodyMatch = bodyRegex.Match(html);
            if (!bodyMatch.Success) return null;

            string attrStr = bodyMatch.Groups[1].Value;
            string content = bodyMatch.Groups[2].Value;
            var attrs = ParseAttributes(attrStr);
            string positioningMode = attrs.GetValueOrDefault("data-positioning-mode", "Absolute");
            string styleStr = attrs.GetValueOrDefault("style", "");
            var styleDict = ParseStyle(styleStr);
            string background = styleDict.GetValueOrDefault("background-image", "");
            if (background.StartsWith("url("))
            {
                background = background.Substring(4, background.Length - 5).Trim('\'', '"');
            }

            var menu = new MenuDefinition { Name = name, PositioningMode = positioningMode, Background = background };

            var navRegex = new Regex(@"<nav\s+data-role=""tab-selector""[^>]*>(.*?)</nav>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Match navMatch = navRegex.Match(content);
            var tabInfos = new Dictionary<string, (int iconIndex, string action)>();
            if (navMatch.Success)
            {
                string navContent = navMatch.Groups[1].Value;
                var buttonRegex = new Regex(@"<button\s+([^>]*)>([^<]*)</button>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match bm in buttonRegex.Matches(navContent))
                {
                    string bAttrStr = bm.Groups[1].Value;
                    string bText = bm.Groups[2].Value.Trim();
                    var bAttrs = ParseAttributes(bAttrStr);
                    string tabName = bAttrs.GetValueOrDefault("data-tab", "");
                    int iconIndex = bAttrs.ContainsKey("data-icon") ? int.Parse(bAttrs["data-icon"]) : 0;
                    string action = "SwitchTab_" + tabName;
                    tabInfos[tabName] = (iconIndex, action);
                }
            }

            var divRegex = new Regex(@"<div\s+data-tab-content=""([^""]*)""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            menu.Tabs = new List<TabDefinition>();
            foreach (Match dm in divRegex.Matches(content))
            {
                string tabName = dm.Groups[1].Value;
                string tabContent = dm.Groups[2].Value;
                if (tabInfos.TryGetValue(tabName, out var info))
                {
                    var tab = new TabDefinition { Name = tabName, IconIndex = info.iconIndex, Action = info.action };
                    (tab.Buttons, tab.Elements) = ParseTabElements(tabContent);
                    menu.Tabs.Add(tab);
                }
            }

            menu.Buttons = GetCommonButtons(content, navMatch, divRegex);
            menu.Elements = GetCommonElements(content, navMatch, divRegex);

            return menu;
        }

        private (List<ButtonDefinition>, List<Dictionary<string, object>>) ParseTabElements(string content)
        {
            var buttons = new List<ButtonDefinition>();
            var elements = new List<Dictionary<string, object>>();

            var buttonRegex = new Regex(@"<button\s+([^>]*)>([^<]*)</button>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in buttonRegex.Matches(content))
            {
                buttons.Add(ParseButton(m.Groups[1].Value, m.Groups[2].Value.Trim()));
            }

            var selectRegex = new Regex(@"<select\s+([^>]*)>(.*?)</select>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in selectRegex.Matches(content))
            {
                elements.Add(ParseDropdown(m.Groups[1].Value, m.Groups[2].Value));
            }

            var inputRegex = new Regex(@"<input\s+([^>]*)/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in inputRegex.Matches(content))
            {
                var dict = ParseToggle(m.Groups[1].Value);
                if (dict != null) elements.Add(dict);
            }

            var labelRegex = new Regex(@"<label\s+([^>]*)>([^<]*)</label>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in labelRegex.Matches(content))
            {
                elements.Add(ParseLabel(m.Groups[1].Value, m.Groups[2].Value.Trim()));
            }

            return (buttons, elements);
        }

        private List<ButtonDefinition> GetCommonButtons(string content, Match navMatch, Regex divRegex)
        {
            var buttons = new List<ButtonDefinition>();
            var buttonRegex = new Regex(@"<button\s+([^>]*)>([^<]*)</button>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in buttonRegex.Matches(content))
            {
                if (IsCommon(m, navMatch, divRegex, content))
                {
                    buttons.Add(ParseButton(m.Groups[1].Value, m.Groups[2].Value.Trim()));
                }
            }
            return buttons;
        }

        private List<Dictionary<string, object>> GetCommonElements(string content, Match navMatch, Regex divRegex)
        {
            var elements = new List<Dictionary<string, object>>();

            var selectRegex = new Regex(@"<select\s+([^>]*)>(.*?)</select>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in selectRegex.Matches(content))
            {
                if (IsCommon(m, navMatch, divRegex, content))
                {
                    elements.Add(ParseDropdown(m.Groups[1].Value, m.Groups[2].Value));
                }
            }

            var inputRegex = new Regex(@"<input\s+([^>]*)/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in inputRegex.Matches(content))
            {
                if (IsCommon(m, navMatch, divRegex, content))
                {
                    var dict = ParseToggle(m.Groups[1].Value);
                    if (dict != null) elements.Add(dict);
                }
            }

            var labelRegex = new Regex(@"<label\s+([^>]*)>([^<]*)</label>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in labelRegex.Matches(content))
            {
                if (IsCommon(m, navMatch, divRegex, content))
                {
                    elements.Add(ParseLabel(m.Groups[1].Value, m.Groups[2].Value.Trim()));
                }
            }

            return elements;
        }

        private bool IsCommon(Match m, Match navMatch, Regex divRegex, string content)
        {
            int index = m.Index;
            if (navMatch.Success && index > navMatch.Index && index < navMatch.Index + navMatch.Length) return false;
            foreach (Match dm in divRegex.Matches(content))
            {
                if (index > dm.Index && index < dm.Index + dm.Length) return false;
            }
            return true;
        }

        private ButtonDefinition ParseButton(string attrStr, string text)
        {
            var attrs = ParseAttributes(attrStr);
            string action = attrs.GetValueOrDefault("data-action", "");
            int iconIndex = attrs.ContainsKey("data-icon") ? int.Parse(attrs["data-icon"]) : 0;
            string styleStr = attrs.GetValueOrDefault("style", "");
            var styleDict = ParseStyle(styleStr);
            float x = ParseValue(styleDict.GetValueOrDefault("left", "0"));
            float y = ParseValue(styleDict.GetValueOrDefault("top", "0"));
            float w = ParseValue(styleDict.GetValueOrDefault("width", "300"));
            float h = ParseValue(styleDict.GetValueOrDefault("height", "60"));
            Position pos = new Position { X = x, Y = y };
            Size size = new Size { Width = (int)w, Height = (int)h };
            string bgStr = styleDict.GetValueOrDefault("background-color", "");
            Color bgColor = ParseColor(bgStr);
            string hoverStr = attrs.GetValueOrDefault("data-hover-background", bgStr);
            Color hoverColor = ParseColor(hoverStr);
            string borderStr = styleDict.GetValueOrDefault("border-color", "");
            Color borderColor = ParseColor(borderStr);
            float fontSize = ParseValue(styleDict.GetValueOrDefault("font-size", "10"));
            string textColorStr = styleDict.GetValueOrDefault("color", "");
            Color textColor = ParseColor(textColorStr);
            TextStyle textStyle = new TextStyle { FontSize = fontSize, Color = textColor };
            ButtonStyle buttonStyle = new ButtonStyle { BackgroundColor = bgColor, HoverColor = hoverColor, BorderColor = borderColor };
            return new ButtonDefinition
            {
                Text = text,
                Position = pos,
                Size = size,
                IconIndex = iconIndex,
                Action = action,
                TextStyle = textStyle,
                ButtonStyle = buttonStyle
            };
        }

        private Dictionary<string, object> ParseDropdown(string attrStr, string inner)
        {
            var attrs = ParseAttributes(attrStr);
            string name = attrs.GetValueOrDefault("name", "dropdown");
            string action = attrs.GetValueOrDefault("data-action", "");
            bool isOptionsBelow = attrs.GetValueOrDefault("data-is-options-below", "false") == "true";
            var optionRegex = new Regex(@"<option>([^<]*)</option>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            List<string> options = new List<string>();
            foreach (Match om in optionRegex.Matches(inner))
            {
                options.Add(om.Groups[1].Value.Trim());
            }
            int selectedIndex = 0;
            string styleStr = attrs.GetValueOrDefault("style", "");
            var styleDict = ParseStyle(styleStr);
            float x = ParseValue(styleDict.GetValueOrDefault("left", "0"));
            float y = ParseValue(styleDict.GetValueOrDefault("top", "0"));
            float w = ParseValue(styleDict.GetValueOrDefault("width", "300"));
            float h = ParseValue(styleDict.GetValueOrDefault("height", "60"));
            Position pos = new Position { X = x, Y = y };
            Size size = new Size { Width = (int)w, Height = (int)h };
            string bgStr = styleDict.GetValueOrDefault("background-color", "");
            Color bgColor = ParseColor(bgStr);
            string hoverStr = attrs.GetValueOrDefault("data-hover-background", bgStr);
            Color hoverColor = ParseColor(hoverStr);
            string borderStr = styleDict.GetValueOrDefault("border-color", "");
            Color borderColor = ParseColor(borderStr);
            float fontSize = ParseValue(styleDict.GetValueOrDefault("font-size", "10"));
            string textColorStr = styleDict.GetValueOrDefault("color", "");
            Color textColor = ParseColor(textColorStr);
            TextStyle textStyle = new TextStyle { FontSize = fontSize, Color = textColor };
            ButtonStyle buttonStyle = new ButtonStyle { BackgroundColor = bgColor, HoverColor = hoverColor, BorderColor = borderColor };
            var elementDict = new Dictionary<string, object>
            {
                { "type", "dropdown" },
                { "name", name },
                { "position", pos },
                { "size", size },
                { "options", options },
                { "selectedIndex", selectedIndex },
                { "action", action },
                { "isOptionsBelow", isOptionsBelow },
                { "textStyle", textStyle },
                { "buttonStyle", buttonStyle }
            };
            return elementDict;
        }

        private Dictionary<string, object> ParseToggle(string attrStr)
        {
            var attrs = ParseAttributes(attrStr);
            if (attrs.GetValueOrDefault("type", "").ToLower() != "checkbox") return null;
            string name = attrs.GetValueOrDefault("name", "toggle");
            bool state = attrs.ContainsKey("checked");
            string action = attrs.GetValueOrDefault("data-action", "");
            string styleStr = attrs.GetValueOrDefault("style", "");
            var styleDict = ParseStyle(styleStr);
            float x = ParseValue(styleDict.GetValueOrDefault("left", "0"));
            float y = ParseValue(styleDict.GetValueOrDefault("top", "0"));
            float w = ParseValue(styleDict.GetValueOrDefault("width", "300"));
            float h = ParseValue(styleDict.GetValueOrDefault("height", "60"));
            Position pos = new Position { X = x, Y = y };
            Size size = new Size { Width = (int)w, Height = (int)h };
            string bgStr = styleDict.GetValueOrDefault("background-color", "");
            Color bgColor = ParseColor(bgStr);
            string hoverStr = attrs.GetValueOrDefault("data-hover-background", bgStr);
            Color hoverColor = ParseColor(hoverStr);
            string borderStr = styleDict.GetValueOrDefault("border-color", "");
            Color borderColor = ParseColor(borderStr);
            float fontSize = ParseValue(styleDict.GetValueOrDefault("font-size", "10"));
            string textColorStr = styleDict.GetValueOrDefault("color", "");
            Color textColor = ParseColor(textColorStr);
            TextStyle textStyle = new TextStyle { FontSize = fontSize, Color = textColor };
            ButtonStyle buttonStyle = new ButtonStyle { BackgroundColor = bgColor, HoverColor = hoverColor, BorderColor = borderColor };
            var elementDict = new Dictionary<string, object>
            {
                { "type", "toggle" },
                { "name", name },
                { "position", pos },
                { "size", size },
                { "state", state },
                { "action", action },
                { "textStyle", textStyle },
                { "buttonStyle", buttonStyle }
            };
            return elementDict;
        }

        private Dictionary<string, object> ParseLabel(string attrStr, string text)
        {
            var attrs = ParseAttributes(attrStr);
            string styleStr = attrs.GetValueOrDefault("style", "");
            var styleDict = ParseStyle(styleStr);
            float x = ParseValue(styleDict.GetValueOrDefault("left", "0"));
            float y = ParseValue(styleDict.GetValueOrDefault("top", "0"));
            Position pos = new Position { X = x, Y = y };
            float fontSize = ParseValue(styleDict.GetValueOrDefault("font-size", "10"));
            string textColorStr = styleDict.GetValueOrDefault("color", "");
            Color textColor = ParseColor(textColorStr);
            TextStyle textStyle = new TextStyle { FontSize = fontSize, Color = textColor };
            var elementDict = new Dictionary<string, object>
            {
                { "type", "label" },
                { "text", text },
                { "position", pos },
                { "textStyle", textStyle }
            };
            return elementDict;
        }

        private Dictionary<string, string> ParseAttributes(string attrString)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(attrString)) return dict;
            var attrRegex = new Regex(@"(\w+[-\w]*)=""([^""]*)""", RegexOptions.IgnoreCase);
            foreach (Match m in attrRegex.Matches(attrString))
            {
                dict[m.Groups[1].Value] = m.Groups[2].Value;
            }
            return dict;
        }

        private Dictionary<string, string> ParseStyle(string style)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(style)) return dict;
            var pairs = style.Split(';', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pairs.Length; i++)
            {
                var kv = pairs[i].Split(':', 2);
                if (kv.Length == 2)
                {
                    dict[kv[0].Trim()] = kv[1].Trim();
                }
            }
            return dict;
        }

        private float ParseValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0f;
            value = value.TrimEnd('p', 'x', '%', ' ').Trim();
            return float.TryParse(value, out float f) ? f : 0f;
        }

        private Color ParseColor(string value)
        {
            if (string.IsNullOrEmpty(value)) return new Color { R = 1, G = 1, B = 1, A = 1 };
            value = value.Trim();
            string inner = value;
            if (value.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
            {
                inner = value.Substring(5, value.Length - 6).Trim();
            }
            else if (value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
            {
                inner = value.Substring(4, value.Length - 5).Trim();
            }
            var parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                float r = float.Parse(parts[0].Trim());
                float g = float.Parse(parts[1].Trim());
                float b = float.Parse(parts[2].Trim());
                float a = parts.Length > 3 ? float.Parse(parts[3].Trim()) : 1f;
                if (r > 1 || g > 1 || b > 1)
                {
                    r /= 255f;
                    g /= 255f;
                    b /= 255f;
                }
                if (a > 1) a /= 255f;
                return new Color { R = r, G = g, B = b, A = a };
            }
            return new Color { R = 1, G = 1, B = 1, A = 1 };
        }
    }
}