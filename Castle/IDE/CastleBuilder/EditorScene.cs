// Folder: CastleBuilder
// File: EditorScene.cs
using Keystone;
using MapRoom;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;
using System.Numerics;
using System.Text.Json;
namespace CastleBuilder
{
    public class EditorScene : Scene
    {
        private ProjectData _projectData;
        private string _currentGameSceneName = string.Empty;
        private GameScene _activeGameScene;
        private Scene _hostedCustomScene;
        private readonly ProjectSceneCache _sceneCache = new ProjectSceneCache();
        private GameScene _pendingDisposeScene;
        private Scene _pendingDisposeHosted;
        private bool _scriptsActivatedForProject;
        private bool _coreSystemsRegistered;
        public static EditorScene Current { get; private set; }

        // Editor viewport has no implicit sun. Place a Light entity.
        // Play Game still injects LightingFrame.DefaultSunDirection.
        protected override bool AllowRuntimeDefaultSun => false;

        protected override List<ShadowCaster> CollectShadowCasters(IReadOnlyList<Entity> entities)
        {
            var list = entities ?? GetEntities();
            if (_hostedCustomScene != null)
                return _hostedCustomScene.GatherShadowCasters(list);
            if (_activeGameScene != null)
                return _activeGameScene.GatherShadowCasters(list);
            return ShadowMapRenderer.CollectCasters(list);
        }

        public EditorScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus) { }
        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            Current = this;
            RegisterCoreSystems();
            LoadProjectData();
        }
        /// <summary>
        /// Same core systems SceneManager registers for Play.
        /// AudioSystem is optional at runtime (EnableFreeSurfaceAudio / LOS / none).
        /// </summary>
        private void RegisterCoreSystems()
        {
            if (_coreSystemsRegistered || _server == null) return;
            _coreSystemsRegistered = true;
            var prediction = new ClientPredictionSystem(_server, _eventBus);
            _server.AddSystem(prediction);
            _server.AddSystem(new AnimationSystem(_server));
            // Free-surface is fully optional. Games with no 3D world space simply leave
            // EnableFreeSurfaceAudio = false (or never call Kick). LOS / none still work.
            _server.AddSystem(new AudioSystem(_server, _eventBus, isServer: false, validationSystem: null, renderContext: _renderContext));
            Console.WriteLine("[EditorScene] Core systems registered (Prediction, Animation, Audio).");
        }
        public ProjectData GetProjectData() => _projectData;
        public IReadOnlyList<Entity> GetEntities() => _server.GetEntities();
        public Entity GetEntityById(int id)
        {
            return _server.GetEntities().FirstOrDefault(e => e.Id == id);
        }
        public GameScene GetActiveGameScene() => _activeGameScene;
        public IReadOnlyList<ContactManifold> GetContactManifolds()
        {
            if (_server is ClientGameServerProxy proxy)
                return proxy.CurrentManifolds ?? (IReadOnlyList<ContactManifold>)Array.Empty<ContactManifold>();
            return Array.Empty<ContactManifold>();
        }
        public IHeightProvider GetHeightProvider()
        {
            if (_server is ClientGameServerProxy proxy)
                return proxy.PhysicsWorld?.HeightProvider;
            return null;
        }
        public bool TryGetPlacementPosition(out Vector3 position)
        {
            position = Vector3.Zero;
            if (_activeGameScene is TerrainCreatorScene tcs)
            {
                return tcs.TryPerformPlacementRaycast(out position);
            }
            return false;
        }
        public bool TryPerformEntitySelectionRaycast(Vector2 normalizedMouse, float contentW, float contentH, out int entityId, out Vector3 hitPoint, bool cycle = false)
        {
            entityId = -1;
            hitPoint = Vector3.Zero;
            if (!(_activeGameScene is TerrainCreatorScene tcs)) return false;
            if (!tcs.GetMouseRay(normalizedMouse, contentW, contentH, out Vector3 rayOrigin, out Vector3 rayDir))
                return false;
            var hits = new List<(int id, float dist, Vector3 point)>();
            foreach (var e in GetEntities())
            {
                var physics = e.GetComponent<PhysicsComponent>();
                if (physics == null) continue;
                if (physics.RayIntersects(rayOrigin, rayDir, out float dist, out Vector3 p))
                {
                    hits.Add((e.Id, dist, p));
                }
            }
            hits.Sort((a, b) => a.dist.CompareTo(b.dist));
            if (hits.Count == 0) return false;
            if (cycle && hits.Count > 1)
            {
                int nextIndex = 1 % hits.Count;
                var nextHit = hits[nextIndex];
                entityId = nextHit.id;
                hitPoint = nextHit.point;
                return true;
            }
            var best = hits[0];
            entityId = best.id;
            hitPoint = best.point;
            return true;
        }
        public List<int> PerformBoxSelection(Vector2 ndcStart, Vector2 ndcEnd, float contentW, float contentH)
        {
            var selected = new List<int>();
            if (!(_activeGameScene is TerrainCreatorScene tcs)) return selected;
            float minX = Math.Min(ndcStart.X, ndcEnd.X) * contentW;
            float maxX = Math.Max(ndcStart.X, ndcEnd.X) * contentW;
            float minY = Math.Min(ndcStart.Y, ndcEnd.Y) * contentH;
            float maxY = Math.Max(ndcStart.Y, ndcEnd.Y) * contentH;
            foreach (var e in GetEntities())
            {
                var physics = e.GetComponent<PhysicsComponent>();
                if (physics == null) continue;
                if (IsBoxInFrustum(tcs, minX, maxX, minY, maxY, contentW, contentH, physics))
                {
                    selected.Add(e.Id);
                }
            }
            return selected;
        }
        private bool IsBoxInFrustum(TerrainCreatorScene tcs, float minX, float maxX, float minY, float maxY, float contentW, float contentH, PhysicsComponent physics)
        {
            Vector3 center = physics.Position;
            if (!ProjectWorldToScreen(center, contentW, contentH, out Vector2 screenCenter))
                return false;
            if (screenCenter.X >= minX && screenCenter.X <= maxX && screenCenter.Y >= minY && screenCenter.Y <= maxY)
                return true;
            Vector3[] localCorners =
            {
                physics.LocalBoundsMinCm * 0.01f,
                new Vector3(physics.LocalBoundsMinCm.X, physics.LocalBoundsMinCm.Y, physics.LocalBoundsMaxCm.Z) * 0.01f,
                new Vector3(physics.LocalBoundsMinCm.X, physics.LocalBoundsMaxCm.Y, physics.LocalBoundsMinCm.Z) * 0.01f,
                new Vector3(physics.LocalBoundsMaxCm.X, physics.LocalBoundsMinCm.Y, physics.LocalBoundsMinCm.Z) * 0.01f,
                new Vector3(physics.LocalBoundsMaxCm.X, physics.LocalBoundsMaxCm.Y, physics.LocalBoundsMinCm.Z) * 0.01f,
                new Vector3(physics.LocalBoundsMinCm.X, physics.LocalBoundsMaxCm.Y, physics.LocalBoundsMaxCm.Z) * 0.01f,
                new Vector3(physics.LocalBoundsMaxCm.X, physics.LocalBoundsMinCm.Y, physics.LocalBoundsMaxCm.Z) * 0.01f,
                physics.LocalBoundsMaxCm * 0.01f
            };
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(physics.Rotation);
            Matrix4x4 trans = Matrix4x4.CreateTranslation(physics.Position);
            foreach (var local in localCorners)
            {
                Vector3 world = Vector3.Transform(local, rot * trans);
                if (ProjectWorldToScreen(world, contentW, contentH, out Vector2 p))
                {
                    if (p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY)
                        return true;
                }
            }
            return false;
        }
        private bool ProjectWorldToScreen(Vector3 worldPos, float contentW, float contentH, out Vector2 screenPos)
        {
            screenPos = Vector2.Zero;
            if (!(_activeGameScene is TerrainCreatorScene tcs)) return false;
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 180f * 65f, contentW / contentH, 0.1f, 50000f);
            Matrix4x4 view = tcs.GetViewMatrix();
            Vector4 clip = Vector4.Transform(new Vector4(worldPos, 1f), view * proj);
            if (Math.Abs(clip.W) < 1e-6f) return false;
            clip /= clip.W;
            screenPos = new Vector2(
                (clip.X * 0.5f + 0.5f) * contentW,
                (1f - (clip.Y * 0.5f + 0.5f)) * contentH
            );
            return true;
        }
        public void SyncCurrentLevelToRuntimeServer()
        {
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null || _server == null) return;
            var clientProxy = _server as ClientGameServerProxy;
            if (clientProxy != null)
            {
                foreach (var entity in level.Entities)
                {
                    clientProxy.AddEntity(entity);
                }
                Console.WriteLine($"[EditorScene.SyncCurrentLevelToRuntimeServer] Idempotent sync: {level.Entities.Count} entities (loaded + new placements preserved)");
            }
        }
        public void LoadProjectData()
        {
            _scriptsActivatedForProject = false;
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
            {
                string jsonPath = Path.Combine(projectPath, "project.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    _projectData = JsonSerializer.Deserialize<ProjectData>(json, EntityData.SerializerOptions) ?? new ProjectData();
                }
            }
            if (_projectData == null) _projectData = new ProjectData();
            if (_projectData.Scenes == null) _projectData.Scenes = new Dictionary<string, SceneData>();
            EnsureProjectScriptsActivated(projectPath);
            string levelName = ProjectSettings.Current.CurrentLevel?.Name;
            if (!string.IsNullOrEmpty(levelName) && levelName != "Main")
            {
                _currentGameSceneName = levelName;
                if (!_projectData.Scenes.ContainsKey(_currentGameSceneName))
                {
                    var sd = new SceneData { Name = _currentGameSceneName, SceneType = "TerrainTest" };
                    sd.Terrain = new TerrainData();
                    _projectData.Scenes[_currentGameSceneName] = sd;
                }
            }
            else
            {
                _currentGameSceneName = _projectData.LastOpenedScene ?? (_projectData.Scenes.Keys.FirstOrDefault() ?? "Main");
            }
            _sceneCache.Clear();
            _pendingDisposeScene?.Dispose();
            _pendingDisposeScene = null;
            _pendingDisposeHosted?.Dispose();
            _pendingDisposeHosted = null;
            _hostedCustomScene = null;
            ActivateScene(_currentGameSceneName);
        }
        void EnsureProjectScriptsActivated(string projectPath)
        {
            if (_scriptsActivatedForProject || string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                return;
            try
            {
                ScriptLoader.ScanProjectScripts(projectPath);
                var regCtx = new SceneContext
                {
                    RenderContext = _renderContext,
                    ControlContext = _controlContext,
                    Window = _window,
                    Server = _server,
                    EventBus = _eventBus,
                    IsHostedPreview = true
                };
                ScriptLoader.ActivateProjectScripts(regCtx);
                _scriptsActivatedForProject = true;
                Console.WriteLine("[EditorScene] Project scripts scanned and activated for registry (CustomSceneEntry / RegisterGameSystem)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EditorScene] Script activation warning: {ex.Message}");
            }
        }
        private void HostChild(Scene child)
        {
            if (child == null) return;
            child.SetOwnsFramebuffer(false);
        }
        private void ActivateScene(string sceneName)
        {
            Level level = ProjectSettings.Current.CurrentLevel;
            if (level == null || level.Name != sceneName)
            {
                level = CreateOrLoadLevel(sceneName);
                ProjectSettings.Current.SetCurrentLevel(level);
            }
            RegisterAllAssetPacks(level);
            SceneData sd = null;
            _projectData?.Scenes?.TryGetValue(sceneName, out sd);
            if (_sceneCache.TryGet(sceneName, out var cachedScene, out var cachedLevel))
            {
                if (_activeGameScene != null && _activeGameScene != cachedScene)
                    _pendingDisposeScene = _activeGameScene;
                if (_hostedCustomScene != null)
                {
                    _pendingDisposeHosted = _hostedCustomScene;
                    _hostedCustomScene = null;
                }
                _activeGameScene = cachedScene;
                HostChild(_activeGameScene);
                _currentGameSceneName = sceneName;
                if (_projectData != null) _projectData.LastOpenedScene = sceneName;
                if (_activeGameScene is TerrainCreatorScene cachedTcs)
                {
                    cachedTcs.LoadSceneData(sd);
                    ProjectStateManager.Current.BindSceneToLiveState(sceneName, cachedTcs);
                    ProjectSettings.Current.SetCurrentTerrain(sd, cachedTcs.GetHeightmap(), sceneName);
                    if (sd?.Terrain?.ColorTexturePath != null)
                    {
                        cachedTcs.SetColorTexture(sd.Terrain.ColorTexturePath);
                    }
                    else
                    {
                        cachedTcs.SetColorTexture(null);
                    }
                    if (sd?.Skybox != null)
                    {
                        cachedTcs.SetSkybox(sd.Skybox);
                    }
                }
                SyncCurrentLevelToRuntimeServer();
                return;
            }
            _currentGameSceneName = sceneName;
            if (_projectData != null) _projectData.LastOpenedScene = sceneName;
            if (_activeGameScene != null)
                _pendingDisposeScene = _activeGameScene;
            if (_hostedCustomScene != null)
            {
                _pendingDisposeHosted = _hostedCustomScene;
                _hostedCustomScene = null;
            }
            bool isTerrainScene = _projectData.Scenes.TryGetValue(sceneName, out var sceneData) &&
                                  (sceneData.SceneType == "TerrainTest" || !string.IsNullOrEmpty(sceneData.Terrain?.HeightmapPath) || sceneName.Contains("Terrain", StringComparison.OrdinalIgnoreCase));
            if (!isTerrainScene)
            {
                EnsureProjectScriptsActivated(ProjectSettings.Current.ActiveProject);
                string hostedName = SceneRegistry.ResolvePreferredSceneName(sceneName, sd ?? sceneData);
                if (!string.IsNullOrEmpty(hostedName) &&
                    hostedName != "RuntimeGameplay" &&
                    SceneRegistry.IsRegistered(hostedName))
                {
                    var hostedCtx = new SceneContext
                    {
                        RenderContext = _renderContext,
                        ControlContext = _controlContext,
                        Window = _window,
                        Server = _server,
                        EventBus = _eventBus,
                        IsHostedPreview = true,
                        SceneData = sd ?? sceneData,
                        CurrentLevel = level,
                        LoadLevelName = sceneName,
                        PlayProjectPath = ProjectSettings.Current.ActiveProject
                    };
                    try
                    {
                        _hostedCustomScene = (Scene)SceneRegistry.Create(hostedName, hostedCtx);
                        HostChild(_hostedCustomScene);
                        _hostedCustomScene.Initialize(_width, _height);
                        _activeGameScene = new BasicGameScene(_renderContext, _controlContext, _window, _server, _eventBus, sd ?? sceneData);
                        HostChild(_activeGameScene);
                        _activeGameScene.Initialize(_width, _height);
                        Console.WriteLine($"[EditorScene] Hosted pure-client scene '{hostedName}' as view-only preview");
                        SyncCurrentLevelToRuntimeServer();
                        _sceneCache.Store(sceneName, _activeGameScene, level);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EditorScene] Failed to host custom scene '{hostedName}': {ex.Message}");
                        _hostedCustomScene?.Dispose();
                        _hostedCustomScene = null;
                    }
                }
            }
            _activeGameScene = isTerrainScene
                ? new TerrainCreatorScene(_renderContext, _controlContext, _window, _server, _eventBus, sd, enableBrush: false)
                : new BasicGameScene(_renderContext, _controlContext, _window, _server, _eventBus, sd);
            HostChild(_activeGameScene);
            _activeGameScene.Initialize(_width, _height);
            _activeGameScene.LoadSceneData(sd);
            if (_activeGameScene is TerrainCreatorScene tcs)
            {
                ProjectStateManager.Current.BindSceneToLiveState(sceneName, tcs);
                float[,] cached = ProjectSettings.Current.GetUnsavedHeightmap(sceneName);
                float[,] heightmapToUse = cached ?? ProjectSettings.Current.CurrentHeightmap ?? tcs.GetHeightmap();
                ProjectSettings.Current.SetCurrentTerrain(sd, heightmapToUse, sceneName, sd?.Terrain?.HeightmapPath);
                if (!string.IsNullOrEmpty(sd?.Terrain?.HeightmapPath))
                    tcs.LoadTerrain(sd.Terrain.HeightmapPath);
                else if (heightmapToUse != null)
                {
                    tcs.LoadSceneData(new SceneData { Name = sceneName, Terrain = new TerrainData() });
                }
                if (!string.IsNullOrEmpty(sd?.Terrain?.ColorTexturePath))
                {
                    tcs.SetColorTexture(sd.Terrain.ColorTexturePath);
                    Console.WriteLine($"[EditorScene] Synced color texture '{sd.Terrain.ColorTexturePath}' to TerrainCreatorScene for scene '{sceneName}'");
                }
                else
                {
                    tcs.SetColorTexture(null);
                }
                if (sd?.Skybox != null)
                {
                    tcs.SetSkybox(sd.Skybox);
                }
            }
            SyncCurrentLevelToRuntimeServer();
            _sceneCache.Store(sceneName, _activeGameScene, level);
        }
        private void RegisterAllAssetPacks(Level level)
        {
            if (level == null || ModelManager.Instance == null) return;
            var loadedPacks = new HashSet<string>();
            foreach (var entity in level.Entities)
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                if (modelComp != null && !string.IsNullOrEmpty(modelComp.Key) && loadedPacks.Add(modelComp.Key))
                {
                    string projectPath = ProjectSettings.Current.ActiveProject;
                    string packJsonPath = Path.Combine(projectPath, "Assets", modelComp.Key, "assetpack.json");
                    if (File.Exists(packJsonPath))
                    {
                        ModelManager.Instance.LoadAnimationPack(packJsonPath);
                        if (ModelManager.Instance.TryGetModel(modelComp.Key, out var fbxModel))
                        {
                            modelComp.Model = fbxModel;
                            var physics = entity.GetComponent<PhysicsComponent>();
                            if (physics != null && modelComp.Model != null)
                            {
                                physics.Size = modelComp.Model.GetBoundingSize();
                                physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                                physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
                                physics.RebuildShape(modelComp.Model);
                            }
                        }
                    }
                    else
                    {
                        ModelManager.Instance.MaterializeAssetPack(modelComp.Key, Path.Combine(projectPath, "Assets"));
                        ModelManager.Instance.LoadAnimationPack(packJsonPath);
                        if (ModelManager.Instance.TryGetModel(modelComp.Key, out var fbxModel))
                        {
                            modelComp.Model = fbxModel;
                            var physics = entity.GetComponent<PhysicsComponent>();
                            if (physics != null && modelComp.Model != null)
                            {
                                physics.Size = modelComp.Model.GetBoundingSize();
                                physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                                physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
                                physics.RebuildShape(modelComp.Model);
                            }
                        }
                    }
                }
            }
        }
        private Level CreateOrLoadLevel(string sceneName)
        {
            if (_projectData.Scenes.TryGetValue(sceneName, out var sceneData))
            {
                var level = new Level(_eventBus) { Name = sceneName };
                if (sceneData.Entities != null)
                {
                    foreach (var ed in sceneData.Entities)
                    {
                        var entity = Entity.FromData(ed);
                        level.AddEntity(entity);
                    }
                }
                level.Terrain = sceneData.Terrain ?? new TerrainData();
                level.Environment = sceneData.Environment ?? new EnvironmentSettings();
                level.Skybox = sceneData.Skybox;
                if (sceneData.CustomData != null)
                {
                    foreach (var kv in sceneData.CustomData)
                        level.CustomData[kv.Key] = kv.Value;
                }
                return level;
            }
            return new Level(_eventBus) { Name = sceneName };
        }
        public void FlushActiveSceneData()
        {
            if (_sceneCache.TryGet(_currentGameSceneName, out var cachedScene, out var cachedLevel) &&
                cachedScene is TerrainCreatorScene tcs)
            {
                string terrainName = _currentGameSceneName ?? "UntitledTerrain";
                tcs.SaveTerrain(terrainName);
                if (cachedLevel != null)
                {
                    if (cachedLevel.Terrain == null) cachedLevel.Terrain = new TerrainData();
                    cachedLevel.Terrain.HeightmapPath = tcs.GetHeightmap() != null
                        ? $"Assets/Terrain/{terrainName}.tif"
                        : "";
                    cachedLevel.Terrain.ColorTexturePath = tcs.GetColorTexturePath();
                }
                if (_projectData?.Scenes != null && _projectData.Scenes.TryGetValue(_currentGameSceneName, out var sceneData))
                {
                    if (sceneData.Terrain == null) sceneData.Terrain = new TerrainData();
                    sceneData.Terrain.HeightmapPath = cachedLevel?.Terrain?.HeightmapPath ?? "";
                    sceneData.Terrain.ColorTexturePath = cachedLevel?.Terrain?.ColorTexturePath ?? "";
                    ProjectSettings.Current.SetCurrentTerrain(sceneData, tcs.GetHeightmap(), _currentGameSceneName, sceneData.Terrain.HeightmapPath);
                }
            }
            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null && _projectData?.Scenes != null && _projectData.Scenes.TryGetValue(_currentGameSceneName, out var sd))
            {
                sd.Entities = level.Entities.ConvertAll(e => e.ToData());
                if (level.Environment != null)
                    sd.Environment = level.Environment;
                if (level.Skybox != null)
                    sd.Skybox = level.Skybox;
            }
            RegisterAllAssetPacks(level);
        }
        public void SwitchGameScene(string sceneName)
        {
            ActivateScene(sceneName);
        }
        public override void Update(float deltaTime)
        {
            if (_pendingDisposeScene != null)
            {
                _pendingDisposeScene.Dispose();
                _pendingDisposeScene = null;
            }
            if (_pendingDisposeHosted != null)
            {
                _pendingDisposeHosted.Dispose();
                _pendingDisposeHosted = null;
            }
            _activeGameScene?.Update(deltaTime);
            _hostedCustomScene?.Update(deltaTime);
        }
        public void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, bool cameraMode = true)
        {
            if (_activeGameScene is TerrainCreatorScene terrainScene)
                terrainScene.Update(deltaTime, relMousePos, mouseDown, mousePressed, mouseReleased, cameraMode);
            else if (_activeGameScene != null)
                _activeGameScene.Update(deltaTime);
            _hostedCustomScene?.Update(deltaTime);
        }
        // Inherit Scene.FrameClearColor so a fogged horizon is fog color, not a gray wall.

        protected override EnvironmentSettings GetEnvironmentSettings()
        {
            // Post Process Apply writes CurrentLevel.Environment. Prefer that
            // so SunEnabled / intensity take effect the same frame.
            var levelEnv = ProjectSettings.Current?.CurrentLevel?.Environment;
            if (levelEnv != null)
                return levelEnv;
            if (_projectData?.Scenes != null &&
                _projectData.Scenes.TryGetValue(_currentGameSceneName, out SceneData sd) &&
                sd?.Environment != null)
                return sd.Environment;
            return null;
        }

        protected override void GetViewProjection(out Matrix4x4 view, out Matrix4x4 projection)
        {
            if (_hostedCustomScene != null)
            {
                _hostedCustomScene.GetCameraViewProjection(out view, out projection);
                return;
            }
            if (_activeGameScene != null)
            {
                _activeGameScene.GetCameraViewProjection(out view, out projection);
                return;
            }
            base.GetViewProjection(out view, out projection);
        }

        public override void Render(IReadOnlyList<Entity> entities)
        {
            var list = entities ?? GetEntities();
            EnvironmentSettings env = GetEnvironmentSettings();
            if (_activeGameScene != null)
                _activeGameScene.BindAuthoredEnvironment(env);

            // Play Game calls Scene.Render on the gameplay scene. That packs
            // LightingFrame with the gameplay AllowRuntimeDefaultSun / env
            // and draws skybox + terrain + models. The editor wrapper used
            // to pack a different frame and then call RenderWorldOnly, which
            // skipped the gameplay prepare. Run the same method Play uses.
            LightingFrame.Current = null;
            if (_hostedCustomScene != null)
                _hostedCustomScene.Render(list);
            else if (_activeGameScene != null)
                _activeGameScene.Render(list);
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            var list = entities ?? GetEntities();
            if (_hostedCustomScene != null)
            {
                _hostedCustomScene.RenderWorldOnly(list, view, projection);
                return;
            }
            _activeGameScene?.RenderWorldOnly(list, view, projection);
        }

        protected override void RenderOverlay(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            var list = entities ?? GetEntities();
            if (_hostedCustomScene != null)
            {
                _hostedCustomScene.RenderOverlaysOnly(list, view, projection);
                return;
            }
            _activeGameScene?.RenderOverlaysOnly(list, view, projection);
        }

        public override void Resize(int width, int height)
        {
            base.Resize(width, height);
            _activeGameScene?.Resize(width, height);
            _hostedCustomScene?.Resize(width, height);
        }
        public List<string> GetAvailableScenes()
        {
            var keys = _projectData?.Scenes?.Keys.ToList() ?? new List<string>();
            var scenes = new HashSet<string>(keys);
            foreach (var key in ProjectSettings.Current.GetUnsavedHeightmapKeys()) scenes.Add(key);
            return scenes.ToList();
        }
        public string CurrentGameScene => _currentGameSceneName;
        public override void Dispose()
        {
            Current = null;
            _pendingDisposeScene?.Dispose();
            _pendingDisposeHosted?.Dispose();
            _activeGameScene?.Dispose();
            _hostedCustomScene?.Dispose();
            _sceneCache.Clear();
            base.Dispose();
        }
        private class BasicGameScene : GameScene
        {
            public BasicGameScene(IRenderContext rc, IControlContext cc, nint w, IGameServer s, EventBus eb, SceneData data)
                : base(rc, cc, w, s, eb, data) { }
        }
    }
}
