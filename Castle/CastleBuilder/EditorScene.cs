// Folder: CastleBuilder
// File: EditorScene.cs
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
using System.Text.Json;
using System.Linq;

namespace CastleBuilder
{
    public class EditorScene : Scene
    {
        private ProjectData _projectData;
        private string _currentGameScene = string.Empty;

        public EditorScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus)
        {
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            LoadProjectData();
        }

        private void LoadProjectData()
        {
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return;

            string jsonPath = Path.Combine(projectPath, "project.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                _projectData = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();

                if (_projectData.Scenes == null)
                {
                    _projectData.Scenes = new Dictionary<string, SceneData>();
                }

                _currentGameScene = _projectData.LastOpenedScene ?? (_projectData.Scenes.Keys.FirstOrDefault() ?? string.Empty);
                Console.WriteLine($"[CastleBuilder.EditorScene] Loaded project '{_projectData.Name}' | Scenes: {string.Join(", ", _projectData.Scenes.Keys)} | Current: {_currentGameScene}");
            }
        }

        public void SwitchGameScene(string sceneName)
        {
            if (_projectData?.Scenes?.ContainsKey(sceneName) == true)
            {
                _currentGameScene = sceneName;
                Console.WriteLine($"[CastleBuilder.EditorScene] Switched GAME scene → {sceneName}");
            }
        }

        public override void Update(float deltaTime)
        {
            // Future: load SceneData.Terrain, SceneData.Entities, SceneData.Environment into active scene
        }

        public override void Render(IReadOnlyList<Entity> entities)
        {
            _renderContext.ClearColor(0.12f, 0.12f, 0.18f, 1f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            Console.WriteLine($"[CastleBuilder.EditorScene] Rendering GAME scene: {_currentGameScene}");
        }

        public List<string> GetAvailableScenes() => _projectData?.Scenes?.Keys.ToList() ?? new List<string>();
        public string CurrentGameScene => _currentGameScene;
    }
}