// Folder: SiegeEngine/Scenes
// File: RuntimePlayStart.cs
using System;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;

namespace SiegeEngine.Scenes
{
    /// <summary>
    /// Single enter-runtime path. Play Game (SceneManager) and Play Host both call this.
    /// Systems, packs, scripts, movement, audio — one place.
    /// </summary>
    public static class RuntimePlayStart
    {
        public static SceneContext BuildContext(
            IRenderContext renderContext,
            IControlContext controlContext,
            nint window,
            EventBus eventBus,
            InputHandler inputHandler,
            string projectPath,
            string levelName,
            Level level,
            SceneData sceneData,
            float[,] heightmap,
            bool panelHosted)
        {
            level = level ?? new Level { Name = levelName ?? "Main" };
            sceneData = sceneData ?? new SceneData { Name = level.Name ?? levelName ?? "Main" };

            var server = new ClientGameServerProxy(eventBus);
            var ctx = SceneContext.CreateForRuntime(level, sceneData, renderContext, controlContext, window, server, eventBus);
            ctx.PlayProjectPath = projectPath;
            ctx.LoadLevelName = levelName ?? level.Name ?? "Main";
            ctx.CurrentLevel = level;
            ctx.HeightmapSnapshot = heightmap;
            ctx.IsPanelHosted = panelHosted;

            var modelManager = ModelManager.Instance ?? new ModelManager(renderContext);
            ctx.ModelManager = modelManager;
            if (!string.IsNullOrEmpty(projectPath))
                ModelManager.EnsurePacksLoaded(projectPath, level);

            var prediction = new ClientPredictionSystem(server, eventBus);
            server.AddSystem(prediction);
            server.AddSystem(new AnimationSystem(server));
            server.AddSystem(new AudioSystem(server, eventBus, isServer: false, validationSystem: null, renderContext: renderContext));

            ctx.Player = null;
            ctx.PlayerMovement = null;
            ScriptLoader.ActivateProjectScripts(ctx, inputHandler, prediction);
            if (ctx.PlayerMovement == null && inputHandler != null)
                ctx.PlayerMovement = new PlayerMovement(inputHandler, prediction, eventBus);

            Console.WriteLine("[RuntimePlayStart] Context ready hosted=" + panelHosted +
                " settings=" + (sceneData.Settings != null) +
                " entities=" + (level.Entities?.Count ?? 0));
            return ctx;
        }

        public static GameScene CreateScene(SceneContext ctx, string levelName)
        {
            string preferred = SceneRegistry.ResolvePreferredSceneName(levelName, ctx?.SceneData);
            if (!SceneRegistry.IsRegistered(preferred))
                preferred = "RuntimeGameplay";
            Console.WriteLine("[RuntimePlayStart] Scene '" + preferred + "'");
            return (GameScene)SceneRegistry.Create(preferred, ctx);
        }
    }
}
