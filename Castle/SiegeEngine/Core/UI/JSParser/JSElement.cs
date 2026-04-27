using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
namespace SiegeEngine.Core.UI.JSParser
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
                elem.Children.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    TextElement textElem = new TextElement { Content = value };
                    textElem.Parent = elem;
                    elem.Children.Add(textElem);
                }
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
                    TextElement textElem = new TextElement { Content = value };
                    textElem.Parent = elem;
                    elem.Children.Add(textElem);
                }
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
                else if (tag == "input")
                {
                    if (elem is RangeElement range)
                    {
                        return range.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    if (elem is InputElement inp)
                    {
                        return inp.Value ?? "";
                    }
                    return elem.Attributes.GetValueOrDefault("value", "");
                }
                return "";
            }
            set
            {
                string tag = elem.Tag.ToLower();
                string newVal = value?.ToString() ?? "";
                bool valueChanged = false;
                if (tag == "input")
                {
                    string oldValue = "";
                    if (elem is RangeElement range)
                    {
                        if (double.TryParse(newVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double f))
                        {
                            oldValue = range.Value.ToString(CultureInfo.InvariantCulture);
                            range.Value = (float)f;
                            valueChanged = oldValue != newVal;
                        }
                        else
                        {
                            range.Value = 0f;
                            valueChanged = true;
                        }
                    }
                    else if (elem is InputElement inp)
                    {
                        oldValue = inp.Value ?? "";
                        if (oldValue != newVal)
                        {
                            inp.Value = newVal;
                            valueChanged = true;
                        }
                    }
                    string oldAttr = elem.Attributes.GetValueOrDefault("value", "");
                    if (oldAttr != newVal)
                    {
                        elem.Attributes["value"] = newVal;
                        valueChanged = true;
                    }
                }
                else if (tag == "select")
                {
                    bool found = false;
                    foreach (var opt in elem.Children.Where(c => c.Tag.ToLower() == "option"))
                    {
                        string optVal = opt.Attributes.GetValueOrDefault("value", ((TextElement)opt.Children.FirstOrDefault())?.Content ?? "");
                        if (optVal == newVal)
                        {
                            opt.Attributes["selected"] = "";
                            found = true;
                        }
                        else
                        {
                            opt.Attributes.Remove("selected");
                        }
                    }
                    if (found) valueChanged = true;
                }
                else if (tag == "option")
                {
                    if (elem.Attributes.GetValueOrDefault("value", "") != newVal)
                    {
                        elem.Attributes["value"] = newVal;
                        valueChanged = true;
                    }
                }
                if (valueChanged)
                {
                    if (tag == "input")
                    {
                        overlay.InvokeListeners(elem, "input");
                        overlay.TriggerChange(elem);
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
        public float min
        {
            get { return elem is RangeElement r ? r.Min : 0f; }
        }
        public float max
        {
            get { return elem is RangeElement r ? r.Max : 100f; }
        }
        // === LIVE STYLE PROXY (FIXED - no RefreshUI on every style change during drag) ===
        public class StyleProxy
        {
            private readonly HtmlElement _elem;
            private readonly UIOverlay _overlay;
            public StyleProxy(HtmlElement elem, UIOverlay overlay)
            {
                _elem = elem;
                this._overlay = overlay;
            }
            public object this[string key]
            {
                get
                {
                    key = key.ToLower();
                    return key switch
                    {
                        "left" => _elem.Style.LeftStr ?? "",
                        "top" => _elem.Style.TopStr ?? "",
                        "width" => _elem.Style.WidthStr ?? "",
                        "height" => _elem.Style.HeightStr ?? "",
                        "display" => _elem.Style.Display ?? "",
                        "position" => _elem.Style.Position ?? "",
                        _ => ""
                    };
                }
                set
                {
                    key = key.ToLower();
                    string val = value?.ToString() ?? "";
                    bool changed = false;
                    switch (key)
                    {
                        case "left":
                            string elemId = _elem.Attributes.GetValueOrDefault("id", "no-id");
                            Console.WriteLine($"[StyleProxy] Setting left on #{elemId} to {val}");
                            if (_elem.Style.LeftStr != val) { _elem.Style.LeftStr = val; changed = true; }
                            break;
                        case "top":
                            if (_elem.Style.TopStr != val) { _elem.Style.TopStr = val; changed = true; }
                            break;
                        case "width":
                            if (_elem.Style.WidthStr != val) { _elem.Style.WidthStr = val; changed = true; }
                            break;
                        case "height":
                            if (_elem.Style.HeightStr != val) { _elem.Style.HeightStr = val; changed = true; }
                            break;
                        case "display":
                            if (_elem.Style.Display != val) { _elem.Style.Display = val; changed = true; }
                            break;
                        case "position":
                            if (_elem.Style.Position != val) { _elem.Style.Position = val; changed = true; }
                            break;
                        default:
                            if (_elem.Attributes.GetValueOrDefault("style-" + key, "") != val)
                            {
                                _elem.Attributes["style-" + key] = val;
                                changed = true;
                            }
                            break;
                    }
                    if (changed)
                    {
                        _elem.MarkIntrinsicDirty();
                        var p = _elem.Parent;
                        while (p != null)
                        {
                            p.MarkIntrinsicDirty();
                            p = p.Parent;
                        }
                        if ((key == "left" || key == "top") &&
                            (_elem.Style.Position == "absolute" || _elem.Style.Position == "fixed"))
                        {
                            HtmlElement cb = _elem.Parent;
                            float refW = cb != null ? cb.ComputedContentWidth : _overlay.PanelWidth;
                            float refH = cb != null ? cb.ComputedContentHeight : _overlay.PanelHeight;
                            float newLeft = key == "left" ? HtmlLayoutUtils.ParseSize(val, refW, _overlay.PanelWidth, _overlay.PanelHeight) : float.NaN;
                            float newTop = key == "top" ? HtmlLayoutUtils.ParseSize(val, refH, _overlay.PanelWidth, _overlay.PanelHeight) : float.NaN;
                            float newX = _elem.ComputedPosition.X;
                            float newY = _elem.ComputedPosition.Y;
                            if (!float.IsNaN(newLeft)) newX = (cb != null ? cb.ComputedContentX : 0) + newLeft;
                            if (!float.IsNaN(newTop)) newY = (cb != null ? cb.ComputedContentY : 0) + newTop;
                            _elem.ComputedPosition = new Vector2(newX, newY);
                            _elem.UpdateFullTransforms(Matrix4x4.Identity);
                        }
                    }
                }
            }
        }
        public StyleProxy style => new StyleProxy(elem, overlay);
        // === CLASSLIST (full) ===
        public class ClassList
        {
            private readonly HtmlElement _elem;
            private readonly UIOverlay _overlay;
            public ClassList(HtmlElement elem, UIOverlay overlay)
            {
                _elem = elem;
                this._overlay = overlay;
            }
            public bool contains(string className)
            {
                if (string.IsNullOrEmpty(className)) return false;
                string classes = _elem.Attributes.GetValueOrDefault("class", "");
                return classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className);
            }
            public void add(string className)
            {
                if (string.IsNullOrEmpty(className)) return;
                string classes = _elem.Attributes.GetValueOrDefault("class", "");
                var list = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!list.Contains(className))
                {
                    list.Add(className);
                    _elem.Attributes["class"] = string.Join(" ", list);
                    _overlay.RefreshUI();
                }
            }
            public void remove(string className)
            {
                if (string.IsNullOrEmpty(className)) return;
                string classes = _elem.Attributes.GetValueOrDefault("class", "");
                var list = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (list.Remove(className))
                {
                    _elem.Attributes["class"] = string.Join(" ", list);
                    _overlay.RefreshUI();
                }
            }
            public void toggle(string className)
            {
                if (contains(className))
                    remove(className);
                else
                    add(className);
            }
        }
        public ClassList classList => new ClassList(elem, overlay);
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
        public void releaseDrag()
        {
            overlay.RefreshUI();
            if (overlay._document != null && overlay._document._eventListeners.TryGetValue("mouseup", out var mouseupListeners))
            {
                var mouseEvent = new Dictionary<object, object>
                {
                    ["clientX"] = 0.0,
                    ["clientY"] = 0.0,
                    ["target"] = this
                };
                foreach (var handler in mouseupListeners.ToList())
                {
                    overlay._jsContext.Evaluator.CallFunction(handler, new List<object> { mouseEvent });
                }
            }
            Console.WriteLine("[JSElement] Drag released via real mouseup handler");
        }
        public void SetMember(object objValue, object propValue, object value)
        {
            if (objValue is Dictionary<object, object> dictObj)
            {
                dictObj[propValue] = value;
                return;
            }
            if (objValue is JSElement.StyleProxy proxy)
            {
                proxy[propValue.ToString()] = value;
                return;
            }
            if (objValue is List<object> listObj && propValue is double propD && Math.Floor(propD) == propD)
            {
                int index = (int)propD;
                if (index >= 0 && index < listObj.Count)
                {
                    listObj[index] = value;
                    return;
                }
            }
            if (objValue is JSElement jsElem && propValue is string prop)
            {
                if (prop == "value")
                {
                    string tag = jsElem.elem.Tag.ToLower();
                    if (tag == "select")
                    {
                        bool found = false;
                        foreach (var opt in jsElem.elem.Children.Where(c => c.Tag.ToLower() == "option"))
                        {
                            string optVal = opt.Attributes.GetValueOrDefault("value", ((TextElement)opt.Children.FirstOrDefault())?.Content ?? "");
                            if (optVal == value.ToString())
                            {
                                opt.Attributes["selected"] = "";
                                found = true;
                            }
                            else
                            {
                                opt.Attributes.Remove("selected");
                            }
                        }
                    }
                    else if (tag == "option")
                    {
                        jsElem.elem.Attributes["value"] = value.ToString();
                    }
                    else if (tag == "input")
                    {
                        if (jsElem.elem is InputElement inp)
                        {
                            inp.Value = value.ToString();
                        }
                        jsElem.elem.Attributes["value"] = value.ToString();
                    }
                }
                else if (prop == "innerHTML")
                {
                    if (value is string strVal && strVal == "")
                    {
                        jsElem.elem.Children.Clear();
                    }
                }
                else if (prop == "textContent")
                {
                    if (value is string txt)
                    {
                        jsElem.elem.Children.RemoveAll(c => c is TextElement);
                        if (!string.IsNullOrEmpty(txt))
                        {
                            TextElement textElem = new TextElement { Content = txt };
                            textElem.Parent = jsElem.elem;
                            jsElem.elem.Children.Add(textElem);
                        }
                    }
                }
                else if (prop == "style")
                {
                    if (value is Dictionary<object, object> styleDict)
                    {
                        foreach (var kv in styleDict)
                        {
                            if (kv.Key is string key && kv.Value is string val)
                            {
                                jsElem.elem.Style.SetProperty(key, val);
                            }
                        }
                    }
                }
                return;
            }
            var type = objValue?.GetType();
            var prop1 = type?.GetProperty(propValue.ToString());
            prop1?.SetValue(objValue, value);
        }
    }
}