// Folder: SiegeEngine.UI/JSParser
// File: JSDocument.cs
using System;

namespace SiegeEngine.UI.JSParser
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
    }
}