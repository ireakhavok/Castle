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
        public bool TryPerformEntitySelectionRaycast(Vector2 normalizedMouse, float contentW, float contentH, out int entityId, out Vector3 hitPoint)
        {
            entityId = -1;
            hitPoint = Vector3.Zero;
            if (!(_activeGameScene is TerrainCreatorScene tcs))
                return false;
            // PROPERLY ORGANIZED BASE-CONTEXT RAYCAST: use the panel's exact viewport (contentW/contentH)
            // No assumption about scene aspect ratio or full-screen panel. The ray now goes exactly through the mouse cursor.
            if (!tcs.GetMouseRay(normalizedMouse, contentW, contentH, out Vector3 rayOrigin, out Vector3 rayDir))
                return false;

            // === DEBUG PRINTS (future-proof, can be compiled out later) ===
            Console.WriteLine($"[DEBUG Raycast] normalizedMouse=({normalizedMouse.X:F3},{normalizedMouse.Y:F3}) viewport=({contentW:F0}x{contentH:F0})");
            Console.WriteLine($"[DEBUG Raycast] rayOrigin=({rayOrigin.X:F2},{rayOrigin.Y:F2},{rayOrigin.Z:F2}) rayDir=({rayDir.X:F3},{rayDir.Y:F3},{rayDir.Z:F3})");

            // Terrain hit for fallback
            bool terrainHit = tcs.TryTerrainRaycast(rayOrigin, rayDir, out Vector3 terrainHitPoint);
            float terrainDistance = terrainHit ? Vector3.Distance(rayOrigin, terrainHitPoint) : float.MaxValue;
            Console.WriteLine($"[DEBUG Raycast] terrainHit={terrainHit} terrainDist={terrainDistance:F2} terrainPoint=({terrainHitPoint.X:F2},{terrainHitPoint.Y:F2},{terrainHitPoint.Z:F2})");

            // Entity OBB test along ray (ordered by t) - rotation-aware via PhysicsComponent.RayIntersects
            float closestEntityDistance = float.MaxValue;
            Entity closestEntity = null;
            foreach (var e in GetEntities())
            {
                var physics = e.GetComponent<PhysicsComponent>();
                if (physics == null) continue;
                Vector3 dummyHitPoint;
                Console.WriteLine($"[DEBUG Raycast] Entity {e.Id} AABB min=({physics.Position.X - physics.Size.X / 2:F2},{physics.Position.Y - physics.Size.Y / 2:F2},{physics.Position.Z - physics.Size.Z / 2:F2}) max=({physics.Position.X + physics.Size.X / 2:F2},{physics.Position.Y + physics.Size.Y / 2:F2},{physics.Position.Z + physics.Size.Z / 2:F2}) Size={physics.Size}");

                if (physics.RayIntersects(rayOrigin, rayDir, out float dist, out dummyHitPoint) && dist < closestEntityDistance && dist < 10000f)
                {
                    closestEntityDistance = dist;
                    closestEntity = e;
                    Console.WriteLine($"[DEBUG Raycast] → Entity {e.Id} HIT! dist={dist:F2}");
                }
            }
            if (closestEntity != null && closestEntityDistance < terrainDistance)
            {
                entityId = closestEntity.Id;
                hitPoint = rayOrigin + rayDir * closestEntityDistance;
                Console.WriteLine($"[EditorScene] Entity selection raycast hit entity {entityId} (dist {closestEntityDistance:F2})");
                return true;
            }
            if (terrainHit)
            {
                hitPoint = terrainHitPoint;
                Console.WriteLine($"[EditorScene] Raycast hit terrain only at {terrainHitPoint} - clearing selection");
            }
            return false;
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
                            modelComp.Model = fbxModel;
                        Console.WriteLine($"[EditorScene.RegisterAllAssetPacks] Loaded animation pack '{modelComp.Key}' from disk");
                    }
                    else
                    {
                        string packId = modelComp.Key;
                        ModelManager.Instance.RegisterFBXAsPackInMemory(packId);
                        if (ModelManager.Instance.TryGetModel(packId, out var fbxModel))
                            modelComp.Model = fbxModel;
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