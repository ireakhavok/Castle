// Folder: SiegeEngine/Scenes
// File: GameScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using System;

namespace SiegeEngine.Scenes
{
    public abstract class GameScene : Scene
    {
        public SceneData SceneData { get; protected set; }
        public string SceneName { get; protected set; }

        protected GameScene(IRenderContext renderContext, IControlContext controlContext, nint window,
                           IGameServer server, EventBus eventBus, SceneData sceneData = null)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            SceneData = sceneData ?? new SceneData { Name = "Untitled", SceneType = "Gameplay" };
            SceneName = SceneData.Name ?? "Untitled";
        }

        public virtual void LoadSceneData(SceneData data)
        {
            SceneData = data ?? new SceneData { Name = "Untitled", SceneType = "Gameplay" };
            SceneName = SceneData.Name ?? "Untitled";
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}