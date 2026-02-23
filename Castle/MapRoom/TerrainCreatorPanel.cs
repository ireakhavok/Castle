// Folder: MapRoom
// File: TerrainCreatorPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using ReadingChamber;
using SiegeEngine.Scenes;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace MapRoom
{
    public class TerrainCreatorPanel : BasePanel
    {
        private static IRenderContext _staticRenderContext;
        private static IControlContext _staticControlContext;
        private static nint _staticWindow;
        private static EventBus _staticEventBus;
        private static bool _subscriptionInitialized = false;

        private class TerrainUIOverlay : UIOverlay
        {
            private readonly TerrainCreatorPanel _parent;
            public TerrainUIOverlay(TerrainCreatorPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }

        private TerrainCreatorScene _terrainScene;
        private string _initialTerrainPath;
        private TerrainCreationParams _creationParams;   // NEW: full parameters from form
        private bool _cameraMode = true;
        private bool _lastTab = false;

        // ORIGINAL CONSTRUCTOR (kept for Import/Blank compatibility)
        public TerrainCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, string initialTerrainPath = null)
            : base(renderContext, controlContext, window, eventBus)
        {
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            BaseHeight = 720f;
            _initialTerrainPath = initialTerrainPath;
            _terrainScene = new TerrainCreatorScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }

        // NEW CONSTRUCTOR - accepts full TerrainCreationParams from NewTerrainPanel
        public TerrainCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, TerrainCreationParams creationParams)
            : this(renderContext, controlContext, window, eventBus, creationParams?.ImportPath)
        {
            _creationParams = creationParams;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new TerrainUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            _terrainScene.Initialize((int)Size.Y, (int)Size.X);

            if (_creationParams != null)
            {
                // Use full parameters from the form (width, depth, resolution, initial height, etc.)
                _terrainScene.CreateTerrain(_creationParams);
            }
            else if (!string.IsNullOrEmpty(_initialTerrainPath))
            {
                _terrainScene.LoadTerrain(_initialTerrainPath);
            }
            else
            {
                _terrainScene.CreateBlank();
            }

            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            LoadTerrainControlsUI();
        }

        private void LoadTerrainControlsUI()
        {
            string inlineHtml = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <style>
        .ui-container {
            position: absolute;
            bottom: 20px;
            left: 50%;
            transform: translateX(-50%);
            background-color: rgba(30, 30, 30, 0.8);
            padding: 10px 20px;
            border-radius: 8px;
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 15px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
            border: 1px solid rgba(100, 100, 100, 0.5);
            flex-wrap: wrap;
        }
        .ui-button {
            padding: 8px 16px;
            background-color: #555;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
            transition: background-color 0.3s, transform 0.1s;
        }
        .ui-button:hover {
            background-color: #777;
        }
        .ui-button:active {
            transform: scale(0.98);
            background-color: #444;
        }
    </style>
</head>
<body>
    <div class=""ui-container"" id=""controls-bar"">
        <button class=""ui-button"" data-hook=""LoadTerrainTexture"">Import Terrain Texture</button>
    </div>
</body>
</html>";
            _uiOverlay.LoadUI(inlineHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void OnFileSelected(FileSelectedEvent e)
        {
            string hook = e.UserData as string;
            if (hook == "LoadTerrainTexture")
            {
                _terrainScene.SetColorTexture(e.Path);
                Console.WriteLine($"[TerrainCreatorPanel] Color texture loaded: {e.Path}");
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "LoadTerrainTexture")
            {
                string terrainDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", "Textures");
                if (!Directory.Exists(terrainDir))
                {
                    Directory.CreateDirectory(terrainDir);
                    Console.WriteLine($"[TerrainCreatorPanel] Created texture directory: {terrainDir}");
                }
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, terrainDir, ".jp2", ".tif", ".tiff", ".png", ".jpg");
                fileSelector.UserData = "LoadTerrainTexture";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = SiegeEngine.Core.Events.OpenMode.Overlay });
            }
        }

        public static void OpenBlank(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new TerrainCreatorPanel(renderContext, controlContext, window, eventBus, (string)null);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = SiegeEngine.Core.Events.OpenMode.Replace });
        }

        public static void OpenImport(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _staticRenderContext = renderContext;
            _staticControlContext = controlContext;
            _staticWindow = window;
            _staticEventBus = eventBus;
            if (!_subscriptionInitialized)
            {
                eventBus.Subscribe<FileSelectedEvent>(StaticOnFileSelected);
                eventBus.Subscribe<CreateTerrainEvent>(StaticOnCreateTerrain);
                _subscriptionInitialized = true;
            }
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string terrainDir = Path.Combine(baseDir, "Assets", "Terrain");
            if (!Directory.Exists(terrainDir))
            {
                Directory.CreateDirectory(terrainDir);
            }
            var fileSelector = new FileSelectorPanel(renderContext, controlContext, window, eventBus, terrainDir, ".tif", ".tiff");
            fileSelector.UserData = "TerrainImport";
            fileSelector.IsModal = true;
            eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = SiegeEngine.Core.Events.OpenMode.Overlay });
        }

        private static void StaticOnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData as string == "TerrainImport" && !string.IsNullOrEmpty(e.Path) && _staticRenderContext != null)
            {
                var panel = new TerrainCreatorPanel(_staticRenderContext, _staticControlContext, _staticWindow, _staticEventBus, e.Path);
                _staticEventBus.Publish(new OpenPanelEvent(panel) { Mode = SiegeEngine.Core.Events.OpenMode.Replace });
            }
        }

        private static void StaticOnCreateTerrain(CreateTerrainEvent e)
        {
            if (_staticRenderContext == null) return;
            var panel = new TerrainCreatorPanel(_staticRenderContext, _staticControlContext, _staticWindow, _staticEventBus, e.Params);
            _staticEventBus.Publish(new OpenPanelEvent(panel) { Mode = SiegeEngine.Core.Events.OpenMode.Replace });
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            var tab = _controlContext.GetKey(_window, Key.Tab);
            if (tab == InputAction.Press && !_lastTab)
            {
                _cameraMode = !_cameraMode;
                _lastTab = true;
            }
            else if (tab != InputAction.Press)
            {
                _lastTab = false;
            }
            base.Update(deltaTime, absMousePos, mouseDown && !_cameraMode, mousePressed && !_cameraMode, mouseReleased && !_cameraMode, scrollDelta);
            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - TitleHeight);
            _terrainScene.Update(deltaTime, sceneMouse, mouseDown && _cameraMode, mousePressed && _cameraMode, mouseReleased && _cameraMode, _cameraMode);
        }

        public override void Render()
        {
            if (!Visible) return;
            if (_lastW != (int)Size.X || _lastH != (int)Size.Y)
            {
                _lastW = (int)Size.X;
                _lastH = (int)Size.Y;
                _terrainScene.Resize(_lastW, _lastH);
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            _terrainScene.Render(null);
            base.Render();
        }

        public override void Dispose()
        {
            _terrainScene?.Dispose();
            base.Dispose();
        }
    }
}