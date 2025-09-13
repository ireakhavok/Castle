// Folder: SiegeEngine.UI
// File: HtmlParser.cs
using System;
using System.Collections.Generic;
using System.IO;

namespace SiegeEngine.UI
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
                    else if (_index + 2 < _html.Length && _html.Substring(_index, 3) == "!--")
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
                        if (lowerTag == "button")
                        {
                            elem = new ButtonElement();
                        }
                        else if (lowerTag == "div")
                        {
                            elem = new DivElement();
                        }
                        else if (lowerTag == "select")
                        {
                            elem = new SelectElement();
                        }
                        else if (lowerTag == "input")
                        {
                            elem = new InputElement();
                        }
                        else
                        {
                            elem = new HtmlElement { Tag = tag };
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
                        }
                        _index++; // skip '>'
                        bool isSelfClosing = tag.EndsWith("/") || Array.Exists(new string[] { "br", "hr", "img", "input", "meta", "link" }, t => t == lowerTag);

                        if (lowerTag == "include" && elem.Attributes.TryGetValue("src", out string src))
                        {
                            // Handle include
                            string incHtml = File.ReadAllText(src);
                            HtmlParser incParser = new HtmlParser();
                            HtmlElement incRoot = incParser.Parse(incHtml);
                            foreach (var child in incRoot.Children)
                            {
                                parent.Children.Add(child);
                                child.Parent = parent;
                            }
                        }
                        else
                        {
                            parent.Children.Add(elem);
                            if (!isSelfClosing)
                            {
                                // Parse children recursively
                                ParseChildren(elem);
                            }
                        }
                    }
                }
                else
                {
                    // Text node
                    string text = ReadUntil(c => c == '<');
                    text = text.Trim();
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