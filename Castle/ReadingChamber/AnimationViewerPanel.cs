// Folder: ReadingChamber
// File: AnimationViewerPanel.cs
using Keystone;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
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
using System.Text.Json;

namespace ReadingChamber
{
    public unsafe class AnimationViewerPanel : BasePanel, IDataAwarePanel, IOutlinerProvider
    {
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AnimationViewerPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }

        private class AssetUIOverlay : UIOverlay
        {
            private readonly AnimationViewerPanel _parent;

            public AssetUIOverlay(AnimationViewerPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
                : base(renderContext, controlContext, window, eventBus)
            {
                _parent = parent;
            }

            public override bool HandleUIClick(HtmlElement elem)
            {
                bool coreHandled = base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
                return coreHandled;
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
            DockingMode = DockingMode.IDE;
            BaseWidth = 900f;
            BaseHeight = 620f;
            _viewerScene = new ModelViewerScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }

        public string ContentType => "AnimationViewer";

        protected override UIOverlay CreateUIOverlay()
        {
            return new AssetUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);
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

            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);

            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void OnGenericEvent(GenericEvent e)
        {
            if (e.Hook == "AnimSelectChanged")
            {
                var select = _uiOverlay.FindElementById("animSelect") as SelectElement;
                if (select != null && !string.IsNullOrEmpty(select.Value))
                {
                    _viewerScene.LoadAnimation(select.Value);
                }
            }
            else if (e.Hook == "TogglePlay")
            {
                _viewerScene.TogglePlay();
            }
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
                NotifyHierarchyChanged();
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

        private void UpdateUIControls()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AssetViewerUI.html");
            if (!File.Exists(htmlPath)) return;
            string baseHtml = File.ReadAllText(htmlPath);
            int insertIndex = baseHtml.IndexOf("<!--insert here-->");
            if (insertIndex == -1) return;
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

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "LoadMesh" || hook == "LoadArmature" || hook == "LoadAnimation")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = hook;
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
        }

        public override bool WantsContinuousUpdate => true;

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - HeaderHeight);
            _viewerScene.Update(deltaTime, sceneMouse, mouseDown, mousePressed, mouseReleased);
        }

        protected override void RenderInnerContent()
        {
            _viewerScene.Render(null);
        }

        public override void OnLiveResize(float w, float h)
        {
            _viewerScene.Resize((int)w, (int)h);
            base.OnLiveResize(w, h);
        }

        public override void Dispose()
        {
            _viewerScene.Dispose();
            _textRenderer.Dispose();
            _textShader.Dispose();
            base.Dispose();
        }

        public string DataKey => "AnimationViewerPanel";

        public JsonElement SavePanelState()
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object>());
        }

        public void LoadPanelState(JsonElement state)
        {
        }

        public override void OnContentFocusGained()
        {
            Console.WriteLine("[AnimationViewerPanel] OnContentFocusGained → notifying OutlinerCoordinator");
            OutlinerCoordinator.Instance.SetAsActiveProvider(this, _eventBus);
        }

        public List<OutlinerNode> GetCurrentHierarchy()
        {
            var nodes = new List<OutlinerNode>();

            var root = new OutlinerNode { Id = "anim-root", Label = "Animation Viewer", Icon = "🎬" };
            nodes.Add(root);

            var modelNode = new OutlinerNode
            {
                Id = "model",
                Label = "Model",
                Icon = "📦",
                ParentId = "anim-root",
                AssociatedObject = _viewerScene  // real scene object for Model node
            };
            root.Children.Add("model");
            nodes.Add(modelNode);

            var skeletonNode = new OutlinerNode
            {
                Id = "skeleton",
                Label = "Skeleton",
                Icon = "🦴",
                ParentId = "anim-root",
                AssociatedObject = _viewerScene  // real scene object for Skeleton node
            };
            root.Children.Add("skeleton");
            nodes.Add(skeletonNode);

            var animationsNode = new OutlinerNode
            {
                Id = "animations",
                Label = "Animations",
                Icon = "⏯️",
                ParentId = "anim-root",
                AssociatedObject = _animationFiles   // actual list of animation files for Animations node
            };
            root.Children.Add("animations");
            nodes.Add(animationsNode);

            return nodes;
        }

        public object GetObjectForNode(string nodeId)
        {
            // Return the exact object attached to the clicked node
            var node = GetCurrentHierarchy().FirstOrDefault(n => n.Id == nodeId);
            return node?.AssociatedObject ?? _viewerScene;
        }

        public void NotifyHierarchyChanged()
        {
            OutlinerCoordinator.Instance.NotifyHierarchyChanged();
        }
    }
}