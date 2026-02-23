// Folder: MapRoom
// File: NewTerrainPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Scenes;
using System;
using System.IO;
using System.Numerics;
using ReadingChamber;
namespace MapRoom
{
    public class NewTerrainPanel : BasePanel
    {
        private class NewTerrainUIOverlay : UIOverlay
        {
            private readonly NewTerrainPanel _parent;
            public NewTerrainUIOverlay(NewTerrainPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleUIClick(HtmlElement elem)
            {
                base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
            }
        }
        private string _selectedImportPath = null;
        public NewTerrainPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            IsModal = true;
            Scaling = ScalingMode.BestFit;
            BaseWidth = 520f;
            BaseHeight = 620f;
            AllowDragging = true;
            DockState = SiegeEngine.Core.Interfaces.DockState.Floating;
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new NewTerrainUIOverlay(this, _renderContext, _controlContext, _window);
        }
        public override void Init()
        {
            base.Init();
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            LoadNewTerrainFormUI();
        }
        private void LoadNewTerrainFormUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NewTerrainForm.html");
            if (File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
            else
            {
                Console.WriteLine($"[NewTerrainPanel] NewTerrainForm.html not found at {htmlPath}");
            }
            _uiOverlay.RefreshUI();
        }
        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "BrowseGeoTIFF")
            {
                string terrainDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain");
                if (!Directory.Exists(terrainDir))
                {
                    Directory.CreateDirectory(terrainDir);
                }
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, terrainDir, ".tif", ".tiff");
                fileSelector.UserData = "NewTerrainImport";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
            else if (hook == "CreateTerrain")
            {
                var nameElem = _uiOverlay.FindElementById("name");
                var typeElem = _uiOverlay.FindElementById("type");
                var widthElem = _uiOverlay.FindElementById("width");
                var depthElem = _uiOverlay.FindElementById("depth");
                var resElem = _uiOverlay.FindElementById("resolution");
                var initHElem = _uiOverlay.FindElementById("initialHeight");
                var exagElem = _uiOverlay.FindElementById("vertExag");
                float cellSize = float.Parse(resElem.Attributes.GetValueOrDefault("value", "1.0"));
                var parameters = new TerrainCreationParams
                {
                    Name = nameElem.Attributes.GetValueOrDefault("value", "NewTerrain"),
                    Type = typeElem.Attributes.GetValueOrDefault("value", "Flat"),
                    Width = float.Parse(widthElem.Attributes.GetValueOrDefault("value", "2048")),
                    Depth = float.Parse(depthElem.Attributes.GetValueOrDefault("value", "2048")),
                    Resolution = (int)Math.Ceiling(float.Parse(widthElem.Attributes.GetValueOrDefault("value", "2048")) / cellSize),
                    InitialHeight = float.Parse(initHElem.Attributes.GetValueOrDefault("value", "0")),
                    VerticalExaggeration = float.Parse(exagElem.Attributes.GetValueOrDefault("value", "1.0")),
                    ImportPath = _selectedImportPath
                };
                _eventBus.Publish(new CreateTerrainEvent(parameters));
                _eventBus.Publish(new ClosePanelEvent(this));
            }
            else if (hook == "CancelNewTerrain")
            {
                _eventBus.Publish(new ClosePanelEvent(this));
            }
        }
        private void OnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData as string == "NewTerrainImport")
            {
                _selectedImportPath = e.Path;
            }
        }
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new NewTerrainPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
        public override void Dispose()
        {
            base.Dispose();
        }
    }
}