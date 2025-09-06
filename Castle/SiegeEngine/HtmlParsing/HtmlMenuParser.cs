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
            var bodyRegex = new Regex(@"<body\s*([^>]*)>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
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
            // Parse screens as menus, but for now, assume one main, settings as separate
            var screenRegex = new Regex(@"<div\s+id=""([^""]*)""\s+class=""screen""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var screens = new Dictionary<string, string>();
            foreach (Match sm in screenRegex.Matches(content))
            {
                screens[sm.Groups[1].Value] = sm.Groups[2].Value;
            }
            // Parse main screen
            if (screens.TryGetValue("main", out string mainContent))
            {
                ParseMainScreen(mainContent, menu);
            }
            // Parse settings screen as separate menu or tab
            if (screens.TryGetValue("settings", out string settingsContent))
            {
                var settingsMenu = new MenuDefinition { Name = "Settings", PositioningMode = positioningMode, Background = background };
                ParseSettingsScreen(settingsContent, settingsMenu);
                // For now, add as tab or handle in MenuManager
                menu.Tabs.Add(new TabDefinition { Name = "Settings", Elements = settingsMenu.Elements, Buttons = settingsMenu.Buttons });
            }
            return menu;
        }
        private void ParseMainScreen(string content, MenuDefinition menu)
        {
            // Parse sidebar labels as tabs
            var labelRegex = new Regex(@"<label\s+for=""([^""]*)""\s+class=""button""[^>]*>([^<]*)</label>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            menu.Tabs = new List<TabDefinition>();
            foreach (Match lm in labelRegex.Matches(content))
            {
                string tabId = lm.Groups[1].Value;
                string tabText = lm.Groups[2].Value.Trim();
                var tab = new TabDefinition { Name = tabText, Action = "SwitchTab_" + tabId };
                menu.Tabs.Add(tab);
            }
            // Parse content divs
            var contentDivRegex = new Regex(@"<div\s+class=""content\s+([^""]*)""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match cm in contentDivRegex.Matches(content))
            {
                string tabClass = cm.Groups[1].Value;
                string tabContent = cm.Groups[2].Value;
                var tab = menu.Tabs.Find(t => t.Name.ToLower() == tabClass);
                if (tab != null)
                {
                    (tab.Buttons, tab.Elements) = ParseTabElements(tabContent);
                }
            }
            // Parse standalone buttons like Exit
            var buttonRegex = new Regex(@"<div\s+class=""button""[^>]*>([^<]*)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match bm in buttonRegex.Matches(content))
            {
                string bText = bm.Groups[1].Value.Trim();
                menu.Buttons.Add(new ButtonDefinition { Text = bText, Action = bText == "Exit" ? "Exit" : "" });
            }
        }
        private void ParseSettingsScreen(string content, MenuDefinition menu)
        {
            menu.Elements = new List<Dictionary<string, object>>();
            menu.Buttons = new List<ButtonDefinition>();
            // Parse options
            var optionRegex = new Regex(@"<div\s+class=""option""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match om in optionRegex.Matches(content))
            {
                string optionContent = om.Groups[1].Value;
                var labelMatch = Regex.Match(optionContent, @"<label\s+for=""([^""]*)""[^>]*>([^<]*)</label>", RegexOptions.IgnoreCase);
                string labelId = labelMatch.Groups[1].Value;
                string labelText = labelMatch.Groups[2].Value.Trim();
                // Select
                var selectMatch = Regex.Match(optionContent, @"<select\s+id=""([^""]*)""[^>]*>(.*?)</select>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (selectMatch.Success)
                {
                    string selectId = selectMatch.Groups[1].Value;
                    string inner = selectMatch.Groups[2].Value;
                    var element = ParseDropdown($"id=\"{selectId}\" data-action=\"Change{selectId.Replace("-", "")}\"", inner);
                    menu.Elements.Add(element);
                }
            }
            // Parse toggle
            var toggleRegex = new Regex(@"<div\s+class=""toggle""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match tm in toggleRegex.Matches(content))
            {
                string toggleContent = tm.Groups[1].Value;
                var element = ParseToggleContent(toggleContent); // Renamed to avoid ambiguity
                if (element != null)
                {
                    menu.Elements.Add(element);
                }
            }
            // Parse buttons
            var buttonsDivRegex = new Regex(@"<div\s+class=""buttons""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Match buttonsMatch = buttonsDivRegex.Match(content);
            if (buttonsMatch.Success)
            {
                string buttonsContent = buttonsMatch.Groups[1].Value;
                var buttonRegex = new Regex(@"<div\s+class=""button""\s+onclick=""([^""]*)""[^>]*>([^<]*)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match bm in buttonRegex.Matches(buttonsContent))
                {
                    string onclick = bm.Groups[1].Value;
                    string bText = bm.Groups[2].Value.Trim();
                    string action = onclick == "applySettings()" ? "ApplySettings" : "";
                    menu.Buttons.Add(new ButtonDefinition { Text = bText, Action = action });
                }
                var aRegex = new Regex(@"<a\s+href=""([^""]*)""\s+class=""button""[^>]*>([^<]*)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match am in aRegex.Matches(buttonsContent))
                {
                    string href = am.Groups[1].Value;
                    string aText = am.Groups[2].Value.Trim();
                    string action = href == "#main" ? "SwitchMenu_Main" : "";
                    menu.Buttons.Add(new ButtonDefinition { Text = aText, Action = action });
                }
            }
        }
        private (List<ButtonDefinition>, List<Dictionary<string, object>>) ParseTabElements(string content)
        {
            var buttons = new List<ButtonDefinition>();
            var elements = new List<Dictionary<string, object>>();
            var buttonRegex = new Regex(@"<div\s+class=""button""[^>]*>([^<]*)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in buttonRegex.Matches(content))
            {
                string bText = m.Groups[1].Value.Trim();
                buttons.Add(new ButtonDefinition { Text = bText, Action = "" }); // Actions to be set in MenuManager
            }
            var selectRegex = new Regex(@"<select[^>]*>(.*?)</select>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            int selectCount = 0;
            foreach (Match m in selectRegex.Matches(content))
            {
                string inner = m.Groups[1].Value;
                string action = selectCount == 0 ? "ChangeGameType" : "ChangePlayerMode";
                elements.Add(ParseDropdown("", inner));
                elements[^1]["action"] = action;
                selectCount++;
            }
            var toggleRegex = new Regex(@"<div\s+class=""toggle""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in toggleRegex.Matches(content))
            {
                string inner = m.Groups[1].Value;
                var element = ParseToggleContent(inner); // Renamed to avoid ambiguity
                if (element != null)
                {
                    elements.Add(element);
                }
            }
            return (buttons, elements);
        }
        private Dictionary<string, object> ParseToggleContent(string inner)
        {
            var labelMatch = Regex.Match(inner, @"<label\s+for=""([^""]*)""[^>]*>([^<]*)</label>", RegexOptions.IgnoreCase);
            string labelId = labelMatch.Groups[1].Value;
            string labelText = labelMatch.Groups[2].Value.Trim();
            var inputMatch = Regex.Match(inner, @"<input\s+type=""checkbox""\s+id=""([^""]*)""[^>]*>", RegexOptions.IgnoreCase);
            string inputId = inputMatch.Groups[1].Value;
            bool checkedState = inputMatch.Value.Contains("checked");
            string action = "Toggle" + char.ToUpper(inputId[0]) + inputId.Substring(1);
            var elementDict = new Dictionary<string, object>
            {
                { "type", "toggle" },
                { "name", labelText },
                { "state", checkedState },
                { "action", action }
            };
            return elementDict;
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
            string bgStr = styleDict.GetValueOrDefault("background", "");
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