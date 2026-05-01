using Keystone;
using MapRoom;
using ReadingChamber;
using SiegeEngine.Core.AssetParsing;
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
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ToolChest;

namespace CastleBuilder
{
    public class SceneEditorPanel : BasePanel, IDataAwarePanel, IOutlinerProvider
    {
        private class SceneEditorUIOverlay : UIOverlay
        {
            private readonly SceneEditorPanel _parent;
            public SceneEditorUIOverlay(SceneEditorPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window) : base(renderContext, controlContext, window) { _parent = parent; }
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
        private bool _cameraMode = false;
        private ModelManager _modelManager;
        private ModelRenderer _modelRenderer;
        public SceneEditorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            DockingMode = DockingMode.IDE;
            BaseHeight = 720f;
            _editorScene = new EditorScene(renderContext, controlContext, window, eventBus);
            _modelManager = new ModelManager(renderContext);
        }
        public string ContentType => "SceneEditor";
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
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelectedForPlacement);
        }
        public void RefreshSceneList()
        {
            UpdateSceneSelectorUI();
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
            if (scenes.Count == 0) options.Append("<option value=\"Main\" selected>Main</option>");
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
            else if (hook == "OpenPlaceEntityBrowser")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx", ".json");
                fileSelector.UserData = "PlaceEntity";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
        }

        private void OnFileSelectedForPlacement(FileSelectedEvent e)
        {
            if (e.UserData?.ToString() != "PlaceEntity" || string.IsNullOrEmpty(e.Path)) return;

            string ext = Path.GetExtension(e.Path).ToLowerInvariant();
            string originalKey = Path.GetFileNameWithoutExtension(e.Path).ToLower();
            string packId = originalKey + "_pack";
            string placeType = "FBX";

            if (ext == ".json")
            {
                placeType = "AssetPack";
                packId = originalKey;
                _modelManager.LoadAnimationPack(e.Path);
            }
            else if (ext == ".fbx")
            {
                packId = _modelManager.RegisterFBXAsPackInMemory(e.Path);
            }

            Vector3 placePos = new Vector3(100f, 100f, 10f);
            var activeField = _editorScene.GetType().GetField("_activeGameScene", BindingFlags.NonPublic | BindingFlags.Instance);
            if (activeField != null)
            {
                var active = activeField.GetValue(_editorScene) as TerrainCreatorScene;
                if (active != null)
                {
                    var flyField = active.GetType().GetField("_flyCamera", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    var fly = flyField?.GetValue(active) as FlyCameraController;
                    if (fly != null)
                    {
                        Vector3 rayOrigin = fly.Position;
                        float yawRad = fly.Yaw * (MathF.PI / 180f);
                        float pitchRad = fly.Pitch * (MathF.PI / 180f);
                        Vector3 rayDir = Vector3.Normalize(new Vector3(
                            MathF.Cos(pitchRad) * MathF.Sin(yawRad),
                            MathF.Cos(pitchRad) * MathF.Cos(yawRad),
                            MathF.Sin(pitchRad)));
                        var rayMethod = active.GetType().GetMethod("RayTerrainIntersect", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (rayMethod != null)
                        {
                            object[] args = new object[] { rayOrigin, rayDir, null };
                            bool hit = (bool)rayMethod.Invoke(active, args);
                            if (hit && args[2] is Vector3 hitPoint)
                            {
                                placePos = hitPoint + new Vector3(0, 0, 0.1f);
                            }
                        }
                    }
                }
            }

            var entity = new Entity { Type = placeType };
            entity.Transform.Position = placePos;

            if (_modelManager.TryGetModel(packId, out var fbxModel) || _modelManager.TryGetModel(originalKey, out fbxModel))
            {
                var modelComp = new ModelComponent { Model = fbxModel, Key = packId };
                entity.AddComponent(modelComp);
            }
            else if (ext == ".json")
            {
                var modelComp = new ModelComponent { Key = packId };
                entity.AddComponent(modelComp);
            }

            var physics = new PhysicsComponent();
            physics.Position = placePos;
            entity.AddComponent(physics);

            // === FIXED: Use proper Level.AddEntity instead of direct list access ===
            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null)
            {
                level.AddEntity(entity);   // this also publishes EntityAddedEvent cleanly
            }
            else
            {
                Console.WriteLine("[SceneEditorPanel] WARNING: No CurrentLevel - entity not persisted to Level");
            }

            var evt = new EntityPlacedEvent(entity.Id, placeType, placePos);
            if (placeType == "FBX") evt.TexturePath = e.Path;
            _eventBus.Publish(evt);

            var serverField = _editorScene.GetType().GetField("_server", BindingFlags.NonPublic | BindingFlags.Instance);
            if (serverField != null)
            {
                var server = serverField.GetValue(_editorScene) as IGameServer;
                server?.AddEntity(entity);
            }

            Console.WriteLine($"[SceneEditorPanel] Placed entity with AssetPackKey: {packId} (render data loaded in memory, added via Level.AddEntity)");
        }

        public void HandleUIClick(HtmlElement elem)
        {
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
            if (isTopmost && mousePressed) OnContentFocusGained();
            base.Update(deltaTime, absMousePos, mouseDown && !_cameraMode, mousePressed && !_cameraMode, mouseReleased && !_cameraMode, scrollDelta);
            float header = HasTitleBar ? HeaderHeight : 0f;
            float contentX = Position.X;
            float contentY = Position.Y + header;
            float contentW = Size.X;
            float contentH = Size.Y - header;
            if (_cameraMode) _controlContext.PushViewport(new Viewport((int)contentX, (int)contentY, (int)contentW, (int)contentH));
            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - TitleHeight);
            _editorScene.Update(deltaTime, sceneMouse, mouseDown && _cameraMode, mousePressed && _cameraMode, mouseReleased && _cameraMode, _cameraMode);
            if (_cameraMode) _controlContext.PopViewport();
        }
        protected override void RenderInnerContent()
        {
            _editorScene.Render(null);
            if (_modelRenderer == null)
            {
                _modelRenderer = new ModelRenderer(_renderContext);
                _modelRenderer.Initialize();
            }
            var entities = _editorScene.GetEntities();
            if (entities == null || entities.Count == 0)
            {
                var serverField = _editorScene.GetType().GetField("_server", BindingFlags.NonPublic | BindingFlags.Instance);
                if (serverField != null)
                {
                    var server = serverField.GetValue(_editorScene) as IGameServer;
                    entities = server?.GetEntities();
                }
            }
            if (entities == null || entities.Count == 0) return;

            var activeField = _editorScene.GetType().GetField("_activeGameScene", BindingFlags.NonPublic | BindingFlags.Instance);
            var active = activeField?.GetValue(_editorScene) as TerrainCreatorScene;
            Matrix4x4 view = Matrix4x4.Identity;
            Vector3 viewPos = Vector3.Zero;
            float aspect = 16f / 9f;
            if (active != null)
            {
                var flyField = active.GetType().GetField("_flyCamera", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var fly = flyField?.GetValue(active) as FlyCameraController;
                if (fly != null)
                {
                    view = fly.ViewMatrix;
                    viewPos = fly.Position;
                }
                var aspectField = active.GetType().GetProperty("AspectRatio", BindingFlags.NonPublic | BindingFlags.Instance);
                if (aspectField != null) aspect = (float)aspectField.GetValue(active);
            }
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 180f * 65f, aspect, 0.1f, 50000f);

            foreach (var entity in entities)
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                var physics = entity.GetComponent<PhysicsComponent>();
                if (modelComp != null && physics != null && !string.IsNullOrEmpty(modelComp.Key))
                {
                    if (_modelManager.TryGetModelData(modelComp.Key, out var modelData))
                    {
                        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(physics.Rotation);
                        Matrix4x4 translation = Matrix4x4.CreateTranslation(physics.Position);
                        Matrix4x4 scaleMat = Matrix4x4.CreateScale(0.01f);
                        Matrix4x4 modelMatrix = scaleMat * rotation * translation;
                        _modelRenderer.RenderModel(modelComp.Model, modelData, view, projection, viewPos, modelMatrix);
                    }
                }
            }
        }
        public override void OnLiveResize(float w, float h)
        {
            _editorScene.Resize((int)w, (int)h);
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
        public string DataKey => "SceneEditorPanel";
        public JsonElement SavePanelState()
        {
            var state = new Dictionary<string, object> { ["currentSceneName"] = _editorScene?.CurrentGameScene ?? "Main" };
            return JsonSerializer.SerializeToElement(state);
        }
        public void LoadPanelState(JsonElement state)
        {
        }
        public override void OnContentFocusGained()
        {
            Console.WriteLine("[SceneEditorPanel] OnContentFocusGained → notifying OutlinerCoordinator");
            OutlinerCoordinator.Instance.SetAsActiveProvider(this, _eventBus);
        }
        public List<OutlinerNode> GetCurrentHierarchy()
        {
            var nodes = new List<OutlinerNode>();
            nodes.Add(new OutlinerNode { Id = "scene-root", Label = "Scene Editor", Icon = "📐", Children = { "entities", "lights", "cameras" } });
            nodes.Add(new OutlinerNode { Id = "entities", Label = "Entities", Icon = "🧱", ParentId = "scene-root" });
            nodes.Add(new OutlinerNode { Id = "lights", Label = "Lights", Icon = "💡", ParentId = "scene-root" });
            nodes.Add(new OutlinerNode { Id = "cameras", Label = "Cameras", Icon = "📹", ParentId = "scene-root" });
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