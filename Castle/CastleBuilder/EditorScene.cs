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

            Level level = ProjectSettings.Current.CurrentLevel;
            if (level == null || level.Name != _currentGameSceneName)
            {
                Console.WriteLine($"[EditorScene.LoadProjectData] No matching Level in ProjectSettings for scene '{_currentGameSceneName}' - creating from SceneData");
                if (_projectData.Scenes.TryGetValue(_currentGameSceneName, out var sceneData))
                {
                    level = new Level(_eventBus) { Name = _currentGameSceneName };
                    if (sceneData.Entities != null)
                    {
                        foreach (var ed in sceneData.Entities)
                        {
                            var entity = Entity.FromData(ed);
                            var physics = entity.GetComponent<PhysicsComponent>();
                            if (physics != null)
                            {
                                physics.Position = ed.Position;
                                Console.WriteLine($"[EditorScene] Entity '{entity.Type}' (AssetPackKey: {entity.GetComponent<ModelComponent>()?.Key ?? "none"}) - EXPLICITLY restored Position: {physics.Position}");
                            }
                            level.Entities.Add(entity);
                        }
                    }
                    level.Terrain = sceneData.Terrain ?? new TerrainData();
                    level.Environment = sceneData.Environment ?? new EnvironmentSettings();
                    ProjectSettings.Current.SetCurrentLevel(level);
                    Console.WriteLine($"[EditorScene.LoadProjectData] Created and set authoritative Level '{_currentGameSceneName}' with {level.Entities.Count} entities");
                }
                else
                {
                    level = new Level(_eventBus) { Name = _currentGameSceneName };
                    ProjectSettings.Current.SetCurrentLevel(level);
                    Console.WriteLine($"[EditorScene.LoadProjectData] Created empty authoritative Level for new scene '{_currentGameSceneName}'");
                }
            }
            else
            {
                Console.WriteLine($"[EditorScene.LoadProjectData] Using existing authoritative Level '{level.Name}' with {level.Entities.Count} entities from ProjectSettings.CurrentLevel");
            }

            ActivateCurrentGameScene(level);
        }

        private void ActivateCurrentGameScene(Level level)
        {
            if (level == null || string.IsNullOrEmpty(_currentGameSceneName) || _projectData?.Scenes == null)
            {
                _activeGameScene = null;
                return;
            }

            if (_projectData.Scenes.TryGetValue(_currentGameSceneName, out SceneData sceneData))
            {
                _activeGameScene?.Dispose();

                bool isTerrainScene = sceneData.SceneType == "TerrainTest" || !string.IsNullOrEmpty(sceneData.Terrain?.HeightmapPath) || _currentGameSceneName.Contains("Terrain");
                if (isTerrainScene)
                {
                    _activeGameScene = new TerrainCreatorScene(_renderContext, _controlContext, _window, _server, _eventBus, sceneData);
                }
                else
                {
                    _activeGameScene = new BasicGameScene(_renderContext, _controlContext, _window, _server, _eventBus, sceneData);
                }

                _activeGameScene.Initialize(_width, _height);
                _activeGameScene.LoadSceneData(sceneData);

                // === Load asset packs + populate Model reference on entities ===
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
                                {
                                    modelComp.Model = fbxModel;
                                    Console.WriteLine($"[EditorScene] Populated Model reference for entity pack '{modelComp.Key}'");
                                }
                            }
                        }
                    }
                }

                // === CRITICAL: Synchronize authoritative Level entities into runtime IGameServer ===
                SyncLevelToRuntimeServer(level);

                if (_activeGameScene is TerrainCreatorScene tcs)
                {
                    float[,] cached = ProjectSettings.Current.GetUnsavedHeightmap(_currentGameSceneName);
                    float[,] heightmapToUse = cached ?? tcs.GetHeightmap();
                    ProjectSettings.Current.SetCurrentTerrain(sceneData, heightmapToUse, _currentGameSceneName, sceneData.Terrain?.HeightmapPath);
                    if (!string.IsNullOrEmpty(sceneData.Terrain?.HeightmapPath))
                    {
                        tcs.LoadTerrain(sceneData.Terrain.HeightmapPath);
                        Console.WriteLine($"[EditorScene] Loaded terrain for '{_currentGameSceneName}' from path");
                    }
                }

                Console.WriteLine($"[EditorScene] Activated GameScene '{_currentGameSceneName}' using authoritative Level (entities: {level.Entities.Count})");
            }
        }

        private void SyncLevelToRuntimeServer(Level level)
        {
            if (level == null || _server == null) return;

            var clientProxy = _server as ClientGameServerProxy;
            if (clientProxy != null)
            {
                clientProxy.ClearEntities();
                Console.WriteLine($"[EditorScene.SyncLevelToRuntimeServer] Cleared runtime proxy for scene '{level.Name}'");

                foreach (var entity in level.Entities)
                {
                    var physics = entity.GetComponent<PhysicsComponent>();
                    Console.WriteLine($"[EditorScene.SyncLevelToRuntimeServer] Syncing entity ID={entity.Id} Type='{entity.Type}' Position={physics?.Position}");
                    clientProxy.AddEntity(entity);
                }

                Console.WriteLine($"[EditorScene] Synced {level.Entities.Count} entities from authoritative Level → ClientGameServerProxy runtime (positions preserved)");
            }
        }

        public void FlushActiveSceneData()
        {
            if (_activeGameScene is TerrainCreatorScene tcs && _projectData?.Scenes != null)
            {
                if (_projectData.Scenes.TryGetValue(_currentGameSceneName, out SceneData sceneData))
                {
                    string name = _currentGameSceneName ?? "Main";
                    tcs.SaveTerrain(name);
                    if (sceneData.Terrain == null) sceneData.Terrain = new TerrainData();
                    string currentPath = sceneData.Terrain.HeightmapPath ?? "";
                    if (!currentPath.Contains("Assets/Terrain") || currentPath.EndsWith(".tif", StringComparison.OrdinalIgnoreCase))
                    {
                        sceneData.Terrain.HeightmapPath = $"Assets/Terrain/{name}.tif";
                    }
                    ProjectSettings.Current.SetCurrentTerrain(sceneData, tcs.GetHeightmap(), _currentGameSceneName, sceneData.Terrain.HeightmapPath);
                    Console.WriteLine($"[EditorScene] Flushed terrain for scene '{name}' - path preserved: {sceneData.Terrain.HeightmapPath}");
                }
            }

            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null)
            {
                Console.WriteLine($"[EditorScene.FlushActiveSceneData] Authoritative Level '{level.Name}' updated (single source of truth - {level.Entities.Count} entities)");
            }
            else
            {
                Console.WriteLine($"[EditorScene] WARNING: Could not flush - no CurrentLevel in ProjectSettings");
            }
        }

        public void SwitchGameScene(string sceneName)
        {
            if (_projectData?.Scenes?.ContainsKey(sceneName) == true)
            {
                Console.WriteLine($"[EditorScene.SwitchGameScene] Switching from '{_currentGameSceneName}' to '{sceneName}' - flushing previous scene first");
                FlushActiveSceneData(); // Ensure previous Level is saved to ProjectData.Scenes

                _currentGameSceneName = sceneName;
                if (_projectData != null) _projectData.LastOpenedScene = sceneName;

                LoadProjectData(); // re-use full load path (flush → clear proxy → re-sync)
                Console.WriteLine($"[EditorScene] Switched GAME scene → {sceneName} (full isolation via Level + runtime proxy clear/sync)");
            }
        }

        public override void Resize(int width, int height)
        {
            base.Resize(width, height);
            _activeGameScene?.Resize(width, height);
        }

        public void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, bool cameraMode = true)
        {
            if (_activeGameScene is TerrainCreatorScene terrainScene)
            {
                terrainScene.Update(deltaTime, relMousePos, mouseDown, mousePressed, mouseReleased, cameraMode);
            }
            else if (_activeGameScene != null)
            {
                _activeGameScene.Update(deltaTime);
            }
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