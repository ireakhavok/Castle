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
        public EditorScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus) { }
        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            LoadProjectData();
        }
        public void LoadProjectData()
        {
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                Console.WriteLine("[EditorScene] No active project - creating default 200×200 terrain scene");
                _currentGameSceneName = "Default";
                _activeGameScene = new TerrainCreatorScene(_renderContext, _controlContext, _window, _server, _eventBus);
                _activeGameScene.Initialize(_width, _height);
                if (_activeGameScene is TerrainCreatorScene tcs) tcs.CreateBlank();
                return;
            }
            string jsonPath = Path.Combine(projectPath, "project.json");
            if (!File.Exists(jsonPath)) return;
            string json = File.ReadAllText(jsonPath);
            _projectData = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();
            if (_projectData.Scenes == null) _projectData.Scenes = new Dictionary<string, SceneData>();
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
                if (sceneData.SceneType == "TerrainTest" || sceneData.Terrain.HeightmapPath != null || _currentGameSceneName.Contains("Terrain"))
                {
                    _activeGameScene = new TerrainCreatorScene(_renderContext, _controlContext, _window, _server, _eventBus, sceneData);
                }
                else
                {
                    _activeGameScene = new BasicGameScene(_renderContext, _controlContext, _window, _server, _eventBus, sceneData);
                }
                _activeGameScene.Initialize(_width, _height);
                _activeGameScene.LoadSceneData(sceneData);
                Console.WriteLine($"[EditorScene] Activated GameScene '{_currentGameSceneName}' from SceneData");
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
            // FIXED: do NOT double-clear when the inner scene is TerrainCreatorScene (this was causing the UI corruption)
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
            _activeGameScene?.Dispose();
            base.Dispose();
        }
        private class BasicGameScene : GameScene
        {
            public BasicGameScene(IRenderContext rc, IControlContext cc, nint w, IGameServer s, EventBus eb, SceneData data) : base(rc, cc, w, s, eb, data) { }
            public override void Render(IReadOnlyList<Entity> entities) { }
        }
    }
}