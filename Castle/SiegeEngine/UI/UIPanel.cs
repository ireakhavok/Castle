// Folder: SiegeEngine.UI
// File: UIPanel.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Interfaces;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Shaders;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.UI
{
    public abstract class UIPanel : IPanel
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly IntPtr _window;
        protected ShaderProgram _uiShader;
        protected TextRenderer _textRenderer;
        protected UIQuadRenderer _quadRenderer;
        protected CssParser _cssParser;
        protected HtmlElement _uiRoot;
        protected List<HtmlElement> _uiClickables = new List<HtmlElement>();
        public DockState DockState { get; set; } = DockState.Floating;
        protected UIPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
        }
        public virtual void Init()
        {
            _uiShader = new ShaderProgram(_renderContext, UiShader.VertexSource, UiShader.FragmentSource);
            _textRenderer = new TextRenderer(_renderContext, _window);
            _textRenderer.Initialize(_uiShader);
            _quadRenderer = new UIQuadRenderer(_renderContext);
            _cssParser = new CssParser();
        }
        protected void LoadUI(string html)
        {
            HtmlParser parser = new HtmlParser();
            _uiRoot = parser.Parse(html);
            ApplyUserAgentDefaults();
            _cssParser.ApplyInlineStyles(_uiRoot);
            _cssParser.ApplyAll(_uiRoot);
            InheritProperties(_uiRoot, null);
            _uiRoot.PrepareResources("", _controlContext, _window, _renderContext, _uiShader);
            _uiClickables.Clear();
            CollectClickables(_uiRoot);
            // Initial layout
            _controlContext.GetWindowSize(_window, out int w, out int h);
            _uiRoot.ComputeLayout(0, 0, w, h, w, h, _textRenderer, 16f, 0, 0);
        }
        private void ApplyUserAgentDefaults()
        {
            string defaultCss = @"
button {
    padding: 2px 10px;
    min-height: 30px;
    border: 1px solid rgba(128, 128, 128, 1);
    border-radius: 5px;
    background-color: rgba(200, 200, 200, 1);
    color: black;
}
";
            _cssParser.Apply(defaultCss);
        }
        private void InheritProperties(HtmlElement elem, HtmlElement parent)
        {
            if (parent != null)
            {
                if (string.IsNullOrEmpty(elem.Style.Color))
                {
                    elem.Style.Color = parent.Style.Color;
                    elem.Style.TextColor = parent.Style.TextColor;
                }
                if (string.IsNullOrEmpty(elem.Style.FontSizeStr))
                    elem.Style.FontSizeStr = parent.Style.FontSizeStr;
                if (string.IsNullOrEmpty(elem.Style.TextAlign))
                    elem.Style.TextAlign = parent.Style.TextAlign;
            }
            foreach (var child in elem.Children)
                InheritProperties(child, elem);
        }
        private void CollectClickables(HtmlElement elem)
        {
            if (elem.GetEffectiveDisplay() == "none") return;
            string classes = elem.Attributes.GetValueOrDefault("class", "");
            if (classes.Contains("button") || classes.Contains("toggle") || elem.Tag == "select" || elem.Tag == "label" || elem.Tag == "a" || elem.Attributes.ContainsKey("data-hook") || elem.Attributes.ContainsKey("onclick"))
            {
                _uiClickables.Add(elem);
            }
            foreach (var child in elem.Children)
                CollectClickables(child);
        }
        protected virtual void HandleUIClick(HtmlElement elem)
        {
        }
        public virtual void Update(float deltaTime)
        {
            // UI input handling
            Vector2 mousePos = new Vector2();
            _controlContext.GetCursorPos(_window, out double x, out double y);
            mousePos = new Vector2((float)x, (float)y);
            bool mouseDown = _controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Press;
            bool mouseUp = _controlContext.GetMouseButton(_window, MouseButton.Left) == InputAction.Release;
            HtmlElement clickedElem = null;
            foreach (var clickable in _uiClickables)
            {
                bool over = mousePos.X >= clickable.ComputedPosition.X && mousePos.X <= clickable.ComputedPosition.X + clickable.ComputedWidth &&
                            mousePos.Y >= clickable.ComputedPosition.Y && mousePos.Y <= clickable.ComputedPosition.Y + clickable.ComputedHeight;
                clickable.IsHover = over;
                if (over && mouseDown)
                {
                    clickable.IsActive = true;
                }
                if (over && mouseUp && clickable.IsActive)
                {
                    clickedElem = clickable;
                }
                if (mouseUp)
                {
                    clickable.IsActive = false;
                }
            }
            if (clickedElem != null)
            {
                HandleUIClick(clickedElem);
            }
        }
        protected void RenderUI(int w, int h)
        {
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _uiRoot.Render(_renderContext, _textRenderer, _quadRenderer, w, h);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }
        public virtual void Render()
        {
            _controlContext.GetWindowSize(_window, out int w, out int h);
            if (_uiRoot != null)
            {
                RenderUI(w, h);
            }
        }
        public virtual void Dispose()
        {
            _uiShader.Dispose();
            _textRenderer.Dispose();
        }
        public void Detach()
        {
            // Implement pop-out to new window
            // Create new ContextManager, new window, transfer state
        }
    }
}