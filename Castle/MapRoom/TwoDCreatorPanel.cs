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
    public class TwoDCreatorPanel : ClosablePanel
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
        public override bool WantsContinuousUpdate => true;

        public TwoDCreatorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            BaseHeight = 720f;
            _twoDScene = new TwoDCreatorScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new TwoDCreatorUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
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

            var contentRect = GetContentRect();
            bool insideContent = absMousePos.X >= contentRect.X && absMousePos.X <= contentRect.X + contentRect.Width &&
                                 absMousePos.Y >= contentRect.Y && absMousePos.Y <= contentRect.Y + contentRect.Height;

            if (!insideContent)
            {
                _twoDScene.Update(deltaTime, _cameraMode, Vector3.Zero, false);
                return;
            }

            Vector2 contentMouse = absMousePos - new Vector2(contentRect.X, contentRect.Y);

            Vector2 normalizedMouse = new Vector2(
                contentMouse.X / contentRect.Width,
                contentMouse.Y / contentRect.Height
            );

            Vector3 worldPos = _twoDScene.ScreenToWorldPlane(normalizedMouse, out bool hitPlane);

            _twoDScene.Update(deltaTime, _cameraMode, worldPos, mouseReleased && !_cameraMode && hitPlane);
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
                _twoDScene.Resize(_lastW, _lastH);
            }
            var contentRect = GetContentRect();
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            _renderContext.Enable(_renderContext.Enums.ScissorTest);
            int scissorX = (int)contentRect.X;
            int scissorY = winH - (int)(contentRect.Y + contentRect.Height);
            uint scissorW = (uint)contentRect.Width;
            uint scissorH = (uint)contentRect.Height;
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