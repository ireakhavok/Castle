using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;
using System;
using System.Numerics;
using System.Text.Json;

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

        public void Dispose()
        {
            _currentScene?.Dispose();
            _currentScene = null;
        }

        private void OnSwitchScene(SwitchSceneEvent e)
        {
            Console.WriteLine($"SceneManager: Switching to '{e.SceneName}'");
            Dispose();
            if (_menuPanel != null) _menuPanel.Visible = false;
            _server = new ClientGameServerProxy(_eventBus);
            var predictionSystem = new ClientPredictionSystem(_server, _eventBus);
            _server.AddSystem(predictionSystem);
            _server.AddSystem(new AnimationSystem(_server));
            _server.AddSystem(new AudioSystem(_server, _eventBus, false, null, _renderContext));
            _modelManager = new ModelManager(_renderContext);
            var ctx = new SceneContext
            {
                RenderContext = _renderContext,
                ControlContext = _controlContext,
                Window = _window,
                Server = _server,
                EventBus = _eventBus,
                Player = null,
                PlayerMovement = null,
                ModelManager = _modelManager
            };
            ScriptLoader.ActivateProjectScripts(ctx, _inputHandler, predictionSystem);
            if (ctx.PlayerMovement == null)
            {
                ctx.PlayerMovement = new PlayerMovement(_inputHandler, predictionSystem, _eventBus);
            }
            _playerMovement = ctx.PlayerMovement;
            _player = ctx.Player;
            _currentScene = (Scene)SceneRegistry.Create(e.SceneName, ctx);
            _currentScene.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight);
            Console.WriteLine($"SceneManager: '{e.SceneName}' initialized successfully via registry.");
        }

        public void SwitchToRuntimeGameplay(string projectPath, string levelName, string levelDataPayload = null, string sceneDataPayload = null, Level currentLevel = null)
        {
            Console.WriteLine($"SceneManager: Loading runtime gameplay with FULL snapshot - project '{projectPath}' level '{levelName}' Entities={currentLevel?.Entities?.Count ?? 0} - levelPayload present: {levelDataPayload != null}, scenePayload present: {sceneDataPayload != null}");
            Level level = currentLevel;
            SceneData reconstructedSceneData = null;
            if (!string.IsNullOrEmpty(levelDataPayload))
            {
                try
                {
                    byte[] data = Convert.FromBase64String(levelDataPayload);
                    level = Level.Deserialize(data);
                    Console.WriteLine($"[SceneManager] Reconstructed Level from Level payload - Entities: {level.Entities.Count}, Skybox={(level.Skybox != null)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneManager] Level payload deserialize failed: {ex.Message}");
                    level = null;
                }
            }
            if (!string.IsNullOrEmpty(sceneDataPayload))
            {
                try
                {
                    byte[] data = Convert.FromBase64String(sceneDataPayload);
                    reconstructedSceneData = JsonSerializer.Deserialize<SceneData>(data, EntityData.SerializerOptions);
                    if (level == null)
                    {
                        level = new Level { Name = reconstructedSceneData?.Name ?? levelName };
                        if (reconstructedSceneData?.Entities != null)
                        {
                            foreach (var ed in reconstructedSceneData.Entities)
                                level.AddEntity(Entity.FromData(ed));
                        }
                    }
                    if (reconstructedSceneData != null)
                    {
                        if (level.Terrain == null) level.Terrain = reconstructedSceneData.Terrain;
                        if (level.Environment == null) level.Environment = reconstructedSceneData.Environment;
                        if (level.Skybox == null) level.Skybox = reconstructedSceneData.Skybox;
                        if (reconstructedSceneData.CustomData != null)
                        {
                            foreach (var kv in reconstructedSceneData.CustomData)
                                level.CustomData[kv.Key] = kv.Value;
                        }
                    }
                    Console.WriteLine($"[SceneManager] SceneData applied - Entities: {level.Entities.Count}, Skybox={(level.Skybox != null)}, Settings={(reconstructedSceneData?.Settings != null)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneManager] SceneData payload deserialize failed: {ex.Message}");
                }
            }
            level = level ?? new Level { Name = levelName };
            var ctx = SceneContext.CreateForRuntime(level, reconstructedSceneData ?? new SceneData { Name = levelName }, _renderContext, _controlContext, _window, new ClientGameServerProxy(_eventBus), _eventBus);
            ctx.PlayProjectPath = projectPath;
            ctx.LoadLevelName = levelName;
            ctx.CurrentLevel = level;
            if (reconstructedSceneData?.Terrain != null && reconstructedSceneData.Terrain.EmbeddedHeightmapData != null && reconstructedSceneData.Terrain.EmbeddedHeightmapWidth > 0 && reconstructedSceneData.Terrain.EmbeddedHeightmapHeight > 0)
            {
                int w = reconstructedSceneData.Terrain.EmbeddedHeightmapWidth;
                int h = reconstructedSceneData.Terrain.EmbeddedHeightmapHeight;
                float[] flat = reconstructedSceneData.Terrain.EmbeddedHeightmapData;
                if (flat != null && flat.Length >= w * h)
                {
                    var map = new float[w, h];
                    for (int x = 0; x < w; x++)
                        for (int y = 0; y < h; y++)
                            map[x, y] = flat[x * h + y];
                    ctx.HeightmapSnapshot = map;
                    Console.WriteLine($"[SceneManager] HeightmapSnapshot populated from embedded transfer data ({w}x{h})");
                }
            }
            _modelManager = new ModelManager(_renderContext);
            ctx.ModelManager = _modelManager;
            if (!string.IsNullOrEmpty(projectPath))
            {
                ModelManager.EnsurePacksLoaded(projectPath, level);
            }
            var predictionSystem = new ClientPredictionSystem(ctx.Server, _eventBus);
            ctx.Server.AddSystem(predictionSystem);
            ctx.Server.AddSystem(new AnimationSystem(ctx.Server));
            ctx.Server.AddSystem(new AudioSystem(ctx.Server, _eventBus, false, null, _renderContext));
            ctx.Player = null;
            ctx.PlayerMovement = null;
            // Activate first so [CustomSceneEntry] factories are registered before resolution.
            ScriptLoader.ActivateProjectScripts(ctx, _inputHandler, predictionSystem);
            if (ctx.PlayerMovement == null)
            {
                ctx.PlayerMovement = new PlayerMovement(_inputHandler, predictionSystem, _eventBus);
            }
            _playerMovement = ctx.PlayerMovement;
            _player = ctx.Player;
            // First-class resolution: pure-client [CustomSceneEntry] or classic RuntimeGameplay.
            string preferred = SceneRegistry.ResolvePreferredSceneName(levelName, reconstructedSceneData ?? ctx.SceneData);
            if (!SceneRegistry.IsRegistered(preferred))
                preferred = "RuntimeGameplay";
            Console.WriteLine($"[SceneManager] Resolved preferred scene '{preferred}' (level='{levelName}', CustomSceneClass='{reconstructedSceneData?.CustomSceneClass}')");
            _currentScene = (Scene)SceneRegistry.Create(preferred, ctx);
            _currentScene.Initialize(_settingsManager.WindowWidth, _settingsManager.WindowHeight);
            Console.WriteLine($"[SceneManager] '{preferred}' active with FULL editor snapshot - entities rehydrated and added");
        }
    }
}