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
    public class TerrainCreatorPanel : ClosablePanel
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
            public override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }
        private TerrainCreatorScene _terrainScene;
        private string _initialTerrainPath;
        private TerrainCreationParams _creationParams;
        private bool _cameraMode = true;
        private bool _lastTab = false;
        public TerrainCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, string initialTerrainPath = null)
            : base(renderContext, controlContext, window, eventBus)
        {
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            BaseHeight = 720f;
            _initialTerrainPath = initialTerrainPath;
            _terrainScene = new TerrainCreatorScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }
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
                Console.WriteLine($"[TerrainCreatorPanel] Loaded external TerrainCreatorUI.html");
            }
            else
            {
                return;
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
                Console.WriteLine($"[TerrainCreatorPanel] Color texture loaded: {e.Path}");
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
            else if (hook == "SaveTerrain")
            {
                string name = _creationParams?.Name ?? "UntitledTerrain";
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
            if (_cameraMode)
            {
                var contentViewport = new Viewport(Position.X, Position.Y + TitleHeight, Size.X, Size.Y - TitleHeight);
                _controlContext.PushViewport(contentViewport);
            }
            else
            {
                _controlContext.PopViewport();
            }
            _terrainScene.Update(deltaTime, sceneMouse, mouseDown && _cameraMode, mousePressed && _cameraMode, mouseReleased && _cameraMode, _cameraMode);
        }
        public override void Render()
        {
            if (!Visible) return;
            if (IsResizing)
            {
                base.Render();
                return;
            }
            if (_lastW != (int)Size.X || _lastH != (int)Size.Y)
            {
                _lastW = (int)Size.X;
                _lastH = (int)Size.Y;
                _terrainScene.Resize(_lastW, _lastH);
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            var contentRect = GetContentRect();
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            _renderContext.Enable(_renderContext.Enums.ScissorTest);
            int scissorX = (int)contentRect.X;
            int scissorY = winH - (int)(contentRect.Y + contentRect.Height);
            uint scissorW = (uint)contentRect.Width;
            uint scissorH = (uint)contentRect.Height;
            _renderContext.Scissor(scissorX, scissorY, scissorW, scissorH);
            _terrainScene.Render(null);
            _renderContext.Disable(_renderContext.Enums.ScissorTest);
            base.Render();
        }
        public override void OnLiveResize(float w, float h)
        {
            _terrainScene.Resize((int)w, (int)h);
        }
        public override void Dispose()
        {
            _terrainScene?.Dispose();
            base.Dispose();
        }
        public static void OpenBrushPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            BrushPanel.Open(renderContext, controlContext, window, eventBus);
        }
    }
}