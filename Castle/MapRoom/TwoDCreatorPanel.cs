// Folder: MapRoom
// File: TwoDCreatorPanel.cs
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
using ToolChest;
namespace MapRoom
{
    public class TwoDCreatorPanel : ClosablePanel
    {
        private TwoDCreatorScene _twoDScene;
        private bool _cameraMode = true;
        private bool _lastTab = false;

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
            return new UIOverlay(_renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            _twoDScene.Initialize((int)Size.Y, (int)Size.X);
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
            _twoDScene.Update(deltaTime);
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
            _twoDScene.Render(null);
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