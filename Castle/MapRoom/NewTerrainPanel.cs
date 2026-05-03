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
using SiegeEngine.Core.UI.Elements;
using Keystone;

namespace MapRoom
{
    public class NewTerrainPanel : BasePanel
    {
        private class NewTerrainUIOverlay : UIOverlay
        {
            private readonly NewTerrainPanel _parent;
            public NewTerrainUIOverlay(NewTerrainPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window) : base(renderContext, controlContext, window) { _parent = parent; }
            public override bool HandleUIClick(HtmlElement elem)
            {
                string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
                if (hook == "CreateTerrain")
                {
                    _parent.HandleUIClick(elem);
                    return true;
                }
                _parent.HandleUIClick(elem);
                base.HandleUIClick(elem);
                return true;
            }
        }
        private string _selectedImportPath = null;
        public NewTerrainPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            IsModal = true;
            RenderOrder = 1100;
            Scaling = ScalingMode.Fill;
            Size = new Vector2(420, 460);
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
                var nameInput = _uiOverlay.FindElementById("name") as InputElement;
                var typeSelect = _uiOverlay.FindElementById("type");
                var widthInput = _uiOverlay.FindElementById("width") as InputElement;
                var depthInput = _uiOverlay.FindElementById("depth") as InputElement;
                var resSelect = _uiOverlay.FindElementById("resolution");
                var initHInput = _uiOverlay.FindElementById("initialHeight") as InputElement;
                var exagInput = _uiOverlay.FindElementById("vertExag") as InputElement;
                float cellSize = 1.0f;
                if (resSelect != null)
                {
                    var selectedOpt = resSelect.Children.OfType<OptionElement>().FirstOrDefault(o => o.Attributes.ContainsKey("selected"));
                    if (selectedOpt != null)
                    {
                        string valStr = selectedOpt.Attributes.GetValueOrDefault("value", "1.0");
                        if (float.TryParse(valStr, out float parsed))
                        {
                            cellSize = parsed;
                        }
                    }
                }
                var parameters = new TerrainCreationParams
                {
                    Name = nameInput?.Value ?? "NewTerrain",
                    Type = typeSelect.Attributes.GetValueOrDefault("value", "Flat"),
                    Width = float.Parse(widthInput?.Value ?? "2048"),
                    Depth = float.Parse(depthInput?.Value ?? "2048"),
                    Resolution = cellSize,
                    InitialHeight = float.Parse(initHInput?.Value ?? "0"),
                    VerticalExaggeration = float.Parse(exagInput?.Value ?? "1.0"),
                    ImportPath = _selectedImportPath
                };
                Console.WriteLine($"[NewTerrainPanel] Creating {parameters.Width}m x {parameters.Depth}m terrain with grid spacing {parameters.Resolution:F1}m per cell");
                var tempScene = new TerrainCreatorScene(_renderContext, _controlContext, _window, new ClientGameServerProxy(_eventBus), _eventBus);
                tempScene.Initialize((int)Size.Y, (int)Size.X);
                tempScene.CreateTerrain(parameters);
                var sceneData = new SceneData { Name = parameters.Name, SceneType = "TerrainTest" };
                sceneData.Terrain = new TerrainData { HeightmapPath = parameters.ImportPath, WorldScaleX = parameters.Resolution, WorldScaleZ = parameters.Resolution, VerticalExaggeration = parameters.VerticalExaggeration };
                float[,] heightmap = tempScene.GetHeightmap();

                // CRITICAL: Fresh Level for every new scene - guarantees no cross-contamination
                var newLevel = new Level(_eventBus) { Name = parameters.Name };
                ProjectSettings.Current.SetCurrentLevel(newLevel);
                Console.WriteLine($"[NewTerrainPanel] Created fresh Level for new scene '{parameters.Name}' (entities: {newLevel.Entities.Count})");

                ProjectSettings.Current.SetCurrentTerrain(sceneData, heightmap, parameters.Name, sceneData.Terrain.HeightmapPath);

                _eventBus.Publish(new CreateTerrainEvent(parameters));
                tempScene.Dispose();
                _eventBus.Publish(new ClosePanelEvent(this));
            }
            else if (hook == "CancelNewTerrain")
            {
                _eventBus.Publish(new ClosePanelEvent(this));
            }
        }
        private void OnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData as string == "NewTerrainImport" && !string.IsNullOrEmpty(e.Path))
            {
                _selectedImportPath = e.Path;
                Console.WriteLine($"[NewTerrainPanel] Selected GeoTIFF for import: {e.Path}");
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