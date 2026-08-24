// file: core.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public partial class HtmlElement
    {
        public string Tag { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public List<HtmlElement> Children { get; set; } = new List<HtmlElement>();
        public CssStyle Style { get; set; } = new CssStyle();
        public Dictionary<string, CssStyle> PseudoStyles { get; set; } = new Dictionary<string, CssStyle>();
        public HtmlElement Parent { get; set; }
        public Vector2 ComputedPosition { get; set; }
        public float ComputedWidth { get; set; }
        public float ComputedHeight { get; set; }
        public float ComputedContentX { get; set; }
        public float ComputedContentY { get; set; }
        public float ComputedContentWidth { get; set; }
        public float ComputedContentHeight { get; set; }
        public float ComputedBackgroundX { get; set; }
        public float ComputedBackgroundY { get; set; }
        public float ComputedBackgroundWidth { get; set; }
        public float ComputedBackgroundHeight { get; set; }
        public Vector4 BorderWidth { get; set; }
        public bool IsHover { get; set; }
        public bool IsActive { get; set; }
        public bool Checked { get; set; }
        public bool IsTarget { get; set; }
        public bool IsFocused { get; set; }
        public string OnClickJS { get; set; }
        public string OnChangeJS { get; set; }
        public string OnMouseEnterJS { get; set; }
        public string OnMouseLeaveJS { get; set; }
        public string OnMouseOverJS { get; set; }
        public string OnMouseOutJS { get; set; }
        public string OnMouseDownJS { get; set; }
        public string OnMouseUpJS { get; set; }
        public string OnFocusJS { get; set; }
        public string OnBlurJS { get; set; }
        public Dictionary<string, List<object>> EventListeners { get; } = new Dictionary<string, List<object>>();
        private BackgroundRenderer _bgRenderer;
        private string _baseDir;
        protected Matrix4x4 ComputedTransform;
        protected Matrix4x4 ComputedFullTransform;
        public float ScrollOffsetY { get; set; } = 0f;
        private float _contentFullHeight = 0f;
        private bool _needsVerticalScrollbar = false;
        private const float SCROLLBAR_WIDTH = 12f;
        private Vector2 _cachedIntrinsicSize;
        private float _cachedViewportWidth;
        private float _cachedViewportHeight;
        private float _cachedFs;
        private bool _intrinsicDirty = true;

        public void MarkIntrinsicDirty()
        {
            _intrinsicDirty = true;
            foreach (var child in Children)
            {
                child.MarkIntrinsicDirty();
            }
        }

        public bool IsDescendantOf(HtmlElement ancestor)
        {
            var current = this;
            while (current != null)
            {
                if (current == ancestor) return true;
                current = current.Parent;
            }
            return false;
        }

        public string GetEffectiveDisplay()
        {
            CssStyle effective = Style;
            if (IsTarget && PseudoStyles.TryGetValue("target", out CssStyle ts))
                effective = ts;
            if (Checked && PseudoStyles.TryGetValue("checked", out CssStyle cs))
                effective = cs;
            if (IsHover && PseudoStyles.TryGetValue("hover", out CssStyle hs))
                effective = hs;
            if (IsActive && PseudoStyles.TryGetValue("active", out CssStyle a))
                effective = a;
            string display = effective.Display;
            if (Tag.ToLower() == "ul")
            {
                var parentLi = Parent as LiElement;
                if (parentLi != null)
                {
                    HtmlElement current = parentLi;
                    bool isUnderNav = false;
                    while (current != null)
                    {
                        if (current.Tag.ToLower() == "nav")
                        {
                            isUnderNav = true;
                            break;
                        }
                        current = current.Parent;
                    }
                    if (isUnderNav && parentLi.IsHover)
                    {
                        display = "block";
                    }
                }
            }
            return display;
        }

        private HtmlElement FindContainingBlock()
        {
            HtmlElement current = Parent;
            while (current != null && current.Style.Position == "static")
            {
                current = current.Parent;
            }
            return current;
        }

        public virtual void UpdateFullTransforms(Matrix4x4 parentMatrix)
        {
            ComputedFullTransform = parentMatrix * ComputedTransform;
            foreach (var child in Children)
            {
                child.UpdateFullTransforms(ComputedFullTransform);
            }
        }

        public void PrepareResources(string baseDir, IControlContext controlContext, nint window, IRenderContext renderContext, ShaderProgram shader)
        {
            _baseDir = baseDir;
            if (!string.IsNullOrEmpty(Style.BackgroundImage))
            {
                string relativePath = Style.BackgroundImage;
                string fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                Console.WriteLine($"HtmlElement: Attempting to load background texture from: {fullPath}");
                if (File.Exists(fullPath))
                {
                    _bgRenderer = new BackgroundRenderer(controlContext, window, renderContext);
                    _bgRenderer.Initialize(fullPath, shader);
                }
                else
                {
                    Console.WriteLine($"HtmlElement: Background file not found: {fullPath}");
                }
            }
            foreach (var child in Children)
            {
                child.PrepareResources(baseDir, controlContext, window, renderContext, shader);
            }
        }

        public HtmlElement FindElementById(string id)
        {
            if (Attributes.TryGetValue("id", out var elemId) && elemId == id) return this;
            foreach (var child in Children)
            {
                var found = child.FindElementById(id);
                if (found != null) return found;
            }
            return null;
        }

        public float GetAncestorScrollOffset()
        {
            float sum = 0f;
            var current = Parent;
            while (current != null)
            {
                sum += current.ScrollOffsetY;
                current = current.Parent;
            }
            return sum;
        }
    }
}