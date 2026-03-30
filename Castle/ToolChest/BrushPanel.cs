// Folder: ToolChest
// File: BrushPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.IO;
using System.Numerics;

namespace ToolChest
{
    public class BrushPanel : BasePanel
    {
        private class BrushUIOverlay : UIOverlay
        {
            private readonly BrushPanel _parent;
            public BrushUIOverlay(BrushPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleDataHook(string hook)
            {
                _parent.HandleBrushDataHook(hook);
            }
        }

        private Brush _currentBrush = new Brush();

        public BrushPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new BrushUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            LoadBrushUI();
            _eventBus.Publish(new SelectBrushEvent(0, _currentBrush.Mode.ToString(), _currentBrush.Size, _currentBrush.Intensity, _currentBrush.Shape.ToString(), _currentBrush.Falloff.ToString()), true);
        }

        private void LoadBrushUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrushPanelUI.html");
            if (File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
            _uiOverlay.RefreshUI();
        }

        private void HandleBrushDataHook(string hook)
        {
            bool changed = false;
            if (hook == "BrushSizeChanged")
            {
                var slider = _uiOverlay.FindElementById("sizeSlider") as InputElement;
                if (slider != null)
                {
                    float size = float.Parse(slider.Value ?? "10");
                    _currentBrush.Size = size;
                    changed = true;
                }
            }
            else if (hook == "BrushIntensityChanged")
            {
                var slider = _uiOverlay.FindElementById("intensitySlider") as InputElement;
                if (slider != null)
                {
                    float intensity = float.Parse(slider.Value ?? "1");
                    _currentBrush.Intensity = intensity;
                    changed = true;
                }
            }
            else if (hook == "BrushShapeChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushShapeChanged") as SelectElement;
                if (select != null)
                {
                    string shapeStr = select.Value ?? "Circle";
                    _currentBrush.Shape = (BrushShape)Enum.Parse(typeof(BrushShape), shapeStr);
                    changed = true;
                }
            }
            else if (hook == "BrushFalloffChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushFalloffChanged") as SelectElement;
                if (select != null)
                {
                    string falloffStr = select.Value ?? "Gaussian";
                    _currentBrush.Falloff = (BrushFalloff)Enum.Parse(typeof(BrushFalloff), falloffStr);
                    changed = true;
                }
            }
            else if (hook == "BrushModeChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushModeChanged") as SelectElement;
                if (select != null)
                {
                    string modeStr = select.Value ?? "Raise";
                    _currentBrush.Mode = (BrushMode)Enum.Parse(typeof(BrushMode), modeStr);
                    changed = true;
                }
            }
            else if (hook == "ClosePanel")
            {
                _eventBus.Publish(new ClosePanelEvent(this));
            }
            if (changed)
            {
                _eventBus.Publish(new SelectBrushEvent(0, _currentBrush.Mode.ToString(), _currentBrush.Size, _currentBrush.Intensity, _currentBrush.Shape.ToString(), _currentBrush.Falloff.ToString()), true);
            }
        }

        public override void Detach()
        {
            _eventBus.Publish(new SelectBrushEvent(0, "", 0f, 0f, "", ""), true);
            base.Detach();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new BrushPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}