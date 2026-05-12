// Folder: CastleBuilder
// File: EditorScene.cs
using Keystone;
using MapRoom;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ToolChest;
namespace CastleBuilder
{
    public class EditorScene : Scene
    {
        private ProjectData _projectData;
        private string _currentGameSceneName = string.Empty;
        private GameScene _activeGameScene;
        private readonly ProjectSceneCache _sceneCache = new ProjectSceneCache();
        private GameScene _pendingDisposeScene;
        public static EditorScene Current { get; private set; }
        public EditorScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus) { }
        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            Current = this;
            LoadProjectData();
        }
        public ProjectData GetProjectData() => _projectData;
        public IReadOnlyList<Entity> GetEntities() => _server.GetEntities();
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

            // Convert NDC box to pixel box (this was the bug)
            float minX = Math.Min(ndcStart.X, ndcEnd.X) * contentW;
            float maxX = Math.Max(ndcStart.X, ndcEnd.X) * contentW;
            float minY = Math.Min(ndcStart.Y, ndcEnd.Y) * contentH;
            float maxY = Math.Max(ndcStart.Y, ndcEnd.Y) * contentH;

            Console.WriteLine($"[EditorScene] Box select PIXEL rect: X({minX:F1}-{maxX:F1}) Y({minY:F1}-{maxY:F1})");

            foreach (var e in GetEntities())
            {
                var physics = e.GetComponent<PhysicsComponent>();
                if (physics == null) continue;

                if (IsBoxInFrustum(tcs, minX, maxX, minY, maxY, contentW, contentH, physics))
                {
                    selected.Add(e.Id);
                    Console.WriteLine($"[EditorScene] Box selected entity {e.Id}");
                }
            }
            return selected;
        }

        private bool IsBoxInFrustum(TerrainCreatorScene tcs, float minX, float maxX, float minY, float maxY, float contentW, float contentH, PhysicsComponent physics)
        {
            // Fast center test (pixel space)
            Vector3 center = physics.Position;
            if (!ProjectWorldToScreen(center, contentW, contentH, out Vector2 screenCenter))
                return false;
            if (screenCenter.X >= minX && screenCenter.X <= maxX && screenCenter.Y >= minY && screenCenter.Y <= maxY)
                return true;

            // 8 OBB corners test (pixel space)
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
                clientProxy.ClearEntities();
                foreach (var entity in level.Entities)
                    clientProxy.AddEntity(entity);
                Console.WriteLine($"[EditorScene] Synced {level.Entities.Count} entities to runtime proxy");
            }
        }
        public void LoadProjectData()
        {
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
            string levelName = ProjectSettings.Current.CurrentLevel?.Name;
            if (!string.IsNullOrEmpty(levelName) && levelName != "Main")
            {
                _currentGameSceneName = levelName;
                Console.WriteLine($"[EditorScene.LoadProjectData] No-project - using Level name from NewTerrainPanel: '{_currentGameSceneName}'");
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
            Console.WriteLine($"[EditorScene.LoadProjectData] Active scene: '{_currentGameSceneName}'");
            ActivateScene(_currentGameSceneName);
        }
        private void ActivateScene(string sceneName)
        {
            Level level = ProjectSettings.Current.CurrentLevel;
            if (level == null || level.Name != sceneName)
            {
                Console.WriteLine($"[EditorScene.ActivateScene] Creating fresh Level for scene '{sceneName}'");
                level = CreateOrLoadLevel(sceneName);
                ProjectSettings.Current.SetCurrentLevel(level);
            }
            RegisterAllAssetPacks(level);
            if (_sceneCache.TryGet(sceneName, out var cachedScene, out var cachedLevel))
            {
                if (_activeGameScene != null && _activeGameScene != cachedScene)
                    _pendingDisposeScene = _activeGameScene;
                _activeGameScene = cachedScene;
                _currentGameSceneName = sceneName;
                if (_projectData != null) _projectData.LastOpenedScene = sceneName;
                SyncCurrentLevelToRuntimeServer();
                Console.WriteLine($"[EditorScene] Activated CACHED scene '{sceneName}' (Level authoritative - models hydrated - entities: {level.Entities.Count})");
                return;
            }
            _currentGameSceneName = sceneName;
            if (_projectData != null) _projectData.LastOpenedScene = sceneName;
            if (_activeGameScene != null)
                _pendingDisposeScene = _activeGameScene;
            bool isTerrainScene = _projectData.Scenes.TryGetValue(sceneName, out var sd) &&
                                  (sd.SceneType == "TerrainTest" || !string.IsNullOrEmpty(sd.Terrain?.HeightmapPath) || sceneName.Contains("Terrain", StringComparison.OrdinalIgnoreCase));
            _activeGameScene = isTerrainScene
                ? new TerrainCreatorScene(_renderContext, _controlContext, _window, _server, _eventBus, sd)
                : new BasicGameScene(_renderContext, _controlContext, _window, _server, _eventBus, sd);
            _activeGameScene.Initialize(_width, _height);
            _activeGameScene.LoadSceneData(sd);
            SyncCurrentLevelToRuntimeServer();
            if (_activeGameScene is TerrainCreatorScene tcs)
            {
                float[,] cached = ProjectSettings.Current.GetUnsavedHeightmap(sceneName);
                float[,] heightmapToUse = cached ?? ProjectSettings.Current.CurrentHeightmap ?? tcs.GetHeightmap();
                ProjectSettings.Current.SetCurrentTerrain(sd, heightmapToUse, sceneName, sd.Terrain?.HeightmapPath);
                if (!string.IsNullOrEmpty(sd.Terrain?.HeightmapPath))
                    tcs.LoadTerrain(sd.Terrain.HeightmapPath);
                else if (heightmapToUse != null)
                {
                    tcs.LoadSceneData(new SceneData { Name = sceneName, Terrain = new TerrainData() });
                }
            }
            _sceneCache.Store(sceneName, _activeGameScene, level);
            Console.WriteLine($"[EditorScene] Activated scene '{sceneName}' using authoritative Level (entities: {level.Entities.Count} - models hydrated)");
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
                                Console.WriteLine($"[EditorScene.RegisterAllAssetPacks] Synced model bounds for loaded entity {entity.Id} (Key='{modelComp.Key}') Size={physics.Size} LocalAABB=({physics.LocalBoundsMinCm}..{physics.LocalBoundsMaxCm})");
                            }
                        }
                        Console.WriteLine($"[EditorScene.RegisterAllAssetPacks] Loaded animation pack '{modelComp.Key}' from disk");
                    }
                    else
                    {
                        string packId = modelComp.Key;
                        ModelManager.Instance.RegisterFBXAsPackInMemory(packId);
                        if (ModelManager.Instance.TryGetModel(packId, out var fbxModel))
                        {
                            modelComp.Model = fbxModel;
                            var physics = entity.GetComponent<PhysicsComponent>();
                            if (physics != null && modelComp.Model != null)
                            {
                                physics.Size = modelComp.Model.GetBoundingSize();
                                physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                                physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
                                Console.WriteLine($"[EditorScene.RegisterAllAssetPacks] Synced model bounds for loaded entity {entity.Id} (Key='{packId}') Size={physics.Size} LocalAABB=({physics.LocalBoundsMinCm}..{physics.LocalBoundsMaxCm})");
                            }
                        }
                        Console.WriteLine($"[EditorScene.RegisterAllAssetPacks] Registered in-memory pack '{packId}' for saved entity");
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
                return level;
            }
            return new Level(_eventBus) { Name = sceneName };
        }
        public void FlushActiveSceneData()
        {
            Console.WriteLine($"[EditorScene.FlushActiveSceneData] Called for scene '{_currentGameSceneName}'");
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
                }
                if (_projectData?.Scenes != null && _projectData.Scenes.TryGetValue(_currentGameSceneName, out var sceneData))
                {
                    if (sceneData.Terrain == null) sceneData.Terrain = new TerrainData();
                    sceneData.Terrain.HeightmapPath = cachedLevel?.Terrain?.HeightmapPath ?? "";
                    ProjectSettings.Current.SetCurrentTerrain(sceneData, tcs.GetHeightmap(), _currentGameSceneName, sceneData.Terrain.HeightmapPath);
                }
                Console.WriteLine($"[EditorScene] Flushed terrain for scene '{terrainName}' → {cachedLevel?.Terrain?.HeightmapPath ?? "null"}");
            }
            else
            {
                Console.WriteLine($"[EditorScene] FlushActiveSceneData - no TerrainCreatorScene in cache for '{_currentGameSceneName}'");
            }
            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null && _projectData?.Scenes != null && _projectData.Scenes.TryGetValue(_currentGameSceneName, out var sd))
            {
                sd.Entities = level.Entities.ConvertAll(e => e.ToData());
                Console.WriteLine($"[EditorScene] Flushed {level.Entities.Count} entities into clean Entities array");
            }
        }
        public void SwitchGameScene(string sceneName)
        {
            if (sceneName == _currentGameSceneName) return;
            Console.WriteLine($"[EditorScene.SwitchGameScene] Switching from '{_currentGameSceneName}' → '{sceneName}' (destructive + cached activation)");
            ActivateScene(sceneName);
            Console.WriteLine($"[EditorScene] Successfully switched to scene '{sceneName}'");
        }
        public override void Update(float deltaTime)
        {
            if (_pendingDisposeScene != null)
            {
                _pendingDisposeScene.Dispose();
                _pendingDisposeScene = null;
            }
            _activeGameScene?.Update(deltaTime);
        }
        public void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, bool cameraMode = true)
        {
            if (_activeGameScene is TerrainCreatorScene terrainScene)
                terrainScene.Update(deltaTime, relMousePos, mouseDown, mousePressed, mouseReleased, cameraMode);
            else if (_activeGameScene != null)
                _activeGameScene.Update(deltaTime);
        }
        public override void Render(IReadOnlyList<Entity> entities)
        {
            if (!(_activeGameScene is TerrainCreatorScene))
            {
                _renderContext.ClearColor(0.12f, 0.12f, 0.18f, 1f);
                _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            }
            _activeGameScene?.Render(entities ?? GetEntities());
        }
        public override void Resize(int width, int height)
        {
            base.Resize(width, height);
            _activeGameScene?.Resize(width, height);
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
            _activeGameScene?.Dispose();
            _sceneCache.Clear();
            base.Dispose();
        }
        private class BasicGameScene : GameScene
        {
            public BasicGameScene(IRenderContext rc, IControlContext cc, nint w, IGameServer s, EventBus eb, SceneData data)
                : base(rc, cc, w, s, eb, data) { }
            public override void Render(IReadOnlyList<Entity> entities) { }
        }
    }
}