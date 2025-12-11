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
                    TextElement textElem = new TextElement { Content = value };
                    textElem.Parent = elem;
                    elem.Children.Add(textElem);
                }
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
                    TextElement textElem = new TextElement { Content = value };
                    textElem.Parent = elem;
                    elem.Children.Add(textElem);
                }
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
                else if (tag == "input")
                {
                    if (elem is InputElement inp)
                    {
                        return inp.Value;
                    }
                    return elem.Attributes.GetValueOrDefault("value", "");
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
                        overlay.TriggerChange(elem);
                    }
                }
                else if (tag == "option")
                {
                    elem.Attributes["value"] = value;
                }
                else if (tag == "input")
                {
                    if (elem is InputElement inp)
                    {
                        string oldValue = inp.Value;
                        inp.Value = value;
                        if (oldValue != value)
                        {
                            overlay.RefreshUI();
                            overlay.TriggerChange(elem);
                        }
                    }
                    else
                    {
                        elem.Attributes["value"] = value;
                    }
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
        public bool @checked
        {
            get { return elem.Checked; }
            set
            {
                bool oldChecked = elem.Checked;
                elem.Checked = value;
                overlay.RefreshUI();
                if (oldChecked != value)
                {
                    overlay.TriggerChange(elem);
                }
            }
        }
        public void appendChild(JSElement child)
        {
            elem.Children.Add(child.elem);
            child.elem.Parent = elem;
            overlay.RefreshUI();
        }
        public void removeChild(JSElement child)
        {
            elem.Children.Remove(child.elem);
            child.elem.Parent = null;
            overlay.RefreshUI();
        }
        public void insertBefore(JSElement newChild, JSElement referenceChild)
        {
            int index = elem.Children.IndexOf(referenceChild.elem);
            if (index != -1)
            {
                elem.Children.Insert(index, newChild.elem);
                newChild.elem.Parent = elem;
                overlay.RefreshUI();
            }
        }
        public void replaceChild(JSElement newChild, JSElement oldChild)
        {
            int index = elem.Children.IndexOf(oldChild.elem);
            if (index != -1)
            {
                elem.Children[index] = newChild.elem;
                newChild.elem.Parent = elem;
                oldChild.elem.Parent = null;
                overlay.RefreshUI();
            }
        }
        public string getAttribute(string name)
        {
            return elem.Attributes.GetValueOrDefault(name, null);
        }
        public void setAttribute(string name, string value)
        {
            elem.Attributes[name] = value;
            overlay.RefreshUI();
        }
        public void removeAttribute(string name)
        {
            elem.Attributes.Remove(name);
            overlay.RefreshUI();
        }
        public JSElement querySelector(string selector)
        {
            var elemFound = QuerySelectorAll(selector).FirstOrDefault();
            return elemFound == null ? null : new JSElement(elemFound, overlay);
        }
        public List<JSElement> querySelectorAll(string selector)
        {
            var elems = QuerySelectorAll(selector);
            return elems.Select(e => new JSElement(e, overlay)).ToList();
        }
        private List<HtmlElement> QuerySelectorAll(string selector)
        {
            List<HtmlElement> matches = new List<HtmlElement>();
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(elem);
            var css = new CssParser();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (css.Matches(current, selector))
                {
                    matches.Add(current);
                }
                foreach (var child in current.Children)
                {
                    queue.Enqueue(child);
                }
            }
            return matches;
        }
        public void addEventListener(string eventName, object callback)
        {
            eventName = eventName.ToLower();
            if (!elem.EventListeners.ContainsKey(eventName)) elem.EventListeners[eventName] = new List<object>();
            elem.EventListeners[eventName].Add(callback);
        }
        public void removeEventListener(string eventName, object callback)
        {
            eventName = eventName.ToLower();
            if (elem.EventListeners.ContainsKey(eventName))
            {
                elem.EventListeners[eventName].Remove(callback);
                if (elem.EventListeners[eventName].Count == 0) elem.EventListeners.Remove(eventName);
            }
        }
    }
}