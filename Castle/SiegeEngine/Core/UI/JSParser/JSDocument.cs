// File: SiegeEngine/Core/UI/JSParser/JSDocument.cs
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.UI.JSParser
{
    public class JSDocument
    {
        private UIOverlay _overlay;
        public readonly Dictionary<string, List<object>> _eventListeners = new Dictionary<string, List<object>>();

        public JSDocument(UIOverlay overlay)
        {
            _overlay = overlay;
        }

        public JSElement getElementById(string id)
        {
            var elem = _overlay.FindElementById(id);
            return elem == null ? null : new JSElement(elem, _overlay);
        }

        public JSElement createElement(string tag)
        {
            HtmlElement newElem;
            switch (tag.ToLower())
            {
                case "option":
                    newElem = new OptionElement();
                    break;
                default:
                    newElem = new HtmlElement { Tag = tag };
                    break;
            }
            return new JSElement(newElem, _overlay);
        }

        public List<JSElement> getElementsByTagName(string tag)
        {
            var elems = _overlay.FindElementsByTag(tag);
            return elems.Select(e => new JSElement(e, _overlay)).ToList();
        }

        public List<JSElement> getElementsByClassName(string className)
        {
            var elems = _overlay.FindElementsByClass(className);
            return elems.Select(e => new JSElement(e, _overlay)).ToList();
        }

        public JSElement querySelector(string selector)
        {
            var elem = QuerySelectorAll(selector).FirstOrDefault();
            return elem == null ? null : new JSElement(elem, _overlay);
        }

        public List<JSElement> querySelectorAll(string selector)
        {
            var elems = QuerySelectorAll(selector);
            return elems.Select(e => new JSElement(e, _overlay)).ToList();
        }

        public void addEventListener(string eventName, object callback)
        {
            eventName = eventName.ToLower();
            if (!_eventListeners.ContainsKey(eventName))
                _eventListeners[eventName] = new List<object>();
            _eventListeners[eventName].Add(callback);
        }

        public void removeEventListener(string eventName, object callback)
        {
            eventName = eventName.ToLower();
            if (_eventListeners.ContainsKey(eventName))
            {
                _eventListeners[eventName].Remove(callback);
                if (_eventListeners[eventName].Count == 0)
                    _eventListeners.Remove(eventName);
            }
        }

        internal bool InvokeDocumentListeners(string eventName, HtmlElement targetElement)
        {
            eventName = eventName.ToLower();
            if (!_eventListeners.TryGetValue(eventName, out var listeners))
                return false;

            var jsEvent = new JSClickEvent(targetElement, _overlay);
            bool handled = false;

            foreach (var cb in listeners.ToList())
            {
                object result = _overlay._jsContext.Evaluator.CallFunction(cb, new List<object> { jsEvent });
                if (result is bool b && b)
                    handled = true;
            }
            return handled;
        }

        // NEW: public mousemove dispatch for timeline drag/scrub (clientX/Y + target)
        public void InvokeDocumentMousemove(Vector2 mousePos)
        {
            if (!_eventListeners.TryGetValue("mousemove", out var listeners) || listeners.Count == 0)
                return;

            var mouseEvent = new Dictionary<object, object>
            {
                ["clientX"] = mousePos.X,
                ["clientY"] = mousePos.Y,
                ["target"] = null
            };

            foreach (var cb in listeners.ToList())
            {
                _overlay._jsContext.Evaluator.CallFunction(cb, new List<object> { mouseEvent });
            }
        }

        private List<HtmlElement> QuerySelectorAll(string selector)
        {
            List<HtmlElement> matches = new List<HtmlElement>();
            Queue<HtmlElement> queue = new Queue<HtmlElement>();
            queue.Enqueue(_overlay._uiRoot);
            var css = new CssParser();
            while (queue.Count > 0)
            {
                var elem = queue.Dequeue();
                if (css.Matches(elem, selector))
                {
                    matches.Add(elem);
                }
                foreach (var child in elem.Children)
                {
                    queue.Enqueue(child);
                }
            }
            return matches;
        }
    }

    public class JSClickEvent
    {
        public JSElement target { get; }

        public JSClickEvent(HtmlElement targetElement, UIOverlay overlay)
        {
            target = new JSElement(targetElement, overlay);
        }
    }
}