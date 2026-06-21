// Folder: IDE
// File: SceneEditorPanel.cs
using Keystone;
using MapRoom;
using ReadingChamber;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private class SelectionBoxOverlay : ICustomOverlay
        {
            private readonly SceneEditorPanel _parent;
            public SelectionBoxOverlay(SceneEditorPanel parent) { _parent = parent; }
            public void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight)
            {
                if (!_parent._isBoxSelecting) return;
                float dragDist = Vector2.Distance(_parent._boxStart, _parent._boxEnd);
                if (dragDist < SceneEditorPanel.MinDragDistance) return;
                float headerHeight = _parent.HasTitleBar ? _parent.HeaderHeight : 0f;
                float x = Math.Min(_parent._boxStart.X, _parent._boxEnd.X);
                float y = Math.Min(_parent._boxStart.Y, _parent._boxEnd.Y);
                float w = Math.Abs(_parent._boxEnd.X - _parent._boxStart.X);
                float h = Math.Abs(_parent._boxEnd.Y - _parent._boxStart.Y);
                quadRenderer.DrawQuad(x, y + headerHeight, w, h, new Vector4(0.2f, 0.6f, 1f, 0.3f), panelWidth, panelHeight);
                Vector4 borderColor = new Vector4(0.2f, 0.6f, 1f, 1f);
                quadRenderer.DrawLine(x, y + headerHeight, x + w, y + headerHeight, 2f, borderColor, panelWidth, panelHeight);
                quadRenderer.DrawLine(x, y + headerHeight + h, x + w, y + headerHeight + h, 2f, borderColor, panelWidth, panelHeight);
                quadRenderer.DrawLine(x, y + headerHeight, x, y + headerHeight + h, 2f, borderColor, panelWidth, panelHeight);
                quadRenderer.DrawLine(x + w, y + headerHeight, x + w, y + headerHeight + h, 2f, borderColor, panelWidth, panelHeight);
            }
        }
        private EditorScene _editorScene;
        private bool _cameraMode = false;
        private ModelManager _modelManager;
        private ModelRenderer _modelRenderer;
        private bool _pendingSceneSelectorUpdate = false;
        private List<int> _selectedEntityIds = new List<int>();
        private bool _wasRightPressedLastFrame = false;
        private bool _isBoxSelecting = false;
        private Vector2 _boxStart = Vector2.Zero;
        private Vector2 _boxEnd = Vector2.Zero;
        private const float MinDragDistance = 5f;
        private TransformGizmoOverlay _transformGizmo;
        private bool _fileSelectedSubscribed = false;
        public SceneEditorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            DockingMode = DockingMode.IDE;
            BaseHeight = 720f;
            _editorScene = new EditorScene(renderContext, controlContext, window, eventBus);
            _modelManager = ModelManager.Instance ?? new ModelManager(renderContext);
            CustomOverlays.Add(new SelectionBoxOverlay(this));
            _transformGizmo = new TransformGizmoOverlay(
                renderContext,
                eventBus,
                (contentMouse, contentW, contentH) =>
                {
                    var active = _editorScene.GetActiveGameScene() as TerrainCreatorScene;
                    if (active == null) return (Vector3.Zero, Vector3.Zero, false);
                    Vector3 origin = Vector3.Zero;
                    Vector3 dir = Vector3.Zero;
                    bool success = active.GetMouseRay(contentMouse / new Vector2(contentW, contentH), out origin, out dir);
                    return (origin, dir, success);
                },
                id => _editorScene.GetEntityById(id)
            );
            CustomOverlays.Add(_transformGizmo);
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
            if (!_fileSelectedSubscribed)
            {
                _eventBus.Subscribe<FileSelectedEvent>(OnFileSelectedForPlacement);
                _fileSelectedSubscribed = true;
            }
            _eventBus.Subscribe<EntitySelectedEvent>(OnEntitySelected);
        }
        private void OnEntitySelected(EntitySelectedEvent e)
        {
            if (e.Additive)
            {
                foreach (var id in e.SelectedEntityIds)
                {
                    if (!_selectedEntityIds.Contains(id))
                        _selectedEntityIds.Add(id);
                }
            }
            else
            {
                _selectedEntityIds = new List<int>(e.SelectedEntityIds);
            }
            var nodeIds = _selectedEntityIds.Select(id => $"entity-{id}").ToList();
            OutlinerCoordinator.Instance.SaveSelectedState(ContentType, nodeIds);
            if (nodeIds.Count > 0)
                OutlinerCoordinator.Instance.NotifySelectionChanged(nodeIds[0]);
            else
                OutlinerCoordinator.Instance.NotifySelectionChanged("");
            if (_selectedEntityIds.Count == 1)
            {
                var entity = _editorScene.GetEntityById(_selectedEntityIds[0]);
                if (entity != null)
                {
                    var physics = entity.GetComponent<PhysicsComponent>();
                    if (physics != null)
                    {
                        _transformGizmo.OnEntitySelected(_selectedEntityIds[0], physics.Position, physics.Rotation);
                    }
                }
            }
            else
            {
                _transformGizmo.ClearSelection();
            }
            NotifyHierarchyChanged();
        }
        public void RefreshSceneList()
        {
            UpdateSceneSelectorUI();
        }
        private void UpdateSceneSelectorUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SceneEditorUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine("[SceneEditorPanel] WARNING: SceneEditorUI.html not found");
                return;
            }
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
            {
                options.Append("<option value=\"Main\" selected>Main</option>");
            }
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
                    string newScene = select.Value.Trim();
                    string oldScene = _editorScene.CurrentGameScene ?? "Main";
                    if (newScene != oldScene)
                    {
                        _editorScene.SwitchGameScene(newScene);
                        _pendingSceneSelectorUpdate = true;
                    }
                }
                return;
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
            if (!_editorScene.TryGetPlacementPosition(out var hitPoint))
            {
                Console.WriteLine("[SceneEditorPanel.OnFileSelectedForPlacement] Raycast failed - no valid placement position (aborting to prevent erroneous default entity)");
                return;
            }
            Vector3 placePos = hitPoint + new Vector3(0, 0, 0.1f);
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null)
            {
                string sceneName = _editorScene.CurrentGameScene ?? "Main";
                level = new Level(_eventBus) { Name = sceneName };
                ProjectSettings.Current.SetCurrentLevel(level);
            }
            var entity = level.PlaceEntity(placePos, placeType);
            entity.Type = placeType;
            if (_modelManager.TryGetModel(packId, out var fbxModel) || _modelManager.TryGetModel(originalKey, out fbxModel))
            {
                var modelComp = new ModelComponent { Model = fbxModel, Key = packId };
                entity.AddComponent(modelComp);
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null && modelComp.Model != null)
                {
                    physics.Size = modelComp.Model.GetBoundingSize();
                    physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                    physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
                }
            }
            else if (ext == ".json")
            {
                var modelComp = new ModelComponent { Key = packId };
                entity.AddComponent(modelComp);
            }
            Console.WriteLine($"[SceneEditorPanel.OnFileSelectedForPlacement] Placed entity ID={entity.Id} AssetPackKey='{packId}' at {placePos}");
            _editorScene.SyncCurrentLevelToRuntimeServer();
        }
        public void HandleUIClick(HtmlElement elem)
        {
        }
        public override void ToggleCameraMode()
        {
            _cameraMode = !_cameraMode;
            if (_cameraMode) PanelManager.Current.CapturePanel(this);
            else PanelManager.Current.ReleasePanelCapture();
            if (_cameraMode)
                _transformGizmo.ClearSelection();
        }
        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            if (_pendingSceneSelectorUpdate)
            {
                _pendingSceneSelectorUpdate = false;
                UpdateSceneSelectorUI();
            }
            bool isTopmost = PanelManager.Current?.GetTopmostPanelAt(absMousePos) == this;
            bool ctrlPressed = _controlContext.GetKey(_window, Key.LeftControl) == InputAction.Press ||
                               _controlContext.GetKey(_window, Key.RightControl) == InputAction.Press;
            bool rightPressedThisFrame = _controlContext.GetMouseButton(_window, MouseButton.Right) == InputAction.Press;
            if (isTopmost && rightPressedThisFrame && !_wasRightPressedLastFrame)
            {
                float header = HasTitleBar ? HeaderHeight : 0f;
                _isBoxSelecting = true;
                _boxStart = new Vector2(absMousePos.X - Position.X, absMousePos.Y - Position.Y - header);
                _boxEnd = _boxStart;
            }
            if (_isBoxSelecting && _controlContext.GetMouseButton(_window, MouseButton.Right) == InputAction.Press)
            {
                float header = HasTitleBar ? HeaderHeight : 0f;
                _boxEnd = new Vector2(absMousePos.X - Position.X, absMousePos.Y - Position.Y - header);
            }
            if (_isBoxSelecting && _controlContext.GetMouseButton(_window, MouseButton.Right) == InputAction.Release)
            {
                _isBoxSelecting = false;
                float dragDist = Vector2.Distance(_boxStart, _boxEnd);
                if (dragDist >= MinDragDistance)
                {
                    PerformBoxSelection(ctrlPressed);
                }
                else
                {
                    float headerHeight = HasTitleBar ? HeaderHeight : 0f;
                    Vector2 contentMouse = new Vector2(absMousePos.X - Position.X, absMousePos.Y - Position.Y - headerHeight);
                    float contentW = Size.X;
                    float contentH = Size.Y - headerHeight;
                    Vector2 normalizedMouse = new Vector2(
                        Math.Clamp(contentMouse.X / contentW, 0f, 1f),
                        Math.Clamp(contentMouse.Y / contentH, 0f, 1f)
                    );
                    if (_editorScene.TryPerformEntitySelectionRaycast(normalizedMouse, contentW, contentH, out int entityId, out Vector3 hitPoint, ctrlPressed))
                    {
                        var evt = new EntitySelectedEvent(entityId, hitPoint, ctrlPressed);
                        _eventBus.Publish(evt);
                    }
                    else
                    {
                        _selectedEntityIds.Clear();
                        NotifyHierarchyChanged();
                    }
                }
            }
            _wasRightPressedLastFrame = rightPressedThisFrame;
            // Compute fresh matrices BEFORE gizmo input (using reflection for AspectRatio)
            Matrix4x4 view = Matrix4x4.Identity;
            Matrix4x4 projection = Matrix4x4.Identity;
            var activeField = _editorScene.GetType().GetField("_activeGameScene", BindingFlags.NonPublic | BindingFlags.Instance);
            var active = activeField?.GetValue(_editorScene) as TerrainCreatorScene;
            if (active != null)
            {
                var flyField = active.GetType().GetField("_flyCamera", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var fly = flyField?.GetValue(active) as FlyCameraController;
                if (fly != null)
                {
                    view = fly.ViewMatrix;
                }
                float aspect = 16f / 9f;
                var aspectField = active.GetType().GetProperty("AspectRatio", BindingFlags.NonPublic | BindingFlags.Instance);
                if (aspectField != null)
                {
                    aspect = (float)aspectField.GetValue(active);
                }
                projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 180f * 65f, aspect, 0.1f, 50000f);
            }
            _transformGizmo.UpdateMatrices(view, projection);
            // Gizmo input handling
            if (!_cameraMode && isTopmost)
            {
                float headerHeight = HasTitleBar ? HeaderHeight : 0f;
                Vector2 contentMouse = new Vector2(absMousePos.X - Position.X, absMousePos.Y - Position.Y - headerHeight);
                float contentW = Size.X;
                float contentH = Size.Y - headerHeight;
                _transformGizmo.HandleMouseInput(contentMouse, contentW, contentH, mouseDown, mousePressed, mouseReleased);
            }
            base.Update(deltaTime, absMousePos, mouseDown && !_cameraMode, mousePressed && !_cameraMode, mouseReleased && !_cameraMode, scrollDelta);
            if (_cameraMode)
            {
                Vector2 sceneMouse = absMousePos - Position - new Vector2(0, TitleHeight);
                _editorScene.Update(deltaTime, sceneMouse, mouseDown && _cameraMode, mousePressed && _cameraMode, mouseReleased && _cameraMode, _cameraMode);
            }
        }
        private void PerformBoxSelection(bool additive)
        {
            float header = HasTitleBar ? HeaderHeight : 0f;
            float contentW = Size.X;
            float contentH = Size.Y - header;
            Vector2 startNdc = new Vector2(
                Math.Clamp(_boxStart.X / contentW, 0f, 1f),
                Math.Clamp(_boxStart.Y / contentH, 0f, 1f)
            );
            Vector2 endNdc = new Vector2(
                Math.Clamp(_boxEnd.X / contentW, 0f, 1f),
                Math.Clamp(_boxEnd.Y / contentH, 0f, 1f)
            );
            var selected = _editorScene.PerformBoxSelection(startNdc, endNdc, contentW, contentH);
            var evt = new EntitySelectedEvent { SelectedEntityIds = selected, Additive = additive };
            _eventBus.Publish(evt);
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
                    FBXModel fbxModel = modelComp.Model;
                    if (fbxModel == null)
                    {
                        if (ModelManager.Instance.TryGetModel(modelComp.Key, out fbxModel))
                        {
                            modelComp.Model = fbxModel;
                            if (physics != null && modelComp.Model != null)
                            {
                                physics.Size = modelComp.Model.GetBoundingSize();
                                physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                                physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
                            }
                        }
                    }
                    if (fbxModel != null && _modelManager.TryGetModelData(modelComp.Key, out var modelData))
                    {
                        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(physics.Rotation);
                        Matrix4x4 translation = Matrix4x4.CreateTranslation(physics.Position);
                        Matrix4x4 scaleMat = Matrix4x4.CreateScale(0.01f);
                        Matrix4x4 modelMatrix = scaleMat * rotation * translation;
                        _modelRenderer.RenderModel(fbxModel, modelData, view, projection, viewPos, modelMatrix);
                    }
                }
            }
            if (_transformGizmo != null)
            {
                _transformGizmo.RenderWorld(view, projection);
            }
        }
        public override void OnLiveResize(float w, float h)
        {
            _editorScene.Resize((int)w, (int)h);
            base.OnLiveResize(w, h);
        }
        public override void Dispose()
        {
            if (_fileSelectedSubscribed)
            {
                _eventBus.Unsubscribe<FileSelectedEvent>(OnFileSelectedForPlacement);
                _fileSelectedSubscribed = false;
            }
            PanelManager.Current.ReleasePanelCapture();
            _editorScene?.Dispose();
            base.Dispose();
        }
        public string DataKey => "SceneEditorPanel";
        public JsonElement SavePanelState()
        {
            var state = new Dictionary<string, string> { ["currentSceneName"] = _editorScene?.CurrentGameScene ?? "Main" };
            return JsonSerializer.SerializeToElement(state);
        }
        public void LoadPanelState(JsonElement state)
        {
            if (state.TryGetProperty("currentSceneName", out JsonElement sceneNameElem))
            {
                string savedScene = sceneNameElem.GetString();
                if (!string.IsNullOrEmpty(savedScene))
                {
                    _editorScene.SwitchGameScene(savedScene);
                    _pendingSceneSelectorUpdate = true;
                }
            }
        }
        public override void OnContentFocusGained()
        {
            OutlinerCoordinator.Instance.SetAsActiveProvider(this, _eventBus);
        }
        public List<OutlinerNode> GetCurrentHierarchy()
        {
            var nodes = new List<OutlinerNode>();
            var level = ProjectSettings.Current.CurrentLevel;
            var root = new OutlinerNode
            {
                Id = "root",
                Label = $"Scene: {level?.Name ?? _editorScene.CurrentGameScene ?? "Untitled"}",
                Icon = "📐",
                Children = { "level-info", "entities" }
            };
            nodes.Add(root);
            var levelInfo = new OutlinerNode
            {
                Id = "level-info",
                Label = $"Level '{level?.Name ?? "NewTerrain"}' - Entities: {level?.Entities.Count ?? 0} | Heightmap: {level?.Terrain?.HeightmapPath ?? "flat"}",
                Icon = "🌍",
                ParentId = "root"
            };
            nodes.Add(levelInfo);
            var entitiesParent = new OutlinerNode { Id = "entities", Label = "Entities", Icon = "🧱", ParentId = "root" };
            nodes.Add(entitiesParent);
            var entities = _editorScene.GetEntities();
            foreach (var entity in entities)
            {
                bool selected = _selectedEntityIds.Contains(entity.Id);
                var node = new OutlinerNode
                {
                    Id = $"entity-{entity.Id}",
                    Label = $"Entity {entity.Id} ({entity.Type ?? "Unknown"})",
                    Icon = selected ? "✅🧱" : "🧱",
                    ParentId = "entities"
                };
                nodes.Add(node);
                entitiesParent.Children.Add(node.Id);
            }
            Console.WriteLine($"[SceneEditorPanel.GetCurrentHierarchy] Returned {nodes.Count} nodes (root + {entities.Count} entities)");
            return nodes;
        }
        public object GetObjectForNode(string nodeId)
        {
            if (nodeId.StartsWith("entity-"))
            {
                if (int.TryParse(nodeId.Substring(7), out int id))
                {
                    var entities = _editorScene.GetEntities();
                    return entities.FirstOrDefault(e => e.Id == id);
                }
            }
            return null;
        }
        public void NotifyHierarchyChanged()
        {
            OutlinerCoordinator.Instance.NotifyHierarchyChanged();
        }
    }
}