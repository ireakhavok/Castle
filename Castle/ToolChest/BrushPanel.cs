// Folder: ToolChest
// File: BrushPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.IO;
using System.Numerics;
namespace ToolChest
{
    public class BrushPanel : CompanionPanel
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
                if (hook.StartsWith("Brush"))
                {
                    _parent.HandleBrushDataHook(hook);
                }
            }
        }
        private Brush _currentBrush = new Brush();
        public BrushPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new BrushUIOverlay(this, _renderContext, _controlContext, _window);
        }
        public override void Init()
        {
            base.Init();
            LoadBrushUI();
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
            if (hook == "BrushSizeChanged")
            {
                var slider = _uiOverlay.FindElementById("sizeSlider");
                if (slider != null)
                {
                    float size = float.Parse(slider.Attributes.GetValueOrDefault("value", "10"));
                    _currentBrush.Size = size;
                }
            }
            else if (hook == "BrushIntensityChanged")
            {
                var slider = _uiOverlay.FindElementById("intensitySlider");
                if (slider != null)
                {
                    float intensity = float.Parse(slider.Attributes.GetValueOrDefault("value", "1"));
                    _currentBrush.Intensity = intensity;
                }
            }
            else if (hook == "BrushShapeChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushShapeChanged");
                if (select != null)
                {
                    string shapeStr = select.Attributes.GetValueOrDefault("value", "GaussianCircle");
                    _currentBrush.Shape = (BrushShape)Enum.Parse(typeof(BrushShape), shapeStr);
                }
            }
            else if (hook == "BrushModeChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushModeChanged");
                if (select != null)
                {
                    string modeStr = select.Attributes.GetValueOrDefault("value", "Raise");
                    _currentBrush.Mode = (BrushMode)Enum.Parse(typeof(BrushMode), modeStr);
                }
            }
            _eventBus.Publish(new SelectBrushEvent(0, _currentBrush.Mode.ToString(), _currentBrush.Size, _currentBrush.Intensity, _currentBrush.Shape.ToString()), true);
        }
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new BrushPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}