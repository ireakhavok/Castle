// Folder: SiegeEngine.Core.Managers
// File: SceneManager.cs
using SiegeEngine.Core.AssetParsing.V2;
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
using System.Reflection;

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
        private ModelManagerV2 _modelManager;
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
            if (_menuPanel != null)
            {
                _menuPanel.Visible = false; // Hide menu on scene switch
            }
            _server = new ClientGameServerProxy(_eventBus); // Secure proxy
            var predictionSystem = new ClientPredictionSystem(_server, _eventBus);
            _server.AddSystem(predictionSystem);
            _modelManager = new ModelManagerV2(_renderContext);
            _modelManager.LoadModel(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "Man_Mesh.fbx"));
            Vector3 startPos = new Vector3(10, 10, 0);
            _player = new Player(1, startPos, ((SteamEngine)_steamEngine).GetSteamId(), _modelManager);
            _player.InitializeCamera(_controlContext, _window);
            var playerEntity = new Entity { Id = 1, Type = "Player" };
            playerEntity.AddComponent(_player);
            playerEntity.AddComponent(_player.Physics);
            var modelComp = new ModelComponent { Model = _player.Model, Key = "man_mesh" };
            playerEntity.AddComponent(modelComp);
            _server.AddEntity(playerEntity);
            _playerMovement = new PlayerMovement(_inputHandler, predictionSystem, _eventBus);
            // Dynamic scene loading
            string sceneClassName = e.SceneName.Replace(" ", "_").Replace("-", "_") + "Scene";
            Type sceneType = Type.GetType($"SiegeEngine.Scenes.StartingPoints.{sceneClassName}");
            if (sceneType != null && sceneType.IsSubclassOf(typeof(Scene)))
            {
                ConstructorInfo ctor = sceneType.GetConstructor(new Type[]
                {
                    typeof(IRenderContext), typeof(IControlContext), typeof(nint),
                    typeof(Player), typeof(IGameServer), typeof(PlayerMovement), typeof(EventBus), typeof(ModelManagerV2)
                });
                if (ctor != null)
                {
                    _currentScene = (Scene)ctor.Invoke(new object[]
                    {
                        _renderContext, _controlContext, _window,
                        _player, _server, _playerMovement, _eventBus, _modelManager
                    });
                    _currentScene.SetPlayer(_player);
                    _currentScene.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight);
                    Console.WriteLine($"SceneManager: Dynamically loaded and initialized {sceneClassName}");
                }
                else
                {
                    Console.WriteLine($"SceneManager: Constructor not found for {sceneClassName}");
                }
            }
            else
            {
                Console.WriteLine($"SceneManager: Scene type not found or invalid: {sceneClassName}");
            }
        }
    }
}