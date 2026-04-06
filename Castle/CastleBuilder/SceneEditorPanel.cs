// Folder: CastleBuilder
// File: SceneEditorPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Scenes;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace CastleBuilder
{
    public class SceneEditorPanel : BasePanel
    {
        private class SceneEditorUIOverlay : UIOverlay
        {
            private readonly SceneEditorPanel _parent;

            public SceneEditorUIOverlay(SceneEditorPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }

            public override bool HandleUIClick(HtmlElement elem)
            {
                base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
                return true;
            }

            protected override void HandleDataHook(string hook)
            {
                _parent.HandleDataHook(hook);
            }
        }

        private EditorScene _editorScene;
        private bool _cameraMode = false;   // start in UI mode — no auto-capture
        private int _lastW;
        private int _lastH;

        public SceneEditorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            Scaling = ScalingMode.BestFit;   // same as TerrainCreatorPanel
            BaseWidth = 1280f;
            DockingMode = DockingMode.IDE;
            BaseHeight = 720f;

            _editorScene = new EditorScene(renderContext, controlContext, window, eventBus);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new SceneEditorUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            _editorScene.Initialize((int)Size.X, (int)Size.Y);

            _editorScene.LoadProjectData();

            UpdateSceneSelectorUI();

            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void UpdateSceneSelectorUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SceneEditorUI.html");
            if (!File.Exists(htmlPath)) return;

            string baseHtml = File.ReadAllText(htmlPath);

            var scenes = _editorScene.GetAvailableScenes();
            string current = _editorScene.CurrentGameScene ?? "Main";

            StringBuilder options = new StringBuilder();
            foreach (var sceneName in scenes)
            {
                string selected = (sceneName == current) ? " selected" : "";
                options.Append($"<option value=\"{sceneName}\"{selected}>{sceneName}</option>");
            }
            if (scenes.Count == 0)
                options.Append("<option value=\"Main\" selected>Main</option>");

            string finalHtml = baseHtml.Replace("<!-- Populated dynamically -->", options.ToString());

            _uiOverlay.LoadUI(finalHtml);
        }

        private void HandleDataHook(string hook)
        {
            if (hook == "SceneSelected")
            {
                var select = _uiOverlay.FindElementById("sceneSelect") as SelectElement;
                if (select != null && !string.IsNullOrEmpty(select.Value))
                {
                    _editorScene.SwitchGameScene(select.Value);
                    UpdateSceneSelectorUI();
                }
            }
            else if (hook == "CreateNewScene")
            {
                MenuCommands.CreateNewScene(_renderContext, _controlContext, _window, _eventBus);
                UpdateSceneSelectorUI();
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            // reserved for future buttons
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

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            // EXACT same pattern as TerrainCreatorPanel (this is what prevents stretching)
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
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - TitleHeight);

            _editorScene.Update(deltaTime, sceneMouse, mouseDown && _cameraMode, mousePressed && _cameraMode, mouseReleased && _cameraMode, _cameraMode);

            if (_cameraMode)
            {
                _controlContext.PopViewport();
            }
        }

        protected override void RenderInnerContent()
        {
            _editorScene.Render(null);
        }

        public override void OnLiveResize(float w, float h)
        {
            _editorScene.Resize((int)w, (int)h);   // propagate exact size to inner scene
            base.OnLiveResize(w, h);
        }

        public override void Dispose()
        {
            PanelManager.Current.ReleasePanelCapture();
            _editorScene?.Dispose();
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new SceneEditorPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }
    }
}