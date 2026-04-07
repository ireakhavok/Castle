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
using ToolChest;
using SiegeEngine.Core.Managers;

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
            public override bool HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
                return true;
            }
        }

        private TerrainCreatorScene _terrainScene;
        private string _initialTerrainPath;
        private TerrainCreationParams _creationParams;
        private bool _cameraMode = false;
        private int _lastW;
        private int _lastH;
        private SceneData _currentSceneData;

        // NEW 4-param constructor for layout deserialization (this is what was missing)
        public TerrainCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : this(renderContext, controlContext, window, eventBus, (SceneData)null)
        {
        }

        public TerrainCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, string initialTerrainPath = null)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            Scaling = ScalingMode.BestFit;
            DockingMode = DockingMode.IDE;
            BaseWidth = 1280f;
            BaseHeight = 720f;
            _initialTerrainPath = initialTerrainPath;
            _currentSceneData = null;
            _terrainScene = new TerrainCreatorScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }

        public TerrainCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, TerrainCreationParams creationParams)
            : this(renderContext, controlContext, window, eventBus, creationParams?.ImportPath)
        {
            _creationParams = creationParams;
        }

        public TerrainCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, SceneData sceneData)
            : this(renderContext, controlContext, window, eventBus, (string)null)
        {
            _currentSceneData = sceneData;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new TerrainUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            _controlContext.SetMainWindow(_window);
            _terrainScene.Initialize((int)Size.Y, (int)Size.X);

            if (_currentSceneData != null && _currentSceneData.Terrain != null && !string.IsNullOrEmpty(_currentSceneData.Terrain.HeightmapPath))
            {
                _terrainScene.LoadTerrain(_currentSceneData.Terrain.HeightmapPath);
            }
            else if (_creationParams != null)
            {
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
            _eventBus.Subscribe<SelectBrushEvent>(OnBrushSelected);
            _eventBus.Subscribe<TerrainModifiedEvent>(OnTerrainModified);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            LoadTerrainControlsUI();
        }

        private void LoadTerrainControlsUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TerrainCreatorUI.html");
            if (File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
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
            }
            else if (hook == "LoadTerrainFile")
            {
                _terrainScene.LoadTerrain(e.Path);
                if (_currentSceneData != null && _currentSceneData.Terrain != null)
                {
                    _currentSceneData.Terrain.HeightmapPath = e.Path;
                }
            }
        }

        private void OnBrushSelected(SelectBrushEvent e)
        {
            if (string.IsNullOrEmpty(e.BrushMode) || e.Size == 0f)
            {
                _terrainScene.SetActiveBrush(null);
                return;
            }
            var brush = new ToolChest.Brush
            {
                Mode = (BrushMode)Enum.Parse(typeof(BrushMode), e.BrushMode, true),
                Shape = (BrushShape)Enum.Parse(typeof(BrushShape), e.BrushShape, true),
                Falloff = (BrushFalloff)Enum.Parse(typeof(BrushFalloff), e.BrushFalloff, true),
                Size = e.Size,
                Intensity = e.Intensity
            };
            _terrainScene.SetActiveBrush(brush);
        }

        private void OnTerrainModified(TerrainModifiedEvent e)
        {
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "LoadTerrainTexture")
            {
                string terrainDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", "Textures");
                if (!Directory.Exists(terrainDir)) Directory.CreateDirectory(terrainDir);
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, terrainDir, ".jp2", ".tif", ".tiff", ".png", ".jpg");
                fileSelector.UserData = "LoadTerrainTexture";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
            else if (hook == "LoadTerrain")
            {
                string terrainDir = Path.Combine(ProjectSettings.Current.ActiveProject ?? AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain");
                if (!Directory.Exists(terrainDir)) Directory.CreateDirectory(terrainDir);
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, terrainDir, ".tif", ".tiff");
                fileSelector.UserData = "LoadTerrainFile";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
            else if (hook == "SaveTerrain")
            {
                string name = _creationParams?.Name ?? (_currentSceneData?.Name ?? "UntitledTerrain");
                _terrainScene.SaveTerrain(name);
            }
            else if (hook == "OpenBrushPanel")
            {
                BrushPanel.Open(_renderContext, _controlContext, _window, _eventBus);
            }
            else if (hook == "Export2D")
            {
                if (!string.IsNullOrEmpty(ProjectSettings.Current.ActiveProject))
                {
                    string assetsDir = Path.Combine(ProjectSettings.Current.ActiveProject, "Assets");
                    _terrainScene.Export2D(assetsDir);
                }
            }
        }

        public override void ToggleCameraMode()
        {
            _cameraMode = !_cameraMode;
            if (_cameraMode)
            {
                PanelManager.Current.CapturePanel(this);
            }
            else
            {
                PanelManager.Current.ReleasePanelCapture();
            }
        }

        public static void OpenBlank(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new TerrainCreatorPanel(renderContext, controlContext, window, eventBus, (string)null);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
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
            eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
        }

        private static void StaticOnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData as string == "TerrainImport" && !string.IsNullOrEmpty(e.Path) && _staticRenderContext != null)
            {
                var panel = new TerrainCreatorPanel(_staticRenderContext, _staticControlContext, _staticWindow, _staticEventBus, e.Path);
                _staticEventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
            }
        }

        private static void StaticOnCreateTerrain(CreateTerrainEvent e)
        {
            if (_staticRenderContext == null) return;
            var panel = new TerrainCreatorPanel(_staticRenderContext, _staticControlContext, _staticWindow, _staticEventBus, e.Params);
            _staticEventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown && !_cameraMode, mousePressed && !_cameraMode, mouseReleased && !_cameraMode, scrollDelta);

            float header = HasTitleBar ? HeaderHeight : 0f;
            float contentX = Position.X;
            float contentY = Position.Y + header;
            float contentW = Size.X;
            float contentH = Size.Y - header;
            if (_cameraMode)
            {
                _controlContext.PushViewport(new Viewport((int)contentX, (int)contentY, (int)contentW, (int)contentH));
            }
            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - HeaderHeight);
            _terrainScene.Update(deltaTime, sceneMouse, mouseDown && _cameraMode, mousePressed && _cameraMode, mouseReleased && _cameraMode, _cameraMode);
            if (_cameraMode)
            {
                _controlContext.PopViewport();
            }
        }

        protected override void RenderInnerContent()
        {
            _terrainScene.Render(null);
        }

        public override void OnLiveResize(float w, float h)
        {
            _terrainScene.Resize((int)w, (int)h);
        }

        public override void Dispose()
        {
            PanelManager.Current.ReleasePanelCapture();
            _terrainScene?.Dispose();
            base.Dispose();
        }

        public static void OpenBrushPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            BrushPanel.Open(renderContext, controlContext, window, eventBus);
        }
    }
}