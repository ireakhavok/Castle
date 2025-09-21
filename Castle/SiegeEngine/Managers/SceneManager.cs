using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Managers;
using SiegeEngine.Networking;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Rendering;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;
using System;
using System.Numerics;
namespace SiegeEngine.Managers
{
    public class SceneManager
    {
        private readonly EventBus _eventBus;
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private readonly ModManager _modManager;
        private readonly UISettingsManager _settingsManager;
        private readonly ISteamEngine _steamEngine;
        private readonly InputHandler _inputHandler;
        private Scene _currentScene;
        private Player _player;
        private PlayerMovement _playerMovement;
        private ModelManager _modelManager;
        private IGameServer _server;
        public SceneManager(EventBus eventBus, IRenderContext renderContext, IControlContext controlContext, IntPtr window, ModManager modManager, UISettingsManager settingsManager, ISteamEngine steamEngine, InputHandler inputHandler)
        {
            _eventBus = eventBus;
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _modManager = modManager;
            _settingsManager = settingsManager;
            _steamEngine = steamEngine;
            _inputHandler = inputHandler;
            _eventBus.Subscribe<SwitchSceneEvent>(OnSwitchScene);
        }
        public void Update(float deltaTime)
        {
            if (_currentScene != null)
            {
                _currentScene.Update(deltaTime);
            }
        }
        public void Render()
        {
            if (_currentScene != null)
            {
                _currentScene.Render(_server.GetEntities());
            }
        }
        public void Resize(int width, int height)
        {
            if (_currentScene != null)
            {
                _currentScene.Resize(width, height);
            }
        }
        public void Dispose()
        {
            if (_currentScene != null)
            {
                _currentScene.Dispose();
                _currentScene = null;
            }
        }
        private void OnSwitchScene(SwitchSceneEvent e)
        {
            Console.WriteLine($"SceneManager: SwitchSceneEvent received for {e.SceneName}");
            Dispose(); // Clean up previous scene
            _server = new ClientGameServerProxy(_eventBus); // Secure proxy
            var predictionSystem = new ClientPredictionSystem(_server, _eventBus);
            _server.AddSystem(predictionSystem);
            _modelManager = new ModelManager("Mods/Models", "Assets/Models", _modManager, _renderContext);
            _modelManager.LoadCharacters();
            Vector3 startPos = new Vector3(10, 10, 0);
            _player = new Player(1, startPos, ((SteamEngine)_steamEngine).GetSteamId(), _modelManager);
            _player.InitializeCamera(_controlContext, _window);
            var playerEntity = new Entity { Id = 1, Type = "Player" };
            playerEntity.AddComponent(_player);
            playerEntity.AddComponent(_player.Physics);
            _server.AddEntity(playerEntity);
            _playerMovement = new PlayerMovement(_inputHandler, predictionSystem, _eventBus);
            if (e.SceneName == "Sandbox")
            {
                _currentScene = new SandboxScene(_renderContext, _controlContext, _window, _player, _server, _playerMovement, _eventBus, _modelManager);
            }
            // Add more scene types here for generic support, e.g., else if (e.SceneName == "Editor") { _currentScene = new EditorScene(...); }
            else
            {
                Console.WriteLine($"SceneManager: Unknown scene {e.SceneName}, defaulting to Sandbox");
                _currentScene = new SandboxScene(_renderContext, _controlContext, _window, _player, _server, _playerMovement, _eventBus, _modelManager);
            }
            _currentScene.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight);
        }
    }
}