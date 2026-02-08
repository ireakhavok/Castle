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
        public TerrainCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, string initialTerrainPath = null)
            : base(renderContext, controlContext, window, eventBus)
        {
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            BaseHeight = 720f;
            _initialTerrainPath = initialTerrainPath;
            _terrainScene = new TerrainCreatorScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new TerrainUIOverlay(this, _renderContext, _controlContext, _window);
        }
        public override void Init()
        {
            base.Init();
            _terrainScene.Initialize((int)Size.Y, (int)Size.X);
            if (!string.IsNullOrEmpty(_initialTerrainPath))
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
        }
        private void OnFileSelected(FileSelectedEvent e)
        {
        }
        public void HandleUIClick(HtmlElement elem)
        {
        }
        public static void OpenBlank(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new TerrainCreatorPanel(renderContext, controlContext, window, eventBus, null);
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
                _subscriptionInitialized = true;
            }
            // Safe path: always use executable directory + create Terrain folder if missing
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string terrainDir = Path.Combine(baseDir, "Assets", "Terrain");
            if (!Directory.Exists(terrainDir))
            {
                Directory.CreateDirectory(terrainDir);
                Console.WriteLine($"[TerrainCreatorPanel] Created missing directory: {terrainDir}");
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
        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased);
            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - TitleHeight);
            _terrainScene.Update(deltaTime, sceneMouse, mouseDown, mousePressed, mouseReleased);
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