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
    /// <summary>
    /// GameScene is the data-driven runtime scene used by the editor and games.
    /// It inherits from the pure engine Scene but always carries a live SceneData reference.
    /// This cleanly separates "engine scene" from "game scene" as requested.
    /// Subclasses (TerrainGameScene, etc.) will be added in later steps.
    /// </summary>
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

        /// <summary>
        /// Reload SceneData into this runtime scene (called on project load or scene switch).
        /// Concrete subclasses override to populate terrain/entities/etc.
        /// </summary>
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