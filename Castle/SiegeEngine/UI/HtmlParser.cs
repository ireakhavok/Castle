using System;
using System.Collections.Generic;
using System.IO;

namespace SiegeEngine.UI
{
    public class HtmlParser
    {
        public HtmlElement Parse(string html)
        {
            Stack<HtmlElement> stack = new Stack<HtmlElement>();
            HtmlElement root = new HtmlElement { Tag = "root" };
            stack.Push(root);
            int i = 0;
            while (i < html.Length)
            {
                if (html[i] == '<')
                {
                    i++;
                    if (i < html.Length && html[i] == '/')
                    {
                        i++;
                        string closingTag = "";
                        while (i < html.Length && html[i] != '>')
                        {
                            closingTag += html[i];
                            i++;
                        }
                        i++; // >
                        closingTag = closingTag.Trim();
                        if (stack.Count > 1 && stack.Peek().Tag.ToLower() == closingTag.ToLower())
                        {
                            stack.Pop();
                        }
                    }
                    else if (i + 2 < html.Length && html.Substring(i, 3) == "!--")
                    {
                        i += 3;
                        while (i + 2 < html.Length && html.Substring(i, 3) != "-->")
                        {
                            i++;
                        }
                        i += 3;
                    }
                    else
                    {
                        string tag = "";
                        while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] != '>')
                        {
                            tag += html[i];
                            i++;
                        }
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
                        else
                        {
                            elem = new HtmlElement { Tag = tag };
                        }
                        while (i < html.Length && html[i] != '>')
                        {
                            while (i < html.Length && char.IsWhiteSpace(html[i])) i++;
                            if (i >= html.Length || html[i] == '>') break;
                            string key = "";
                            while (i < html.Length && html[i] != '=' && !char.IsWhiteSpace(html[i]) && html[i] != '>')
                            {
                                key += html[i];
                                i++;
                            }
                            key = key.Trim();
                            if (string.IsNullOrEmpty(key)) continue;
                            string value = "";
                            if (i < html.Length && html[i] == '=')
                            {
                                i++;
                                while (i < html.Length && char.IsWhiteSpace(html[i])) i++;
                                char quote = '\0';
                                if (i < html.Length && (html[i] == '"' || html[i] == '\''))
                                {
                                    quote = html[i];
                                    i++;
                                }
                                while (i < html.Length && (quote != '\0' ? html[i] != quote : !char.IsWhiteSpace(html[i]) && html[i] != '>'))
                                {
                                    value += html[i];
                                    i++;
                                }
                                if (quote != '\0' && i < html.Length) i++; // close quote
                            }
                            elem.Attributes[key] = value;
                            if (key == "data-hook" && elem is ButtonElement btn)
                            {
                                btn.AttachHook(value);
                            }
                        }
                        i++; // >
                        if (lowerTag == "include" && elem.Attributes.TryGetValue("src", out string src))
                        {
                            // Handle include
                            string incHtml = File.ReadAllText(src);
                            HtmlElement incRoot = Parse(incHtml);
                            foreach (var child in incRoot.Children)
                            {
                                stack.Peek().Children.Add(child);
                                child.Parent = stack.Peek();
                            }
                        }
                        else
                        {
                            stack.Peek().Children.Add(elem);
                            elem.Parent = stack.Peek();
                            string[] selfClosingTags = { "br", "hr", "img", "input", "meta", "link" };
                            if (!Array.Exists(selfClosingTags, t => t == lowerTag) && !tag.EndsWith("/"))
                            {
                                stack.Push(elem);
                            }
                        }
                    }
                }
                else
                {
                    string text = "";
                    while (i < html.Length && html[i] != '<')
                    {
                        text += html[i];
                        i++;
                    }
                    text = text.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        TextElement textElem = new TextElement { Content = text };
                        stack.Peek().Children.Add(textElem);
                        textElem.Parent = stack.Peek();
                    }
                }
            }
            return root.Children.Count == 1 ? root.Children[0] : root;
        }
    }
}