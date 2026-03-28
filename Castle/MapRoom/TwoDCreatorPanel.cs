// Folder: MapRoom
// File: TwoDCreatorPanel.cs
using ReadingChamber;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Scenes;
using System;
using System.IO;
using System.Numerics;
using ToolChest;

namespace MapRoom
{
    public class TwoDCreatorPanel : BasePanel
    {
        private class TwoDCreatorUIOverlay : UIOverlay
        {
            private readonly TwoDCreatorPanel _parent;
            public TwoDCreatorUIOverlay(TwoDCreatorPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            public override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }

        private TwoDCreatorScene _twoDScene;
        private bool _cameraMode = false;
        private bool _lastTab = false;
        private int _lastW;
        private int _lastH;

        public override bool WantsContinuousUpdate => true;

        public TwoDCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
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
            var selectEvt = new SelectSpriteEvent(0UL, e.Path, 2f, 2f);
            _eventBus.Publish(selectEvt);
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            var tab = _controlContext.GetKey(_window, Key.Tab);
            if (tab == InputAction.Press && !_lastTab)
            {
                _cameraMode = !_cameraMode;
                _lastTab = true;
                _controlContext.SetInputMode(_window, CursorAttribute.Cursor, _cameraMode ? CursorMode.Disabled : CursorMode.Normal);
            }
            else if (tab != InputAction.Press)
            {
                _lastTab = false;
            }

            base.Update(deltaTime, absMousePos, mouseDown && !_cameraMode, mousePressed && !_cameraMode, mouseReleased && !_cameraMode, scrollDelta);

            // === Consistent content-area rect (same as TerrainCreatorPanel) ===
            float header = HasTitleBar ? HeaderHeight : 0f;
            float contentX = Position.X;
            float contentY = Position.Y + header;
            float contentW = Size.X;
            float contentH = Size.Y - header;

            Vector2 contentMouse = absMousePos - new Vector2(contentX, contentY);

            // === REMOVED the 1.0f - flip (this matches what the scene expects after the refactor) ===
            Vector2 normalizedMouse = new Vector2(
                Math.Clamp(contentMouse.X / contentW, 0f, 1f),
                Math.Clamp(contentMouse.Y / contentH, 0f, 1f)
            );

            bool insideContent = contentMouse.X >= 0 && contentMouse.X <= contentW &&
                                 contentMouse.Y >= 0 && contentMouse.Y <= contentH;

            if (_cameraMode)
            {
                _controlContext.PushViewport(new Viewport((int)contentX, (int)contentY, (int)contentW, (int)contentH));
            }

            Vector3 worldPos = _twoDScene.ScreenToWorldPlane(normalizedMouse, out bool hitPlane);
            _twoDScene.Update(deltaTime, _cameraMode, worldPos, mouseReleased && !_cameraMode && hitPlane && insideContent);

            if (_cameraMode)
            {
                _controlContext.PopViewport();
            }
        }

        public override void Render()
        {
            if (!Visible) return;

            if (_lastW != (int)Size.X || _lastH != (int)Size.Y)
            {
                _lastW = (int)Size.X;
                _lastH = (int)Size.Y;
                _twoDScene.Resize(_lastW, _lastH);
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }

            // === Consistent content-area rect for scissor ===
            float header = HasTitleBar ? HeaderHeight : 0f;
            float contentX = Position.X;
            float contentY = Position.Y + header;
            float contentW = Size.X;
            float contentH = Size.Y - header;

            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            _renderContext.Enable(_renderContext.Enums.ScissorTest);
            int scissorX = (int)contentX;
            int scissorY = winH - (int)(contentY + contentH);
            uint scissorW = (uint)contentW;
            uint scissorH = (uint)contentH;
            _renderContext.Scissor(scissorX, scissorY, scissorW, scissorH);

            _twoDScene.Render(_twoDScene.GetEntities());
            _renderContext.Disable(_renderContext.Enums.ScissorTest);
            base.Render();
        }

        public override void OnLiveResize(float w, float h)
        {
            _twoDScene.Resize((int)w, (int)h);
        }

        public override void Dispose()
        {
            _twoDScene?.Dispose();
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new TwoDCreatorPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }
    }
}