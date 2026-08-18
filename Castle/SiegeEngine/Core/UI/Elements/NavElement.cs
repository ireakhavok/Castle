// Folder: SiegeEngine.Core.UI
// File: NavElement.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU;
using System;
using System.Numerics;
using System.Linq;

namespace SiegeEngine.Core.UI.Elements
{
    public class NavElement : HtmlElement
    {
        public NavElement()
        {
            Tag = "nav";
            // Minimal defaults only. All nav-specific styling, hover states,
            // and dropdown (nav > ul > li > ul) behavior now handled exclusively
            // by CssParser.DefaultUserAgentCss (added in this iteration).
            // This fixes CSS not being applied to nav items.
            Style.Display = "flex";
            Style.BackgroundColor = new Vector4(0.121f, 0.121f, 0.121f, 1f);
            Style.HeightStr = "28px";
            Style.FontSizeStr = "14px";
        }
    }
}