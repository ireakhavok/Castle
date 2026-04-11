// Folder: CastleBuilder
// File: EditorScene.cs
using MapRoom;
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
using Keystone;
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

        public void LoadProjectData()
        {
            // ALWAYS load the full project.json first so we never lose existing scenes
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

            if (_projectData == null)
            {
                _projectData = new ProjectData();
            }
            if (_projectData.Scenes == null)
            {
                _projectData.Scenes = new Dictionary<string, SceneData>();
            }

            // CENTRAL MEMORY STORE FIRST (new scene from NewTerrainPanel)
            if (ProjectSettings.Current.CurrentSceneData != null && ProjectSettings.Current.CurrentHeightmap != null)
            {
                string newSceneName = ProjectSettings.Current.CurrentSceneName ?? "NewTerrain";
                _currentGameSceneName = newSceneName;

                // MERGE - add to existing collection, never replace it
                if (!_projectData.Scenes.ContainsKey(newSceneName))
                {
                    _projectData.Scenes[newSceneName] = ProjectSettings.Current.CurrentSceneData;
                }
                _projectData.LastOpenedScene = newSceneName;

                Console.WriteLine($"[EditorScene] Merged new scene '{newSceneName}' from central store (total scenes now: {_projectData.Scenes.Count})");
                ActivateCurrentGameScene();
                return;
            }

            // Normal project load path
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                Console.WriteLine("[EditorScene] No active project - creating default");
                _currentGameSceneName = "Default";
                _activeGameScene = new TerrainCreatorScene(_renderContext, _controlContext, _window, _server, _eventBus);
                _activeGameScene.Initialize(_width, _height);
                if (_activeGameScene is TerrainCreatorScene tcs) tcs.CreateBlank();
                var defaultSceneData = new SceneData { Name = "Default", SceneType = "TerrainTest" };
                ProjectSettings.Current.SetCurrentTerrain(defaultSceneData, ((TerrainCreatorScene)_activeGameScene).GetHeightmap(), "Default");
                return;
            }

            _currentGameSceneName = _projectData.LastOpenedScene ?? (_projectData.Scenes.Keys.FirstOrDefault() ?? "Main");
            ActivateCurrentGameScene();
        }

        private void ActivateCurrentGameScene()
        {
            if (string.IsNullOrEmpty(_currentGameSceneName) || _projectData?.Scenes == null)
            {
                _activeGameScene = null;
                return;
            }

            if (_projectData.Scenes.TryGetValue(_currentGameSceneName, out SceneData sceneData))
            {
                _activeGameScene?.Dispose();

                bool isTerrainScene = sceneData.SceneType == "TerrainTest" ||
                                    !string.IsNullOrEmpty(sceneData.Terrain?.HeightmapPath) ||
                                    _currentGameSceneName.Contains("Terrain");

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

                if (_activeGameScene is TerrainCreatorScene tcs)
                {
                    ProjectSettings.Current.SetCurrentTerrain(sceneData, tcs.GetHeightmap(), _currentGameSceneName, sceneData.Terrain?.HeightmapPath);
                    if (!string.IsNullOrEmpty(sceneData.Terrain?.HeightmapPath))
                    {
                        tcs.LoadTerrain(sceneData.Terrain.HeightmapPath);
                        Console.WriteLine($"[EditorScene] Loaded saved terrain (relative): {sceneData.Terrain.HeightmapPath}");
                    }
                }

                Console.WriteLine($"[EditorScene] Activated GameScene '{_currentGameSceneName}' (all previous scenes preserved)");
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
                    sceneData.Terrain.HeightmapPath = $"Assets/Terrain/{name}.tif";

                    ProjectSettings.Current.SetCurrentTerrain(sceneData, tcs.GetHeightmap(), _currentGameSceneName, sceneData.Terrain.HeightmapPath);

                    Console.WriteLine($"[EditorScene] Flushed terrain - relative path stored: {sceneData.Terrain.HeightmapPath}");
                }
            }
        }

        public void SwitchGameScene(string sceneName)
        {
            if (_projectData?.Scenes?.ContainsKey(sceneName) == true)
            {
                _currentGameSceneName = sceneName;
                if (_projectData != null) _projectData.LastOpenedScene = sceneName;
                ActivateCurrentGameScene();
                Console.WriteLine($"[EditorScene] Switched GAME scene → {sceneName}");
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

        public List<string> GetAvailableScenes() => _projectData?.Scenes?.Keys.ToList() ?? new List<string>();

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