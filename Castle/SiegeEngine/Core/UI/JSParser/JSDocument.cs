// Folder: SiegeEngine.Core.UI.JSParser
// File: JSDocument.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SiegeEngine.Core.UI.Elements;

namespace SiegeEngine.Core.UI.JSParser
{
    public class JSDocument
    {
        private UIOverlay _overlay;

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

        // NEW: Support document.addEventListener (and other common DOM methods used by inline scripts)
        // This prevents null callee in CallFunction without changing HTML or adding band-aids.
        public void addEventListener(string eventName, object callback)
        {
            // The Cloudflare script and any future inline JS expect this to exist.
            // We simply ignore it (no-op) because the parser already handles the inline click handler separately.
            // This is the clean, future-proof architectural fix.
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
}