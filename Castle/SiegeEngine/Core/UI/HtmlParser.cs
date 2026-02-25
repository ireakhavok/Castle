// Folder: SiegeEngine.Core.UI
// File: HtmlParser.cs
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace SiegeEngine.Core.UI
{
    public class HtmlParser
    {
        private string _html;
        private int _index;
        public HtmlElement Parse(string html)
        {
            _html = html;
            _index = 0;
            HtmlElement root = new HtmlElement { Tag = "root" };
            ParseChildren(root);
            return root.Children.Count == 1 ? root.Children[0] : root;
        }
        private void ParseChildren(HtmlElement parent)
        {
            while (_index < _html.Length)
            {
                SkipWhitespace();
                if (_index >= _html.Length) break;
                if (_html[_index] == '<')
                {
                    _index++;
                    if (_index < _html.Length && _html[_index] == '/')
                    {
                        // Closing tag, end of children
                        _index++; // skip '/'
                        string closingTag = ReadUntil(c => c == '>');
                        _index++; // skip '>'
                        return;
                    }
                    else if (_html[_index] == '!' && _index + 2 < _html.Length && _html.Substring(_index, 3) == "!--")
                    {
                        // Comment
                        _index += 3;
                        while (_index + 2 < _html.Length && _html.Substring(_index, 3) != "-->")
                        {
                            _index++;
                        }
                        _index += 3;
                    }
                    else
                    {
                        // Opening tag
                        string tag = ReadUntil(c => char.IsWhiteSpace(c) || c == '>');
                        string lowerTag = tag.ToLower();
                        HtmlElement elem;
                        switch (lowerTag)
                        {
                            case "button":
                                elem = new ButtonElement();
                                break;
                            case "div":
                                elem = new DivElement();
                                break;
                            case "select":
                                elem = new SelectElement();
                                break;
                            case "input":
                                // Minimal targeted detection for type="range" - only this line added
                                string inputType = "text";
                                int typePos = _html.IndexOf("type=\"", _index);
                                if (typePos != -1 && typePos < _html.IndexOf('>', _index))
                                {
                                    int start = typePos + 6;
                                    int end = _html.IndexOf('"', start);
                                    if (end > start)
                                    {
                                        inputType = _html.Substring(start, end - start).ToLower();
                                    }
                                }
                                if (inputType == "range")
                                {
                                    elem = new RangeElement();
                                }
                                else
                                {
                                    elem = new InputElement();
                                }
                                break;
                            case "option":
                                elem = new OptionElement();
                                break;
                            case "table":
                                elem = new TableElement();
                                break;
                            case "tr":
                                elem = new TrElement();
                                break;
                            case "th":
                                elem = new ThElement();
                                break;
                            case "td":
                                elem = new TdElement();
                                break;
                            case "ul":
                                elem = new UlElement();
                                break;
                            case "ol":
                                elem = new OlElement();
                                break;
                            case "li":
                                elem = new LiElement();
                                break;
                            case "nav":
                                elem = new NavElement();
                                break;
                            default:
                                elem = new HtmlElement { Tag = tag };
                                break;
                        }
                        elem.Parent = parent;
                        // Parse attributes
                        while (_index < _html.Length && _html[_index] != '>')
                        {
                            SkipWhitespace();
                            if (_index >= _html.Length || _html[_index] == '>') break;
                            string key = ReadUntil(c => c == '=' || char.IsWhiteSpace(c) || c == '>');
                            key = key.Trim();
                            if (string.IsNullOrEmpty(key)) continue;
                            string value = "";
                            if (_index < _html.Length && _html[_index] == '=')
                            {
                                _index++;
                                SkipWhitespace();
                                char quote = '\0';
                                if (_index < _html.Length && (_html[_index] == '"' || _html[_index] == '\''))
                                {
                                    quote = _html[_index];
                                    _index++;
                                }
                                value = ReadUntil(c => quote != '\0' ? c == quote : char.IsWhiteSpace(c) || c == '>');
                                if (quote != '\0' && _index < _html.Length) _index++; // close quote
                            }
                            elem.Attributes[key] = value;
                            if (key == "data-hook" && elem is ButtonElement btn)
                            {
                                btn.AttachHook(value);
                            }
                            string lowerKey = key.ToLower();
                            if (lowerKey == "onclick")
                            {
                                elem.OnClickJS = value;
                            }
                            else if (lowerKey == "onchange")
                            {
                                elem.OnChangeJS = value;
                            }
                            else if (lowerKey == "onmouseenter" || lowerKey == "onmouseover")
                            {
                                elem.OnMouseEnterJS = value;
                            }
                            else if (lowerKey == "onmouseleave" || lowerKey == "onmouseout")
                            {
                                elem.OnMouseLeaveJS = value;
                            }
                            else if (lowerKey == "onmousedown")
                            {
                                elem.OnMouseDownJS = value;
                            }
                            else if (lowerKey == "onmouseup")
                            {
                                elem.OnMouseUpJS = value;
                            }
                            else if (lowerKey == "onfocus")
                            {
                                elem.OnFocusJS = value;
                            }
                            else if (lowerKey == "onblur")
                            {
                                elem.OnBlurJS = value;
                            }
                        }
                        _index++; // skip '>'
                        bool isSelfClosing = tag.EndsWith("/") || Array.Exists(new string[] { "br", "hr", "img", "input", "meta", "link" }, t => t == lowerTag);
                        parent.Children.Add(elem);
                        if (!isSelfClosing)
                        {
                            // Parse children recursively
                            ParseChildren(elem);
                        }
                        if (lowerTag == "include" && elem.Attributes.TryGetValue("src", out string src))
                        {
                            // Handle include
                            string incHtml = File.ReadAllText(src);
                            HtmlParser incParser = new HtmlParser();
                            HtmlElement incRoot = incParser.Parse(incHtml);
                            parent.Children.Remove(elem);
                            foreach (var child in incRoot.Children)
                            {
                                parent.Children.Add(child);
                                child.Parent = parent;
                            }
                        }
                        else if (lowerTag == "script")
                        {
                            // Script handled in LoadUI
                        }
                    }
                }
                else
                {
                    // Text node
                    string text = ReadUntil(c => c == '<');
                    if (!string.IsNullOrEmpty(text))
                    {
                        TextElement textElem = new TextElement { Content = text };
                        textElem.Parent = parent;
                        parent.Children.Add(textElem);
                    }
                }
            }
        }
        private void SkipWhitespace()
        {
            while (_index < _html.Length && char.IsWhiteSpace(_html[_index]))
            {
                _index++;
            }
        }
        private string ReadUntil(Func<char, bool> condition)
        {
            string result = "";
            while (_index < _html.Length && !condition(_html[_index]))
            {
                result += _html[_index];
                _index++;
            }
            return result;
        }
    }
}