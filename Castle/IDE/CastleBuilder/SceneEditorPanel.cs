// Folder: CastleBuilder
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
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering.Renderers;
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
        private PhysicsDebugOverlay _physicsDebug;
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
            _physicsDebug = new PhysicsDebugOverlay(
                renderContext,
                () =>
                {
                    var ents = _editorScene.GetEntities();
                    if (ents != null && ents.Count > 0) return ents;
                    var serverField = _editorScene.GetType().GetField("_server", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (serverField != null)
                    {
                        var server = serverField.GetValue(_editorScene) as IGameServer;
                        return server?.GetEntities() ?? (IReadOnlyList<Entity>)Array.Empty<Entity>();
                    }
                    return Array.Empty<Entity>();
                },
                () =>
                {
                    try
                    {
                        var serverField = _editorScene.GetType().GetField("_server", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (serverField == null) return Array.Empty<ContactManifold>();
                        var server = serverField.GetValue(_editorScene);
                        // Editor path – ClientGameServerProxy owns the live PhysicsWorld
                        if (server is ClientGameServerProxy proxy)
                            return proxy.CurrentManifolds;
                        // Play-mode / full GameServer path
                        if (server != null)
                        {
                            var physSysField = server.GetType().GetField("_physicsSystem", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (physSysField != null)
                            {
                                var physSys = physSysField.GetValue(server);
                                if (physSys != null)
                                {
                                    var worldProp = physSys.GetType().GetProperty("World");
                                    if (worldProp != null)
                                    {
                                        var world = worldProp.GetValue(physSys) as PhysicsWorld;
                                        return world?.CurrentManifolds ?? (IReadOnlyList<ContactManifold>)Array.Empty<ContactManifold>();
                                    }
                                }
                            }
                        }
                        return Array.Empty<ContactManifold>();
                    }
                    catch
                    {
                        return Array.Empty<ContactManifold>();
                    }
                },
                () => _selectedEntityIds
            );
            CustomOverlays.Add(_physicsDebug);
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
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
        }
        private void OnGenericEvent(GenericEvent e)
        {
            if (e.Hook == "SkyboxSet" && e.Data != null && e.Data.ContainsKey("skybox"))
            {
                try
                {
                    string json = e.Data["skybox"].ToString();
                    SkyboxData sky = JsonSerializer.Deserialize<SkyboxData>(json);
                    if (sky != null)
                    {
                        var active = _editorScene.GetActiveGameScene() as TerrainCreatorScene;
                        if (active != null)
                        {
                            active.SetSkybox(sky);
                            Console.WriteLine("[SceneEditorPanel] SkyboxSet payload applied to active TerrainCreatorScene");
                        }
                    }
                }
                catch { }
            }
            else if (e.Hook == "SkyboxSet")
            {
                var active = _editorScene.GetActiveGameScene() as TerrainCreatorScene;
                if (active != null)
                {
                    active.RefreshFromLiveState(ProjectSettings.Current.CurrentSceneData);
                    Console.WriteLine("[SceneEditorPanel] SkyboxSet event → forced RefreshFromLiveState on active TerrainCreatorScene");
                }
            }
            else if (e.Hook == "OutlinerSelectionChanged")
            {
                // Selection originated from Outliner / Properties.
                // Update local state + gizmo directly. Do NOT re-publish EntitySelectedEvent
                // (that would re-enter NotifySelectionChanged and cause infinite recursion).
                string nodeId = e.Data != null && e.Data.ContainsKey("nodeId") ? e.Data["nodeId"]?.ToString() ?? "" : "";
                if (nodeId.StartsWith("entity-") && int.TryParse(nodeId.Substring(7), out int entityId))
                {
                    _selectedEntityIds = new List<int> { entityId };
                    var entity = _editorScene.GetEntityById(entityId);
                    if (entity != null)
                    {
                        var physics = entity.GetComponent<PhysicsComponent>();
                        if (physics != null)
                        {
                            _transformGizmo.OnEntitySelected(entityId, physics.Position, physics.Rotation);
                        }
                        else
                        {
                            _transformGizmo.ClearSelection();
                        }
                    }
                    else
                    {
                        _transformGizmo.ClearSelection();
                    }
                }
                else
                {
                    _selectedEntityIds.Clear();
                    _transformGizmo.ClearSelection();
                }
                NotifyHierarchyChanged();
            }
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
            else if (hook == "TogglePhysicsDebug")
            {
                if (_physicsDebug != null)
                    _physicsDebug.Enabled = !_physicsDebug.Enabled;
            }
            else if (hook == "PlaceSoundSource")
            {
                if (!_editorScene.TryGetPlacementPosition(out var hitPoint))
                {
                    Console.WriteLine("[SceneEditorPanel.PlaceSoundSource] Raycast failed - no valid placement position");
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
                var entity = level.PlaceEntity(placePos, "SoundSource");
                entity.Type = "SoundSource";
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                {
                    physics.BodyType = BodyType.Static;
                    physics.CollisionEnabled = false;
                }
                entity.AddComponent(new SoundComponent
                {
                    AudioClip = "",
                    Type = "SoundSource",
                    IsSensitive = false,
                    Loop = false,
                    Volume = 1f
                });
                Console.WriteLine($"[SceneEditorPanel.PlaceSoundSource] Placed SoundSource entity ID={entity.Id} at {placePos}");
                _editorScene.SyncCurrentLevelToRuntimeServer();
                NotifyHierarchyChanged();
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
                    physics.RebuildShape(modelComp.Model);
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
            if (!_cameraMode && isTopmost)
            {
                float headerHeight = HasTitleBar ? HeaderHeight : 0f;
                Vector2 contentMouse = new Vector2(absMousePos.X - Position.X, absMousePos.Y - Position.Y - headerHeight);
                float contentW = Size.X;
                float contentH = Size.Y - headerHeight;
                _transformGizmo.HandleMouseInput(contentMouse, contentW, contentH, mouseDown, mousePressed, mouseReleased);
                // Camera-relative keyboard: arrows = translation, WASD = rotation
                if (_selectedEntityIds.Count == 1)
                {
                    var entity = _editorScene.GetEntityById(_selectedEntityIds[0]);
                    if (entity != null)
                    {
                        var physics = entity.GetComponent<PhysicsComponent>();
                        if (physics != null)
                        {
                            // Extract camera-relative axes from the view matrix already computed above
                            Vector3 camForward = new Vector3(-view.M13, -view.M23, -view.M33);
                            Vector3 camRight = new Vector3(view.M11, view.M21, view.M31);
                            Vector3 camUp = new Vector3(view.M12, view.M22, view.M32);
                            camForward.Z = 0f;
                            camRight.Z = 0f;
                            float lenF = camForward.Length();
                            float lenR = camRight.Length();
                            if (lenF > 1e-5f) camForward /= lenF;
                            if (lenR > 1e-5f) camRight /= lenR;
                            const float translateSpeed = 5.0f;
                            const float rotateSpeed = 90.0f * (MathF.PI / 180f); // rad/s
                            // Arrows → camera-relative translation
                            Vector3 delta = Vector3.Zero;
                            if (_controlContext.GetKey(_window, Key.Up) == InputAction.Press) delta += camForward;
                            if (_controlContext.GetKey(_window, Key.Down) == InputAction.Press) delta -= camForward;
                            if (_controlContext.GetKey(_window, Key.Left) == InputAction.Press) delta -= camRight;
                            if (_controlContext.GetKey(_window, Key.Right) == InputAction.Press) delta += camRight;
                            bool moved = false;
                            if (delta.LengthSquared() > 1e-8f)
                            {
                                delta = Vector3.Normalize(delta) * translateSpeed * deltaTime;
                                physics.Position += delta;
                                moved = true;
                            }
                            // WASD → camera-relative rotation
                            float yawDelta = 0f;
                            float pitchDelta = 0f;
                            if (_controlContext.GetKey(_window, Key.A) == InputAction.Press) yawDelta += rotateSpeed * deltaTime;
                            if (_controlContext.GetKey(_window, Key.D) == InputAction.Press) yawDelta -= rotateSpeed * deltaTime;
                            if (_controlContext.GetKey(_window, Key.W) == InputAction.Press) pitchDelta += rotateSpeed * deltaTime;
                            if (_controlContext.GetKey(_window, Key.S) == InputAction.Press) pitchDelta -= rotateSpeed * deltaTime;
                            if (MathF.Abs(yawDelta) > 1e-6f || MathF.Abs(pitchDelta) > 1e-6f)
                            {
                                Quaternion q = physics.Rotation;
                                if (MathF.Abs(yawDelta) > 1e-6f)
                                {
                                    Quaternion yawQ = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yawDelta);
                                    q = Quaternion.Normalize(yawQ * q);
                                }
                                if (MathF.Abs(pitchDelta) > 1e-6f)
                                {
                                    Vector3 rightAxis = Vector3.Transform(Vector3.UnitX, q);
                                    Quaternion pitchQ = Quaternion.CreateFromAxisAngle(rightAxis, pitchDelta);
                                    q = Quaternion.Normalize(pitchQ * q);
                                }
                                physics.Rotation = q;
                                moved = true;
                            }
                            if (moved)
                            {
                                if (physics.BodyType == BodyType.Dynamic)
                                {
                                    physics.IsSleeping = false;
                                    physics.SleepTimer = 0f;
                                }
                                var level = ProjectSettings.Current.CurrentLevel;
                                if (level != null)
                                {
                                    var blueprintEntity = level.Entities.Find(e => e.Id == _selectedEntityIds[0]);
                                    if (blueprintEntity != null)
                                    {
                                        var bpPhysics = blueprintEntity.GetComponent<PhysicsComponent>();
                                        if (bpPhysics != null)
                                        {
                                            bpPhysics.Position = physics.Position;
                                            bpPhysics.Rotation = physics.Rotation;
                                            if (bpPhysics.BodyType == BodyType.Dynamic)
                                            {
                                                bpPhysics.IsSleeping = false;
                                                bpPhysics.SleepTimer = 0f;
                                            }
                                        }
                                    }
                                }
                                _eventBus.Publish(new EntityMovedEvent(_selectedEntityIds[0], new Vector2(physics.Position.X, physics.Position.Y), physics.Rotation));
                            }
                        }
                    }
                }
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
                                physics.RebuildShape(modelComp.Model);
                            }
                        }
                    }
                    if (fbxModel != null && _modelManager.TryGetModelData(modelComp.Key, out var modelData))
                    {
                        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(physics.Rotation);
                        Matrix4x4 translation = Matrix4x4.CreateTranslation(physics.Position);
                        // Per-model unit conversion: Blender (metres) uses 1.0, Unity/Unreal (cm) uses 0.01.
                        float unitScale = fbxModel.UnitToMeters;
                        Matrix4x4 scaleMat = Matrix4x4.CreateScale(unitScale * physics.Scale);
                        Matrix4x4 modelMatrix = scaleMat * rotation * translation;
                        _modelRenderer.RenderModel(fbxModel, modelData, view, projection, viewPos, modelMatrix);
                    }
                }
            }
            if (_transformGizmo != null)
            {
                _transformGizmo.RenderWorld(view, projection);
            }
            if (_physicsDebug != null)
            {
                _physicsDebug.RenderWorld(view, projection);
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
            if (nodeId == "level-info" || nodeId == "root")
            {
                return ProjectSettings.Current.CurrentLevel;
            }
            return null;
        }
        public void NotifyHierarchyChanged()
        {
            OutlinerCoordinator.Instance.NotifyHierarchyChanged();
        }
    }
}