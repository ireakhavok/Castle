// Folder: SiegeEngine/Core/Managers
// File: SceneManager.cs
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;
using System;
using System.Numerics;
namespace SiegeEngine.Core.Managers
{
    public class SceneManager
    {
        private readonly EventBus _eventBus;
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly nint _window;
        private readonly ModManager _modManager;
        private readonly UISettingsManager _settingsManager;
        private readonly ISteamEngine _steamEngine;
        private readonly InputHandler _inputHandler;
        private readonly MenuPanel _menuPanel;
        private Scene _currentScene;
        private Player _player;
        private PlayerMovement _playerMovement;
        private ModelManager _modelManager;
        private IGameServer _server;
        public SceneManager(EventBus eventBus, IRenderContext renderContext, IControlContext controlContext, nint window, ModManager modManager, UISettingsManager settingsManager, ISteamEngine steamEngine, InputHandler inputHandler, MenuPanel menuPanel)
        {
            _eventBus = eventBus;
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _modManager = modManager;
            _settingsManager = settingsManager;
            _steamEngine = steamEngine;
            _inputHandler = inputHandler;
            _menuPanel = menuPanel;
            _eventBus.Subscribe<SwitchSceneEvent>(OnSwitchScene);
        }
        public void Update(float deltaTime) => _currentScene?.Update(deltaTime);
        public void Render() => _currentScene?.Render(_server?.GetEntities() ?? Array.Empty<Entity>());
        public void Resize(int width, int height) => _currentScene?.Resize(width, height);
        public void Dispose() { _currentScene?.Dispose(); _currentScene = null; }
        private void OnSwitchScene(SwitchSceneEvent e)
        {
            Console.WriteLine($"SceneManager: Switching to '{e.SceneName}'");
            Dispose();
            if (_menuPanel != null) _menuPanel.Visible = false;
            _server = new ClientGameServerProxy(_eventBus);
            var predictionSystem = new ClientPredictionSystem(_server, _eventBus);
            _server.AddSystem(predictionSystem);
            _modelManager = new ModelManager(_renderContext);
            _modelManager.LoadModel(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "Man_Mesh.fbx"));
            Vector3 startPos = new Vector3(10, 10, 0);
            _player = new Player(1, startPos, ((SteamEngine)_steamEngine).GetSteamId());
            _player.InitializeCamera(_controlContext, _window);
            var playerEntity = new Entity { Id = 1, Type = "Player" };
            playerEntity.AddComponent(_player);
            playerEntity.AddComponent(_player.Physics);
            playerEntity.AddComponent(new ModelComponent { Model = _player.Model, Key = "man_mesh" });
            _server.AddEntity(playerEntity);
            _playerMovement = new PlayerMovement(_inputHandler, predictionSystem, _eventBus);
            var ctx = new SceneContext { RenderContext = _renderContext, ControlContext = _controlContext, Window = _window, Server = _server, EventBus = _eventBus, Player = _player, PlayerMovement = _playerMovement, ModelManager = _modelManager };
            _currentScene = (Scene)SceneRegistry.Create(e.SceneName, ctx);
            _currentScene.SetPlayer(_player);
            _currentScene.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight);
            Console.WriteLine($"SceneManager: '{e.SceneName}' initialized successfully via registry.");
        }
        public void SwitchToRuntimeGameplay(string projectPath, string levelName, Level currentLevel = null)
        {
            Console.WriteLine($"SceneManager: Loading runtime gameplay with FULL snapshot - project '{projectPath}' level '{levelName}' Entities={currentLevel?.Entities?.Count ?? 0}");
            var level = currentLevel ?? new Level { Name = levelName };
            var ctx = SceneContext.CreateForRuntime(level, new SceneData { Name = levelName }, _renderContext, _controlContext, _window, new ClientGameServerProxy(_eventBus), _eventBus);
            ctx.PlayProjectPath = projectPath;
            ctx.LoadLevelName = levelName;
            ctx.CurrentLevel = level;
            _modelManager = new ModelManager(_renderContext);
            ctx.ModelManager = _modelManager;
            if (!string.IsNullOrEmpty(projectPath))
            {
                ModelManager.EnsurePacksLoaded(projectPath, level); // robust scan for ALL *_pack folders
            }
            _currentScene = (Scene)SceneRegistry.Create("RuntimeGameplay", ctx);
            _currentScene.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight);
            Console.WriteLine("[SceneManager] RuntimeGameplayScene active with FULL editor snapshot");
        }
    }
}