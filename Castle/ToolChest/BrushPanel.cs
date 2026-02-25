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
    public class BrushPanel : BasePanel
    {
        private static BrushPanel _currentInstance;

        private class BrushUIOverlay : UIOverlay
        {
            private readonly BrushPanel _parent;

            public BrushUIOverlay(BrushPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }

            protected override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }

        private Brush _currentBrush = new Brush();

        public BrushPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            IsModal = false;
            Scaling = ScalingMode.BestFit;
            BaseWidth = 320f;
            BaseHeight = 420f;
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
            _currentInstance = this;
            LoadBrushUI();
        }

        private void LoadBrushUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrushPanelUI.html");
            if (File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
            else
            {
                Console.WriteLine($"[BrushPanel] BrushPanelUI.html not found at {htmlPath}");
            }
            _uiOverlay.RefreshUI();
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook.StartsWith("Brush"))
            {
                Console.WriteLine($"[BrushPanel] Hook triggered: {hook}");
            }
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            if (_currentInstance != null && _currentInstance.Visible)
            {
                _currentInstance.BringToAttention();
                return;
            }

            var panel = new BrushPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }

        private void BringToAttention()
        {
            // Flicker effect for visual feedback when already open
            Visible = false;
            _uiOverlay.RefreshUI();
            Visible = true;
            _uiOverlay.RefreshUI();
            Console.WriteLine("[BrushPanel] Already open - flicker triggered");
        }

        public override void Dispose()
        {
            if (_currentInstance == this)
                _currentInstance = null;
            base.Dispose();
        }
    }
}