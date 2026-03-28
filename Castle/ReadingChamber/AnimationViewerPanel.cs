// Folder: ReadingChamber
// File: AnimationViewerPanel.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ReadingChamber
{
    public unsafe class AnimationViewerPanel : BasePanel
    {
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AnimationViewerPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }

        private class AssetUIOverlay : UIOverlay
        {
            private readonly AnimationViewerPanel _parent;
            public AssetUIOverlay(AnimationViewerPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window) : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            public override void HandleUIClick(HtmlElement elem)
            {
                base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
            }
            protected override void HandleDataHook(string hook)
            {
                _parent.HandleDataHook(hook);
            }
        }

        private ModelViewerScene _viewerScene;
        private EditorTextRenderer _textRenderer;
        private ShaderProgram _textShader;
        private List<string> _animationFiles = new List<string>();

        public AnimationViewerPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            BaseHeight = 720f;
            _viewerScene = new ModelViewerScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new AssetUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            _textShader = new ShaderProgram(_renderContext, TextShader.VertexShaderSource, TextShader.FragmentShaderSource);
            _textRenderer = new EditorTextRenderer(_renderContext, _window);
            _textRenderer.Initialize(_textShader);
            _viewerScene.Initialize((int)Size.Y, (int)Size.X);
            _animationFiles = _viewerScene.GetAnimationFiles();
            UpdateUIControls();
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void UpdateUIControls()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AssetViewerUI.html");
            if (!File.Exists(htmlPath))
            {
                return;
            }
            string baseHtml = File.ReadAllText(htmlPath);
            int insertIndex = baseHtml.IndexOf("<!--insert here-->");
            if (insertIndex == -1)
            {
                return;
            }
            StringBuilder dynamicSelect = new StringBuilder();
            dynamicSelect.Append("<select id=\"animSelect\" data-hook=\"AnimSelectChanged\" style=\"\">");
            foreach (var file in _animationFiles)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                dynamicSelect.Append($"<option value=\"{file}\">{name}</option>");
            }
            dynamicSelect.Append("</select>");
            string modifiedHtml = baseHtml.Insert(insertIndex, dynamicSelect.ToString());
            _uiOverlay.LoadUI(modifiedHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void OnFileSelected(FileSelectedEvent e)
        {
            string hook = e.UserData as string;
            if (hook == "LoadMesh")
            {
                _viewerScene.LoadMesh(e.Path);
                _viewerScene.DiscoverAnimationFiles();
                _animationFiles = _viewerScene.GetAnimationFiles();
                UpdateUIControls();
                _uiOverlay.RefreshUI();
            }
            else if (hook == "LoadArmature")
            {
                _viewerScene.LoadArmature(e.Path);
            }
            else if (hook == "LoadAnimation")
            {
                _viewerScene.LoadAnimation(e.Path);
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "TogglePlay")
            {
                _viewerScene.TogglePlay();
            }
            else if (hook == "LoadMesh")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = "LoadMesh";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
            else if (hook == "LoadArmature")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = "LoadArmature";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
            else if (hook == "LoadAnimation")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = "LoadAnimation";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
        }

        private void HandleDataHook(string hook)
        {
            if (hook == "AnimSelectChanged")
            {
                var select = _uiOverlay.FindElementById("animSelect") as SelectElement;
                if (select != null)
                {
                    string val = select.Value;
                    _viewerScene.LoadAnimation(val);
                }
            }
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - HeaderHeight);
            _viewerScene.Update(deltaTime, sceneMouse, mouseDown, mousePressed, mouseReleased);
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
                _viewerScene.Resize(_lastW, _lastH);
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            _renderContext.Enable(_renderContext.Enums.ScissorTest);
            int scissorX = (int)Position.X;
            int scissorY = winH - (int)(Position.Y + Size.Y);
            uint scissorW = (uint)Size.X;
            uint scissorH = (uint)Size.Y;
            _renderContext.Scissor(scissorX, scissorY, scissorW, scissorH);
            _viewerScene.Render(null);
            _renderContext.Disable(_renderContext.Enums.ScissorTest);
            base.Render();
            string frameInfo = _viewerScene.GetFrameInfo();
            _textRenderer.RenderText(frameInfo, 10, HeaderHeight + 10, (int)Size.X, (int)Size.Y, 12f);
        }

        public override void OnLiveResize(float w, float h)
        {
            _viewerScene.Resize((int)w, (int)h);
        }

        public override void Dispose()
        {
            _viewerScene.Dispose();
            _textRenderer.Dispose();
            _textShader.Dispose();
            base.Dispose();
        }
    }
}