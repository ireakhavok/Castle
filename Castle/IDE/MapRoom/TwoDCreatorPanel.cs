// Folder: MapRoom
// File: TwoDCreatorPanel.cs
using Keystone;
using ReadingChamber;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Scenes;
using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ToolChest;

namespace MapRoom
{
    public class TwoDCreatorPanel : BasePanel, IDataAwarePanel, IOutlinerProvider
    {
        private class TwoDCreatorUIOverlay : UIOverlay
        {
            private readonly TwoDCreatorPanel _parent;
            public TwoDCreatorUIOverlay(TwoDCreatorPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window) : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            public override bool HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
                return true;
            }
        }
        private TwoDCreatorScene _twoDScene;
        private bool _cameraMode = false;

        public TwoDCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            BaseHeight = 720f;
            DockingMode = DockingMode.IDE;
            _twoDScene = new TwoDCreatorScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
        }

        public string ContentType => "TwoDCreator";

        protected override UIOverlay CreateUIOverlay()
        {
            return new TwoDCreatorUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            _controlContext.SetMainWindow(_window);
            _twoDScene.Initialize((int)Size.X, (int)Size.Y);
            LoadUI();
        }

        private void LoadUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TwoDCreatorPanelUI.html");
            if (File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        public void HandleDataHook(string hook)
        {
            if (hook == "OpenSpriteTool")
            {
                SpritePlacementPanel.Open(_renderContext, _controlContext, _window, _eventBus);
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
            if (string.IsNullOrEmpty(e.Path) || !e.Path.ToLower().EndsWith(".png")) return;
            var selectEvt = new SelectSpriteEvent(0, e.Path, 2f, 2f);
            _eventBus.Publish(selectEvt);
        }

        public override void ToggleCameraMode()
        {
            _cameraMode = !_cameraMode;
            if (_cameraMode) PanelManager.Current.CapturePanel(this);
            else PanelManager.Current.ReleasePanelCapture();
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            bool isTopmost = PanelManager.Current?.GetTopmostPanelAt(absMousePos) == this;
            if (isTopmost && mousePressed)
                OnContentFocusGained();

            base.Update(deltaTime, absMousePos, mouseDown && !_cameraMode, mousePressed && !_cameraMode, mouseReleased && !_cameraMode, scrollDelta);

            float header = HasTitleBar ? HeaderHeight : 0f;
            float contentX = Position.X;
            float contentY = Position.Y + header;
            float contentW = Size.X;
            float contentH = Size.Y - header;
            Vector2 contentMouse = absMousePos - new Vector2(contentX, contentY);
            Vector2 normalizedMouse = new Vector2(Math.Clamp(contentMouse.X / contentW, 0f, 1f), Math.Clamp(contentMouse.Y / contentH, 0f, 1f));
            bool insideContent = contentMouse.X >= 0 && contentMouse.X <= contentW && contentMouse.Y >= 0 && contentMouse.Y <= contentH;
            if (_cameraMode) _controlContext.PushViewport(new Viewport((int)contentX, (int)contentY, (int)contentW, (int)contentH));
            Vector3 worldPos = _twoDScene.ScreenToWorldPlane(normalizedMouse, out bool hitPlane);
            _twoDScene.Update(deltaTime, _cameraMode, worldPos, mouseReleased && !_cameraMode && hitPlane && insideContent);
            if (_cameraMode) _controlContext.PopViewport();
        }

        protected override void RenderInnerContent()
        {
            _twoDScene.Render(_twoDScene.GetEntities());
        }

        public override void OnLiveResize(float w, float h)
        {
            _twoDScene.Resize((int)w, (int)h);
        }

        public override void Dispose()
        {
            PanelManager.Current.ReleasePanelCapture();
            _twoDScene?.Dispose();
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new TwoDCreatorPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }

        public string DataKey => "TwoDCreatorPanel";

        public JsonElement SavePanelState()
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object>());
        }

        public void LoadPanelState(JsonElement state)
        {
        }

        public override void OnContentFocusGained()
        {
            Console.WriteLine("[TwoDCreatorPanel] OnContentFocusGained → notifying OutlinerCoordinator");
            OutlinerCoordinator.Instance.SetAsActiveProvider(this, _eventBus);
        }

        public List<OutlinerNode> GetCurrentHierarchy()
        {
            var nodes = new List<OutlinerNode>();
            nodes.Add(new OutlinerNode { Id = "2d-root", Label = "2D Scene", Icon = "🖼️", Children = { "sprites", "layers" } });
            nodes.Add(new OutlinerNode { Id = "sprites", Label = "Sprites", Icon = "🌟", ParentId = "2d-root" });
            nodes.Add(new OutlinerNode { Id = "layers", Label = "Layers", Icon = "📚", ParentId = "2d-root" });
            return nodes;
        }

        public object GetObjectForNode(string nodeId)
        {
            return null;
        }

        public void NotifyHierarchyChanged()
        {
            OutlinerCoordinator.Instance.NotifyHierarchyChanged();
        }
    }
}