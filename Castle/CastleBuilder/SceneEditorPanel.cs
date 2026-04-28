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
            string placeType = Path.GetExtension(e.Path).ToLowerInvariant() == ".json" ? "AssetPack" : "FBX";
            Vector3 placePos = new Vector3(100f, 100f, 10f);
            var activeField = _editorScene.GetType().GetField("_activeGameScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (activeField != null)
            {
                var active = activeField.GetValue(_editorScene) as TerrainCreatorScene;
                if (active != null)
                {
                    var flyField = active.GetType().GetField("_flyCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                    if (flyField != null)
                    {
                        var fly = flyField.GetValue(active) as FlyCameraController;
                        if (fly != null)
                        {
                            Vector3 rayOrigin = fly.Position;
                            float yawRad = fly.Yaw * (MathF.PI / 180f);
                            float pitchRad = fly.Pitch * (MathF.PI / 180f);
                            Vector3 rayDir = Vector3.Normalize(new Vector3(
                                MathF.Cos(pitchRad) * MathF.Sin(yawRad),
                                MathF.Cos(pitchRad) * MathF.Cos(yawRad),
                                MathF.Sin(pitchRad)));
                            if (RayTerrainIntersect(active, rayOrigin, rayDir, out Vector3 hit))
                            {
                                placePos = hit + new Vector3(0, 0, 2f);
                            }
                        }
                    }
                }
            }
            var entity = new Entity { Type = placeType };
            entity.Transform.Position = placePos;
            if (placeType == "FBX" && File.Exists(e.Path))
            {
                string key = Path.GetFileNameWithoutExtension(e.Path).ToLower();
                _modelManager.LoadModel(e.Path);
                if (_modelManager.TryGetModel(key, out var fbxModel))
                {
                    entity.AddComponent(new ModelComponent { Model = fbxModel, Key = key });
                }
            }
            var physics = new PhysicsComponent();
            physics.Position = placePos;
            entity.AddComponent(physics);
            var evt = new EntityPlacedEvent(entity.Id, placeType, placePos);
            if (placeType == "FBX") evt.TexturePath = e.Path;
            _eventBus.Publish(evt);
            var serverField = _editorScene.GetType().GetField("_server", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (serverField != null)
            {
                var server = serverField.GetValue(_editorScene) as IGameServer;
                server?.AddEntity(entity);
            }
        }
        private bool RayTerrainIntersect(TerrainCreatorScene scene, Vector3 origin, Vector3 dir, out Vector3 hitPoint)
        {
            hitPoint = Vector3.Zero;
            var heightmapField = scene.GetType().GetField("_heightmap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (heightmapField == null) return false;
            var heightmap = heightmapField.GetValue(scene) as float[,];
            if (heightmap == null) return false;
            var worldScaleXField = scene.GetType().GetField("_worldScaleX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var worldScaleZField = scene.GetType().GetField("_worldScaleZ", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float worldScaleX = worldScaleXField != null ? (float)worldScaleXField.GetValue(scene) : 1f;
            float worldScaleZ = worldScaleZField != null ? (float)worldScaleZField.GetValue(scene) : 1f;
            const float maxDist = 10000f;
            const float step = 1f;
            for (float t = 0; t < maxDist; t += step)
            {
                Vector3 p = origin + dir * t;
                float h = GetHeightFromMap(heightmap, p.X, p.Y, worldScaleX, worldScaleZ);
                if (p.Z <= h)
                {
                    float tLow = t - step;
                    float tHigh = t;
                    for (int i = 0; i < 10; i++)
                    {
                        float tMid = (tLow + tHigh) / 2;
                        p = origin + dir * tMid;
                        h = GetHeightFromMap(heightmap, p.X, p.Y, worldScaleX, worldScaleZ);
                        if (p.Z <= h) tHigh = tMid;
                        else tLow = tMid;
                    }
                    hitPoint = origin + dir * tHigh;
                    return true;
                }
            }
            return false;
        }
        private float GetHeightFromMap(float[,] heightmap, float x, float y, float scaleX, float scaleZ)
        {
            int w = heightmap.GetLength(0);
            int h = heightmap.GetLength(1);
            int ix = (int)Math.Clamp(x / scaleX, 0, w - 1);
            int iy = (int)Math.Clamp(y / scaleZ, 0, h - 1);
            return heightmap[ix, iy];
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
                var serverField = _editorScene.GetType().GetField("_server", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (serverField != null)
                {
                    var server = serverField.GetValue(_editorScene) as IGameServer;
                    entities = server?.GetEntities();
                }
            }
            if (entities == null || entities.Count == 0) return;
            var activeField = _editorScene.GetType().GetField("_activeGameScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var active = activeField?.GetValue(_editorScene) as TerrainCreatorScene;
            Matrix4x4 view = Matrix4x4.Identity;
            Vector3 viewPos = Vector3.Zero;
            if (active != null)
            {
                var flyField = active.GetType().GetField("_flyCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                var fly = flyField?.GetValue(active) as FlyCameraController;
                if (fly != null)
                {
                    view = fly.ViewMatrix;
                    viewPos = fly.Position;
                }
            }
            float aspect = Size.X / Math.Max(Size.Y, 1f);
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 180f * 65f, aspect, 0.1f, 50000f);
            foreach (var entity in entities)
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                var physics = entity.GetComponent<PhysicsComponent>();
                if (modelComp != null && physics != null)
                {
                    string modelKey = modelComp.Key?.ToLower() ?? "man_mesh";
                    if (_modelManager.TryGetModelData(modelKey, out var modelData))
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