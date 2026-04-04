// Folder: CastleBuilder
// File: SceneEditorPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Scenes;
using System;
using System.IO;
using System.Numerics;

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
                _parent.HandleUIClick(elem);
                return true;
            }
        }

        private EditorScene _editorScene;

        public SceneEditorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            Scaling = ScalingMode.BestFit;
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
            LoadUIControls();
        }

        private void LoadUIControls()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SceneEditorUI.html");
            if (File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook.StartsWith("SwitchGameScene:"))
            {
                string sceneName = hook.Substring(16);
                _editorScene.SwitchGameScene(sceneName);
            }
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            _editorScene.Update(deltaTime);
        }

        protected override void RenderInnerContent()
        {
            _editorScene.Render(null);
        }

        public override void OnLiveResize(float w, float h)
        {
            _editorScene.Resize((int)w, (int)h);
        }

        public override void Dispose()
        {
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