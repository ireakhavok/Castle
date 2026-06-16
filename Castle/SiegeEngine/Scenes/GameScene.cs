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
        protected bool _baseLoadCalled = false;

        protected GameScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus, SceneData sceneData = null) : base(renderContext, controlContext, window, server, eventBus)
        {
            SceneData = sceneData ?? new SceneData { Name = "Untitled", SceneType = "Gameplay" };
            SceneName = SceneData.Name ?? "Untitled";
        }

        public virtual void LoadSceneData(SceneData data)
        {
            if (_baseLoadCalled) return;
            _baseLoadCalled = true;
            SceneData = data ?? new SceneData { Name = "Untitled", SceneType = "Gameplay" };
            SceneName = SceneData.Name ?? "Untitled";
            LoadContentFromContext(null);
        }

        protected virtual void LoadContentFromContext(SceneContext ctx)
        {
        }

        protected virtual void SetupPureRuntimeWorld()
        {
        }

        protected virtual void RenderGameplayContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            RenderGameplayContent(entities, view, projection); // bridge to connect the override
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}