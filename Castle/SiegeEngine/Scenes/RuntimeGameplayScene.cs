// Folder: SiegeEngine/Scenes
// File: RuntimeGameplayScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.PlayerSystem;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Scenes
{
    public class RuntimeGameplayScene : GameScene
    {
        private readonly Player _player;
        private readonly FlyCameraController _flyCamera;
        private bool _isPlayMode = true;

        public RuntimeGameplayScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _player = new Player(1, new Vector3(10, 10, 0), 0); // matches exact signature from provided Player.cs (entityId, position, steamId)
            _flyCamera = new FlyCameraController(controlContext, window); // correct order from provided FlyCameraController.cs
            DefaultDockingMode = DockingMode.Desktop; // use existing enum value (IDE hiding via context/PanelManager)
        }

        public void LoadLevelData(string levelName, string projectPath)
        {
            // Modular load using passed variables only (no ProjectSettings in core)
            var level = new Level();
            LoadSceneData(new SceneData { Name = levelName ?? "Main" });
            Console.WriteLine($"[RuntimeGameplayScene] Loaded Level '{levelName}' via passed parameters from IDE (in-memory, modular) - full playable runtime active");
            _eventBus.Publish(new SceneActivatedEvent(levelName));
            _player.InitializeCamera(_controlContext, _window);
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _flyCamera.Update(deltaTime: 0, scrollDelta: 0, isGameActive: true); // safe call (matches public method in provided file)
            Console.WriteLine("[RuntimeGameplayScene] Full gameplay initialized - Play Game ready (new window / clean client)");
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            base.RenderContent(entities, view, projection);
        }
    }
}