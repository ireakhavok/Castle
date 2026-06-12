// Folder: SiegeEngine.Scenes
// File: SceneRegistry.cs
using SiegeEngine.Scenes.StartingPoints;
using System;
using System.Collections.Generic;

namespace SiegeEngine.Scenes
{
    public static class SceneRegistry
    {
        private static readonly Dictionary<string, Func<SceneContext, IScene>> _factories = new(StringComparer.OrdinalIgnoreCase);

        public static void Register(string sceneName, Func<SceneContext, IScene> factory)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("Scene name cannot be null or empty", nameof(sceneName));
            _factories[sceneName] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static IScene Create(string sceneName, SceneContext context)
        {
            if (!_factories.TryGetValue(sceneName, out var factory))
                throw new InvalidOperationException($"Scene '{sceneName}' is not registered.");
            return factory(context);
        }

        public static bool IsRegistered(string sceneName) => _factories.ContainsKey(sceneName);

        static SceneRegistry()
        {
            // Existing registrations
            Register("Sandbox", ctx => new SandboxScene(ctx.RenderContext, ctx.ControlContext, ctx.Window, ctx.Player, ctx.Server, ctx.PlayerMovement, ctx.EventBus, ctx.ModelManager));
            // NEW runtime gameplay scene for Play/Export (receives passed params via SceneContext)
            Register("RuntimeGameplay", ctx =>
            {
                var scene = new RuntimeGameplayScene(ctx.RenderContext, ctx.ControlContext, ctx.Window, ctx.Server, ctx.EventBus);
                scene.LoadLevelData(ctx.LoadLevelName, ctx.PlayProjectPath); // passed variables, no ProjectSettings reference
                return scene;
            });
        }
    }
}