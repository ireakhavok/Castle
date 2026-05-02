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
                    _projectData = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();
                }
            }
            if (_projectData == null) _projectData = new ProjectData();
            if (_projectData.Scenes == null) _projectData.Scenes = new Dictionary<string, SceneData>();

            _currentGameSceneName = _projectData.LastOpenedScene ?? (_projectData.Scenes.Keys.FirstOrDefault() ?? "Main");

            Console.WriteLine($"[EditorScene.LoadProjectData] Active scene: '{_currentGameSceneName}'");

            ActivateScene(_currentGameSceneName);
        }

        private void ActivateScene(string sceneName)
        {
            _currentGameSceneName = sceneName;
            if (_projectData != null) _projectData.LastOpenedScene = sceneName;

            Level level = ProjectSettings.Current.CurrentLevel;
            if (level == null || level.Name != sceneName)
            {
                Console.WriteLine($"[EditorScene.ActivateScene] Creating/loading Level for '{sceneName}'");
                level = CreateOrLoadLevel(sceneName);
                ProjectSettings.Current.SetCurrentLevel(level);
            }

            _activeGameScene?.Dispose();

            if (_projectData.Scenes.TryGetValue(sceneName, out SceneData sceneData))
            {
                bool isTerrainScene = sceneData.SceneType == "TerrainTest" ||
                                    !string.IsNullOrEmpty(sceneData.Terrain?.HeightmapPath) ||
                                    sceneName.Contains("Terrain", StringComparison.OrdinalIgnoreCase);

                if (isTerrainScene)
                    _activeGameScene = new TerrainCreatorScene(_renderContext, _controlContext, _window, _server, _eventBus, sceneData);
                else
                    _activeGameScene = new BasicGameScene(_renderContext, _controlContext, _window, _server, _eventBus, sceneData);

                _activeGameScene.Initialize(_width, _height);
                _activeGameScene.LoadSceneData(sceneData);

                // Load models for entities
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

                Console.WriteLine($"[EditorScene] Activated '{sceneName}' (entities: {level.Entities.Count})");
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

        // Keep this for explicit project saves only (never called on scene switch)
        public void FlushActiveSceneData()
        {
            Console.WriteLine($"[EditorScene] FlushActiveSceneData called for '{_currentGameSceneName}' (manual save only)");
            // original flush logic can be re-enabled here later if you want manual save
        }

        public void SwitchGameScene(string sceneName)
        {
            if (sceneName == _currentGameSceneName) return;

            Console.WriteLine($"[EditorScene.SwitchGameScene] Switching from '{_currentGameSceneName}' → '{sceneName}' (in-memory, no disk flush)");

            // NO FlushActiveSceneData() — scenes stay in memory/cache as you requested
            ActivateScene(sceneName);

            Console.WriteLine($"[EditorScene] Successfully switched to scene '{sceneName}'");
        }

        public override void Resize(int width, int height)
        {
            base.Resize(width, height);
            _activeGameScene?.Resize(width, height);
        }

        public void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, bool cameraMode = true)
        {
            if (_activeGameScene is TerrainCreatorScene terrainScene)
                terrainScene.Update(deltaTime, relMousePos, mouseDown, mousePressed, mouseReleased, cameraMode);
            else if (_activeGameScene != null)
                _activeGameScene.Update(deltaTime);
        }

        public override void Update(float deltaTime)
        {
            _activeGameScene?.Update(deltaTime);
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
            _activeGameScene?.Dispose();
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