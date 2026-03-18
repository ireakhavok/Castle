// Folder: ToolChest
// File: SpritePlacementPanel.cs
using ReadingChamber;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.IO;
namespace ToolChest
{
    public class SpritePlacementPanel : CompanionPanel
    {
        private class SpritePlacementUIOverlay : UIOverlay
        {
            private readonly SpritePlacementPanel _parent;
            public SpritePlacementUIOverlay(SpritePlacementPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleDataHook(string hook)
            {
                _parent.HandleDataHook(hook);
            }
            public override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }
        public override bool WantsContinuousUpdate => false;
        public SpritePlacementPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new SpritePlacementUIOverlay(this, _renderContext, _controlContext, _window);
        }
        public override void Init()
        {
            base.Init();
            LoadPlacementUI();
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
        }
        private void LoadPlacementUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SpritePlacementPanelUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"[SpritePlacementPanel] WARNING: SpritePlacementPanelUI.html not found at {htmlPath}");
                return;
            }
            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            Console.WriteLine("[SpritePlacementPanel] UI loaded successfully from SpritePlacementPanelUI.html");
        }
        public void HandleDataHook(string hook)
        {
            if (hook == "BrowseTexture")
            {
                string spritesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sprites");
                if (!Directory.Exists(spritesDir))
                {
                    Directory.CreateDirectory(spritesDir);
                }
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, spritesDir, ".png", ".jpg");
                fileSelector.UserData = "SelectSpriteTexture";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
            else if (hook == "ClosePanel")
            {
                _eventBus.Publish(new ClosePanelEvent(this));
            }
        }
        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(hook))
            {
                HandleDataHook(hook);
            }
        }
        private void OnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData as string == "SelectSpriteTexture")
            {
                string path = e.Path;
                float w = 2f, h = 2f;
                var widthElem = _uiOverlay.FindElementById("sizeWidth") as InputElement;
                var heightElem = _uiOverlay.FindElementById("sizeHeight") as InputElement;
                if (widthElem != null) float.TryParse(widthElem.Value ?? "2", out w);
                if (heightElem != null) float.TryParse(heightElem.Value ?? "2", out h);
                _eventBus.Publish(new SelectSpriteEvent(0, path, w, h), true);
                var preview = _uiOverlay.FindElementById("spritePreview");
                if (preview != null)
                {
                    preview.Attributes["src"] = path;
                    _uiOverlay.RefreshUI();
                }
                Console.WriteLine($"[SpritePlacementPanel] Sprite selected: {path} — ghost now active in scene");
            }
        }
        public override void Detach()
        {
            var clearEvent = new SelectSpriteEvent(0, "", 0f, 0f);
            _eventBus.Publish(clearEvent, true);
            base.Detach();
        }
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new SpritePlacementPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}