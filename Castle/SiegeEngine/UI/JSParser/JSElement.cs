// Folder: SiegeEngine.UI/JSParser
// File: JSElement.cs
using System;
using System.Collections.Generic;
using System.Linq;
namespace SiegeEngine.UI.JSParser
{
    public class JSElement
    {
        public HtmlElement elem;
        public UIOverlay overlay;
        public JSElement(HtmlElement elem, UIOverlay overlay)
        {
            this.elem = elem;
            this.overlay = overlay;
        }
        public string id
        {
            get { return elem.Attributes.GetValueOrDefault("id", ""); }
        }
        public string tagName
        {
            get { return elem.Tag; }
        }
        public string innerHTML
        {
            get { return string.Join("", elem.Children.OfType<TextElement>().Select(t => t.Content)); }
            set
            {
                Console.WriteLine("Debug: Cleared children for ID: " + id);
                elem.Children.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    // Simple parse, assume text
                    elem.Children.Add(new TextElement { Content = value });
                }
                Console.WriteLine("Debug: RefreshUI from JS");
                overlay.RefreshUI();
            }
        }
        public string textContent
        {
            get { return string.Join("", elem.Children.OfType<TextElement>().Select(t => t.Content)); }
            set
            {
                elem.Children.RemoveAll(c => c is TextElement);
                if (!string.IsNullOrEmpty(value))
                {
                    elem.Children.Add(new TextElement { Content = value });
                }
                Console.WriteLine("Debug: RefreshUI from JS");
                overlay.RefreshUI();
            }
        }
        public string value
        {
            get
            {
                string tag = elem.Tag.ToLower();
                if (tag == "select")
                {
                    var selected = elem.Children.FirstOrDefault(c => c.Attributes.ContainsKey("selected"));
                    return selected?.Attributes.GetValueOrDefault("value", ((TextElement)selected?.Children.FirstOrDefault())?.Content ?? "") ?? "";
                }
                else if (tag == "option")
                {
                    return elem.Attributes.GetValueOrDefault("value", ((TextElement)elem.Children.FirstOrDefault())?.Content ?? "");
                }
                return "";
            }
            set
            {
                string tag = elem.Tag.ToLower();
                if (tag == "select")
                {
                    bool found = false;
                    foreach (var opt in elem.Children.Where(c => c.Tag.ToLower() == "option"))
                    {
                        string optVal = opt.Attributes.GetValueOrDefault("value", ((TextElement)opt.Children.FirstOrDefault())?.Content ?? "");
                        if (optVal == value)
                        {
                            opt.Attributes["selected"] = "";
                            found = true;
                        }
                        else
                        {
                            opt.Attributes.Remove("selected");
                        }
                    }
                    if (found)
                    {
                        overlay.RefreshUI();
                    }
                }
                else if (tag == "option")
                {
                    elem.Attributes["value"] = value;
                    overlay.RefreshUI();
                }
            }
        }
        public object[] options
        {
            get
            {
                if (elem.Tag.ToLower() == "select")
                {
                    List<object> opts = new List<object>();
                    foreach (var opt in elem.Children.Where(c => c.Tag.ToLower() == "option"))
                    {
                        string txt = ((TextElement)opt.Children.FirstOrDefault())?.Content ?? "";
                        string val = opt.Attributes.GetValueOrDefault("value", txt);
                        bool sel = opt.Attributes.ContainsKey("selected");
                        opts.Add(new Dictionary<string, object> { ["text"] = txt, ["value"] = val, ["selected"] = sel });
                    }
                    return opts.ToArray();
                }
                return new object[0];
            }
        }
        public void appendChild(JSElement child)
        {
            elem.Children.Add(child.elem);
            child.elem.Parent = elem;
            Console.WriteLine("Debug: RefreshUI from JS");
            overlay.RefreshUI();
        }
        public void addOption(string text, string value)
        {
            if (elem.Tag.ToLower() == "select")
            {
                var opt = new OptionElement();
                opt.Attributes["value"] = value;
                opt.Children.Add(new TextElement { Content = text });
                opt.Parent = elem;
                elem.Children.Add(opt);
                Console.WriteLine("Debug: Added option " + text);
                Console.WriteLine("Debug: RefreshUI from JS");
                overlay.RefreshUI();
            }
        }
        public void clearOptions()
        {
            if (elem.Tag.ToLower() == "select")
            {
                elem.Children.RemoveAll(c => c.Tag.ToLower() == "option");
                Console.WriteLine("Debug: RefreshUI from JS");
                overlay.RefreshUI();
            }
        }
    }
}