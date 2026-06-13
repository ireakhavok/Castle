// Folder: SiegeEngine/Scenes
// File: GameScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Numerics;

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
            LoadContentFromContext(null); // core hook call (noop safe)
        }

        // Future-proof protected virtual hooks (core only, no IDE impact, allows pure Runtime + reuse)
        protected virtual void LoadContentFromContext(SceneContext ctx)
        {
            // default noop - overridden in RuntimeGameplayScene for snapshot load
        }

        protected virtual void SetupPureRuntimeWorld()
        {
            // default noop - pure runtime implementation in RuntimeGameplayScene
        }

        protected virtual void RenderGameplayContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            // default empty - overridden for visible draw
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}