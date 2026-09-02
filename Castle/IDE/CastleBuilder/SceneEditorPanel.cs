// Folder: CastleBuilder
// File: SceneEditorPanel.cs
using Keystone;
using MapRoom;
using ReadingChamber;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Core.GPU.Renderers;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.PlayerSystem;
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
        private bool _pendingSceneSelectorUpdate = false;
        private List<int> _selectedEntityIds = new List<int>();
        private bool _wasRightPressedLastFrame = false;
        private bool _isBoxSelecting = false;
        private Vector2 _boxStart = Vector2.Zero;
        private Vector2 _boxEnd = Vector2.Zero;
        private const float MinDragDistance = 5f;
        private TransformGizmoOverlay _transformGizmo;
        private PhysicsDebugOverlay _physicsDebug;
        private AcousticDebugOverlay _acousticDebug;
        private readonly List<IWorldOverlay> _worldOverlays = new List<IWorldOverlay>();
        private bool _fileSelectedSubscribed = false;
        private bool _genericSubscribed = false;
        private bool _entitySelectedSubscribed = false;
        private long _lastLightPlaceTick;
        public SceneEditorPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            DockingMode = DockingMode.IDE;
            BaseHeight = 720f;
            _editorScene = EditorScene.Current ?? new EditorScene(renderContext, controlContext, window, eventBus);
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
            _worldOverlays.Add(_transformGizmo);
            _physicsDebug = new PhysicsDebugOverlay(
                renderContext,
                () => GetLiveEntities(),
                () => _editorScene.GetContactManifolds(),
                () => _selectedEntityIds
            );
            CustomOverlays.Add(_physicsDebug);
            _worldOverlays.Add(_physicsDebug);
            _acousticDebug = new AcousticDebugOverlay(
                renderContext,
                () => GetLiveEntities(),
                () => GetAcousticListenerPosition(),
                () =>
                {
                    var list = new List<Vector3>();
                    var ents = GetLiveEntities();
                    if (ents == null) return list;
                    foreach (var e in ents)
                    {
                        var sc = e.GetComponent<SoundComponent>();
                        if (sc == null) continue;
                        var p = e.GetComponent<PhysicsComponent>();
                        if (p != null) list.Add(p.Position);
                    }
                    return list;
                },
                () => _editorScene.GetHeightProvider()
            );
            CustomOverlays.Add(_acousticDebug);
            _worldOverlays.Add(_acousticDebug);
        }
        /// <summary>
        /// Exact same sources AudioSystem and RuntimeGameplayScene already trust.
        /// 1. Live Player.Camera.Position (AudioSystem.Update path)
        /// 2. PreferredSpawnPointIds → entity PhysicsComponent.Position + 1.8 m eye offset
        /// Never uses fly camera or invented coordinates.
        /// </summary>
        private Vector3 GetAcousticListenerPosition()
        {
            var ents = GetLiveEntities();
            // 1. Identical to AudioSystem.Update – live Player + CameraController
            if (ents != null)
            {
                foreach (var e in ents)
                {
                    var player = e.GetComponent<Player>();
                    if (player?.Camera != null)
                        return player.Camera.Position;
                }
            }
            // 2. Identical to RuntimeGameplayScene preferred-spawn application + eye height
            SceneSettings settings = ResolveCurrentSceneSettings();
            if (settings?.PreferredSpawnPointIds != null && ents != null)
            {
                foreach (int id in settings.PreferredSpawnPointIds)
                {
                    var spawnEntity = ents.FirstOrDefault(e => e.Id == id);
                    if (spawnEntity == null) continue;
                    var spawnPhysics = spawnEntity.GetComponent<PhysicsComponent>();
                    if (spawnPhysics != null)
                        return spawnPhysics.Position + new Vector3(0f, 0f, 1.8f);
                }
            }
            return Vector3.Zero;
        }
        private SceneSettings ResolveCurrentSceneSettings()
        {
            try
            {
                var ps = ProjectSettings.Current;
                if (ps == null) return null;
                object sceneData = null;
                var type = ps.GetType();
                sceneData = type.GetProperty("CurrentSceneData")?.GetValue(ps)
                         ?? type.GetProperty("SceneData")?.GetValue(ps)
                         ?? type.GetField("CurrentSceneData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ps);
                if (sceneData != null)
                {
                    var settingsProp = sceneData.GetType().GetProperty("Settings")
                                    ?? sceneData.GetType().GetProperty("settings");
                    if (settingsProp?.GetValue(sceneData) is SceneSettings s)
                        return s;
                }
                var direct = type.GetProperty("PreferredSpawnPointIds")?.GetValue(ps) as List<int>;
                if (direct != null)
                    return new SceneSettings { PreferredSpawnPointIds = direct };
            }
            catch { }
            return null;
        }
        private IReadOnlyList<Entity> GetLiveEntities()
        {
            var ents = _editorScene.GetEntities();
            if (ents != null && ents.Count > 0) return ents;
            return Array.Empty<Entity>();
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
            if (!_entitySelectedSubscribed)
            {
                _eventBus.Subscribe<EntitySelectedEvent>(OnEntitySelected);
                _entitySelectedSubscribed = true;
            }
            if (!_genericSubscribed)
            {
                _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
                _genericSubscribed = true;
            }
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
            else if (e.Hook == "AddLight")
            {
                PlaceLightFromPanel(e);
            }
            else if (e.Hook == "PostProcessSet")
            {
                ApplyPostProcess(e);
            }
            else if (e.Hook == "EditorDeleteSelection")
            {
                DeleteSelectedEntities();
            }
            else if (e.Hook == "EditorDeleteScene")
            {
                MenuCommands.DeleteScene(_renderContext, _controlContext, _window, _eventBus);
            }
            else if (e.Hook == "SceneDeleted")
            {
                _pendingSceneSelectorUpdate = true;
                if (_selectedEntityIds.Count > 0)
                {
                    _selectedEntityIds.Clear();
                    _transformGizmo.ClearSelection();
                    NotifyHierarchyChanged();
                }
            }
            else if (e.Hook == "OutlinerSelectionChanged")
            {
                string nodeId = e.Data != null && e.Data.ContainsKey("nodeId") ? e.Data["nodeId"]?.ToString() ?? "" : "";
                if (TryParseEntityNode(nodeId, out int entityId, out _))
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
            else if (hook == "DeleteActiveScene")
            {
                MenuCommands.DeleteScene(_renderContext, _controlContext, _window, _eventBus);
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
            else if (hook == "ToggleAcousticDebug")
            {
                if (_acousticDebug != null)
                    _acousticDebug.Enabled = !_acousticDebug.Enabled;
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
                RecordPlacedEntity(entity, "Place sound source");
                NotifyHierarchyChanged();
            }
            else if (hook == "OpenAddLight")
            {
                AddLightPanel.Open(_renderContext, _controlContext, _window, _eventBus);
                Console.WriteLine("[SceneEditorPanel] Opened AddLightPanel");
            }
            else if (hook == "OpenPostProcess")
            {
                PostProcessPanel.Open(_renderContext, _controlContext, _window, _eventBus);
                Console.WriteLine("[SceneEditorPanel] Opened PostProcessPanel");
            }
        }

        private void PlaceLightFromPanel(GenericEvent evt)
        {
            long now = Environment.TickCount64;
            if (now - _lastLightPlaceTick < 250)
            {
                Console.WriteLine("[SceneEditorPanel.PlaceLight] Ignored duplicate place within 250ms");
                return;
            }
            _lastLightPlaceTick = now;
            if (!_editorScene.TryGetPlacementPosition(out var hitPoint))
            {
                Console.WriteLine("[SceneEditorPanel.PlaceLight] Raycast failed - no valid placement position");
                return;
            }
            Vector3 placePos = hitPoint + new Vector3(0f, 0f, 2f);
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null)
            {
                string sceneName = _editorScene.CurrentGameScene ?? "Main";
                level = new Level(_eventBus) { Name = sceneName };
                ProjectSettings.Current.SetCurrentLevel(level);
            }

            LightType type = LightType.Point;
            Vector3 color = Vector3.One;
            float intensity = 1f;
            Vector3 direction = Vector3.Normalize(new Vector3(0f, 0f, -1f));
            float range = 25f;
            bool castShadows = true;
            if (evt?.Data != null)
            {
                string Read(string key) => evt.Data.ContainsKey(key) ? evt.Data[key]?.ToString() : null;
                string typeRaw = Read("type");
                if (!string.IsNullOrWhiteSpace(typeRaw) && Enum.TryParse(typeRaw, true, out LightType parsedType))
                    type = parsedType;
                if (type == LightType.Directional)
                    type = LightType.Point;
                string colorRaw = Read("color");
                if (!string.IsNullOrWhiteSpace(colorRaw))
                {
                    var parts = colorRaw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3
                        && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float cr)
                        && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float cg)
                        && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float cb))
                        color = new Vector3(cr, cg, cb);
                }
                string intensityRaw = Read("intensity");
                if (float.TryParse(intensityRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedIntensity))
                    intensity = parsedIntensity;
                string dirRaw = Read("direction");
                if (!string.IsNullOrWhiteSpace(dirRaw))
                {
                    var parts = dirRaw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3
                        && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dx)
                        && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dy)
                        && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dz))
                    {
                        Vector3 d = new Vector3(dx, dy, dz);
                        if (d.LengthSquared() > 1e-8f)
                            direction = Vector3.Normalize(d);
                    }
                }
                string rangeRaw = Read("range");
                if (float.TryParse(rangeRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedRange))
                    range = parsedRange;
                string castRaw = Read("castShadows");
                if (!string.IsNullOrWhiteSpace(castRaw))
                    castShadows = castRaw != "0" && !string.Equals(castRaw, "false", StringComparison.OrdinalIgnoreCase);
            }

            // Build the entity fully BEFORE Level.AddEntity. EntityAddedEvent
            // subscribers snapshot the object; adding LightComponent afterwards
            // left the runtime server with a bare Physics entity, so placed
            // lights did nothing.
            var entity = new Entity { Id = 0, Type = "Light" };
            var physics = new PhysicsComponent
            {
                Position = placePos,
                Rotation = Quaternion.Identity,
                Scale = Vector3.One,
                BodyType = BodyType.Static,
                CollisionEnabled = false
            };
            entity.AddComponent(physics);
            entity.AddComponent(new LightComponent
            {
                Type = type,
                Color = color,
                Intensity = intensity,
                Direction = direction,
                Position = placePos,
                Range = range,
                Enabled = true,
                CastShadows = castShadows,
                ShadowMode = ShadowMode.Auto
            });
            level.AddEntity(entity);
            LightingFrame.Current = null;
            Console.WriteLine($"[SceneEditorPanel.PlaceLight] Placed {type} Light entity ID={entity.Id} at {placePos}");
            _editorScene.SyncCurrentLevelToRuntimeServer();
            RecordPlacedEntity(entity, "Place light");
            NotifyHierarchyChanged();
        }

        private void ApplyPostProcess(GenericEvent evt)
        {
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null)
            {
                string sceneNameFallback = _editorScene?.CurrentGameScene ?? "Main";
                level = new Level(_eventBus) { Name = sceneNameFallback };
                ProjectSettings.Current.SetCurrentLevel(level);
            }
            var env = level.Environment ?? new EnvironmentSettings();
            string Read(string key) => evt?.Data != null && evt.Data.ContainsKey(key) ? evt.Data[key]?.ToString() : null;
            string fogMode = Read("fogMode");
            if (!string.IsNullOrWhiteSpace(fogMode)) env.FogMode = fogMode.Trim();
            string fogQuality = Read("fogQuality");
            if (!string.IsNullOrWhiteSpace(fogQuality)) env.FogQuality = fogQuality.Trim();
            if (float.TryParse(Read("fogDensity"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float density))
                env.FogDensity = density;
            if (float.TryParse(Read("fogStart"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float start))
                env.FogStart = start;
            string shadowQuality = Read("shadowQuality");
            if (!string.IsNullOrWhiteSpace(shadowQuality)) env.ShadowQuality = shadowQuality.Trim();
            string sunEnabled = Read("sunEnabled");
            if (!string.IsNullOrWhiteSpace(sunEnabled))
                env.SunEnabled = sunEnabled != "0" && !string.Equals(sunEnabled, "false", StringComparison.OrdinalIgnoreCase);
            string dirRaw = Read("sunDirection");
            if (!string.IsNullOrWhiteSpace(dirRaw))
            {
                var parts = dirRaw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dx)
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dy)
                    && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dz))
                {
                    Vector3 d = new Vector3(dx, dy, dz);
                    if (d.LengthSquared() > 1e-8f)
                        env.SunDirection = Vector3.Normalize(d);
                }
            }
            if (float.TryParse(Read("sunIntensity"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sunIntensity))
                env.SunIntensity = sunIntensity;
            string sunCast = Read("sunCastShadows");
            if (!string.IsNullOrWhiteSpace(sunCast))
                env.SunCastShadows = sunCast != "0" && !string.Equals(sunCast, "false", StringComparison.OrdinalIgnoreCase);
            level.Environment = env;
            var project = _editorScene.GetProjectData();
            string sceneName = _editorScene.CurrentGameScene;
            if (project?.Scenes != null && !string.IsNullOrEmpty(sceneName) && project.Scenes.TryGetValue(sceneName, out var sd) && sd != null)
                sd.Environment = env;
            var currentScene = ProjectSettings.Current?.CurrentSceneData;
            if (currentScene != null)
                currentScene.Environment = env;
            LightingSettings.BindAuthored(env);
            LightingFrame.Current = null;
            Console.WriteLine($"[SceneEditorPanel] PostProcess sun={env.SunEnabled} dir={env.SunDirection} fog={env.FogMode}/{env.FogQuality} shadows={env.ShadowQuality}");
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
            RecordPlacedEntity(entity, "Place entity");
            NotifyHierarchyChanged();
        }
        public void HandleUIClick(HtmlElement elem)
        {
        }
        public override void ToggleCameraMode()
        {
            _cameraMode = !_cameraMode;
            EditorHistory.FlyCameraActive = _cameraMode;
            if (_cameraMode) PanelManager.Current.CapturePanel(this);
            else PanelManager.Current.ReleasePanelCapture();
            if (_cameraMode)
                _transformGizmo.ClearSelection();
        }
        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            EditorHistory.Current.BindInput(_controlContext, _window);
            EditorHistory.Current.Tick();
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
            var active = _editorScene.GetActiveGameScene();
            if (active != null)
                active.GetCameraViewProjection(out view, out projection);
            _transformGizmo.UpdateMatrices(view, projection);
            if (!_cameraMode && isTopmost)
            {
                float headerHeight = HasTitleBar ? HeaderHeight : 0f;
                Vector2 contentMouse = new Vector2(absMousePos.X - Position.X, absMousePos.Y - Position.Y - headerHeight);
                float contentW = Size.X;
                float contentH = Size.Y - headerHeight;
                _transformGizmo.HandleMouseInput(contentMouse, contentW, contentH, mouseDown, mousePressed, mouseReleased);
                if (_selectedEntityIds.Count == 1)
                {
                    var entity = _editorScene.GetEntityById(_selectedEntityIds[0]);
                    if (entity != null)
                    {
                        var physics = entity.GetComponent<PhysicsComponent>();
                        if (physics != null)
                        {
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
                            const float rotateSpeed = 90.0f * (MathF.PI / 180f);
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
            // Play Game renders with the scene server entity list and the
            // authored Post Process environment. The panel used to call
            // Render(null) and rely on a later fallback — bind the same
            // inputs Play uses so the editor view cannot pack a different
            // LightingFrame than the export.
            var env = ProjectSettings.Current?.CurrentLevel?.Environment;
            if (env != null)
                LightingSettings.BindAuthored(env);
            _editorScene.Render(_editorScene.GetEntities());
            Matrix4x4 view = Matrix4x4.Identity;
            Matrix4x4 projection = Matrix4x4.Identity;
            var active = _editorScene.GetActiveGameScene();
            if (active != null)
                active.GetCameraViewProjection(out view, out projection);
            for (int i = 0; i < _worldOverlays.Count; i++)
                _worldOverlays[i].RenderWorld(view, projection);
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
            if (_entitySelectedSubscribed)
            {
                _eventBus.Unsubscribe<EntitySelectedEvent>(OnEntitySelected);
                _entitySelectedSubscribed = false;
            }
            if (_genericSubscribed)
            {
                _eventBus.Unsubscribe<GenericEvent>(OnGenericEvent);
                _genericSubscribed = false;
            }
            PanelManager.Current.ReleasePanelCapture();
            _editorScene = null;
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
                Children = { "level-info", "entities" },
                IsExpanded = true
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
            var entitiesParent = new OutlinerNode { Id = "entities", Label = "Entities", Icon = "🧱", ParentId = "root", IsExpanded = true };
            nodes.Add(entitiesParent);
            var entities = GetLiveEntities();
            foreach (var entity in entities)
            {
                bool selected = _selectedEntityIds.Contains(entity.Id);
                var node = new OutlinerNode
                {
                    Id = $"entity-{entity.Id}",
                    Label = $"Entity {entity.Id} ({entity.Type ?? "Unknown"})",
                    Icon = selected ? "✅🧱" : "🧱",
                    ParentId = "entities",
                    AssociatedObject = entity,
                    IsExpanded = false
                };
                nodes.Add(node);
                entitiesParent.Children.Add(node.Id);

                var modelComp = entity.GetComponent<ModelComponent>();
                FBXModel model = modelComp?.Model;
                if (model == null && modelComp != null && !string.IsNullOrEmpty(modelComp.Key) && ModelManager.Instance != null)
                    ModelManager.Instance.TryGetModel(modelComp.Key, out model);
                int meshCount = model?.Meshes?.Count ?? 0;
                if (meshCount == 0 && modelComp != null && ModelManager.Instance != null && !string.IsNullOrEmpty(modelComp.Key)
                    && ModelManager.Instance.TryGetModelData(modelComp.Key, out var md) && md?.MeshRenders != null)
                    meshCount = md.MeshRenders.Count;
                for (int mi = 0; mi < meshCount; mi++)
                {
                    string meshId = $"entity-{entity.Id}-mesh-{mi}";
                    nodes.Add(new OutlinerNode
                    {
                        Id = meshId,
                        Label = $"Mesh {mi}",
                        Icon = "🧊",
                        ParentId = node.Id,
                        AssociatedObject = entity,
                        IsExpanded = false
                    });
                    node.Children.Add(meshId);
                }
            }
            Console.WriteLine($"[SceneEditorPanel.GetCurrentHierarchy] Returned {nodes.Count} nodes (root + {entities.Count} entities)");
            return nodes;
        }
        public object GetObjectForNode(string nodeId)
        {
            if (TryParseEntityNode(nodeId, out int id, out int meshIndex))
            {
                var entities = GetLiveEntities();
                var entity = entities.FirstOrDefault(e => e.Id == id);
                if (entity == null) return null;
                if (meshIndex >= 0)
                {
                    return new MeshLayerRef
                    {
                        EntityId = id,
                        MeshIndex = meshIndex,
                        Label = $"Mesh {meshIndex}",
                        Entity = entity
                    };
                }
                return entity;
            }
            if (nodeId == "level-info" || nodeId == "root")
            {
                return ProjectSettings.Current.CurrentLevel;
            }
            return null;
        }

        private static bool TryParseEntityNode(string nodeId, out int entityId, out int meshIndex)
        {
            entityId = -1;
            meshIndex = -1;
            if (string.IsNullOrEmpty(nodeId) || !nodeId.StartsWith("entity-")) return false;
            string rest = nodeId.Substring(7);
            int meshAt = rest.IndexOf("-mesh-", StringComparison.Ordinal);
            if (meshAt >= 0)
            {
                if (!int.TryParse(rest.Substring(0, meshAt), out entityId)) return false;
                int.TryParse(rest.Substring(meshAt + 6), out meshIndex);
                return true;
            }
            return int.TryParse(rest, out entityId);
        }
        public void NotifyHierarchyChanged()
        {
            OutlinerCoordinator.Instance.NotifyHierarchyChanged();
        }
        private void RecordPlacedEntity(Entity entity, string description)
        {
            if (entity == null || EditorHistory.Current.IsApplying) return;
            var snapshot = entity.ToData();
            int id = entity.Id;
            EditorHistory.Current.Record(new DelegateCommand(
                description ?? "Place entity",
                () => ApplyEntityRestore(new List<EntityData> { snapshot }),
                () => ApplyEntityDelete(new List<int> { id })));
        }
        private void DeleteSelectedEntities()
        {
            if (EditorHistory.AnyTextInputFocused()) return;
            if (_selectedEntityIds == null || _selectedEntityIds.Count == 0) return;
            var snapshots = new List<EntityData>();
            foreach (var id in _selectedEntityIds)
            {
                var entity = _editorScene != null ? _editorScene.GetEntityById(id) : null;
                if (entity == null)
                    entity = ProjectSettings.Current.CurrentLevel?.Entities.Find(e => e.Id == id);
                if (entity != null)
                    snapshots.Add(entity.ToData());
            }
            if (snapshots.Count == 0) return;
            var ids = new List<int>();
            foreach (var snap in snapshots)
                ids.Add(snap.Id);
            EditorHistory.Current.Execute(new DelegateCommand(
                "Delete entities",
                () => ApplyEntityDelete(ids),
                () => ApplyEntityRestore(snapshots)));
        }
        private void ApplyEntityDelete(List<int> ids)
        {
            if (ids == null || _editorScene == null) return;
            foreach (var id in ids)
                _editorScene.RemoveLiveEntity(id);
            _selectedEntityIds.RemoveAll(id => ids.Contains(id));
            _transformGizmo.ClearSelection();
            NotifyHierarchyChanged();
        }
        private void ApplyEntityRestore(List<EntityData> snapshots)
        {
            if (snapshots == null || _editorScene == null) return;
            foreach (var data in snapshots)
            {
                if (data == null) continue;
                var entity = Entity.FromData(data);
                _editorScene.AddLiveEntity(entity);
            }
            _editorScene.SyncCurrentLevelToRuntimeServer();
            NotifyHierarchyChanged();
        }
    }
}