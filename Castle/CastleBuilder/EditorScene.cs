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
        // IDE-only cache for fast scene switching
        private readonly ProjectSceneCache _sceneCache = new ProjectSceneCache();
        // Deferred disposal to prevent "disposed object" crashes during the same frame
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
            _currentGameSceneName = _projectData.LastOpenedScene ?? (_projectData.Scenes.Keys.FirstOrDefault() ?? "Main");
            _sceneCache.Clear();
            _pendingDisposeScene?.Dispose();
            _pendingDisposeScene = null;
            Console.WriteLine($"[EditorScene.LoadProjectData] Active scene: '{_currentGameSceneName}'");
            ActivateScene(_currentGameSceneName);
        }
        private void ActivateScene(string sceneName)
        {
            // === LEVEL-CENTRIC DESIGN: reuse Level already populated by BlueprintManager.OnLoadProject ===
            var currentLevel = ProjectSettings.Current.CurrentLevel;
            if (currentLevel != null && currentLevel.Name == sceneName)
            {
                // Fast path - Level is already the single source of truth with correct entity positions
                if (_sceneCache.TryGet(sceneName, out var cachedScene, out var cachedLevel))
                {
                    if (_activeGameScene != null && _activeGameScene != cachedScene)
                        _pendingDisposeScene = _activeGameScene;
                    _activeGameScene = cachedScene;
                    _currentGameSceneName = sceneName;
                    if (_projectData != null) _projectData.LastOpenedScene = sceneName;
                    SyncLevelToRuntimeServer(currentLevel);
                    Console.WriteLine($"[EditorScene] Activated CACHED scene '{sceneName}' (Level already authoritative - positions preserved)");
                    return;
                }
                // First activation for this Level - use the existing one
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
                SyncLevelToRuntimeServer(currentLevel);
                if (_activeGameScene is TerrainCreatorScene tcs)
                {
                    float[,] cached = ProjectSettings.Current.GetUnsavedHeightmap(sceneName);
                    float[,] heightmapToUse = cached ?? tcs.GetHeightmap();
                    ProjectSettings.Current.SetCurrentTerrain(sd, heightmapToUse, sceneName, sd.Terrain?.HeightmapPath);
                    if (!string.IsNullOrEmpty(sd.Terrain?.HeightmapPath))
                        tcs.LoadTerrain(sd.Terrain.HeightmapPath);
                }
                _sceneCache.Store(sceneName, _activeGameScene, currentLevel);
                Console.WriteLine($"[EditorScene] Activated scene '{sceneName}' using authoritative Level (entities: {currentLevel.Entities.Count} - positions preserved)");
                return;
            }

            // Fallback (first-time or no Level yet) - legacy path
            _currentGameSceneName = sceneName;
            if (_projectData != null) _projectData.LastOpenedScene = sceneName;
            Level level = ProjectSettings.Current.CurrentLevel;
            if (level == null || level.Name != sceneName)
            {
                Console.WriteLine($"[EditorScene.ActivateScene] Creating/loading Level for '{sceneName}'");
                level = CreateOrLoadLevel(sceneName);
                ProjectSettings.Current.SetCurrentLevel(level);
            }
            // Destructive: queue old scene for disposal
            if (_activeGameScene != null)
                _pendingDisposeScene = _activeGameScene;
            if (_projectData.Scenes.TryGetValue(sceneName, out SceneData sceneData))
            {
                bool isTerrainScene = sceneData.SceneType == "TerrainTest" ||
                                    !string.IsNullOrEmpty(sceneData.Terrain?.HeightmapPath) ||
                                    sceneName.Contains("Terrain", StringComparison.OrdinalIgnoreCase);
                _activeGameScene = isTerrainScene
                    ? new TerrainCreatorScene(_renderContext, _controlContext, _window, _server, _eventBus, sceneData)
                    : new BasicGameScene(_renderContext, _controlContext, _window, _server, _eventBus, sceneData);
                _activeGameScene.Initialize(_width, _height);
                _activeGameScene.LoadSceneData(sceneData);
                // Model loading (unchanged)
                if (ModelManager.Instance != null)
                {
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
                            }
                        }
                    }
                }
                SyncLevelToRuntimeServer(level);
                if (_activeGameScene is TerrainCreatorScene tcs)
                {
                    float[,] cached = ProjectSettings.Current.GetUnsavedHeightmap(sceneName);
                    float[,] heightmapToUse = cached ?? tcs.GetHeightmap();
                    ProjectSettings.Current.SetCurrentTerrain(sceneData, heightmapToUse, sceneName, sceneData.Terrain?.HeightmapPath);
                    if (!string.IsNullOrEmpty(sceneData.Terrain?.HeightmapPath))
                        tcs.LoadTerrain(sceneData.Terrain.HeightmapPath);
                }
                // Cache it for next time
                _sceneCache.Store(sceneName, _activeGameScene, level);
                Console.WriteLine($"[EditorScene] Activated NEW scene '{sceneName}' (entities: {level.Entities.Count}) - cached for future switches");
            }
        }
        private Level CreateOrLoadLevel(string sceneName)
        {
            if (_projectData.Scenes.TryGetValue(sceneName, out var sceneData))
            {
                var level = new Level(_eventBus) { Name = sceneName };
                if (sceneData.Entities != null)
                    foreach (var ed in sceneData.Entities)
                        level.AddEntity(Entity.FromData(ed));
                level.Terrain = sceneData.Terrain ?? new TerrainData();
                level.Environment = sceneData.Environment ?? new EnvironmentSettings();
                return level;
            }
            return new Level(_eventBus) { Name = sceneName };
        }
        private void SyncLevelToRuntimeServer(Level level)
        {
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
        public void FlushActiveSceneData()
        {
            Console.WriteLine($"[EditorScene.FlushActiveSceneData] Called for scene '{_currentGameSceneName}'");
            // Use the live cached TerrainCreatorScene (this is the key to correct save after switch)
            if (_sceneCache.TryGet(_currentGameSceneName, out var cachedScene, out var cachedLevel) &&
                cachedScene is TerrainCreatorScene tcs)
            {
                string terrainName = _currentGameSceneName ?? "UntitledTerrain";
                tcs.SaveTerrain(terrainName);
                // Sync the final HeightmapPath back into Level and ProjectData
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
            // Clean entity flush (unchanged)
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
            // Safe disposal at the start of the next frame (prevents disposed-object crash during render)
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