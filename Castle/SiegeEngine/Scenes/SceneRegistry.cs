// Folder: SiegeEngine.Scenes
// File: SceneRegistry.cs
using SiegeEngine.Scenes.StartingPoints;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SiegeEngine.Scenes
{
    public static class SceneRegistry
    {
        private static readonly Dictionary<string, Func<SceneContext, IScene>> _factories = new(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> CoreNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Sandbox",
            "RuntimeGameplay"
        };

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

        /// <summary>
        /// All currently registered scene names (core + project).
        /// </summary>
        public static IReadOnlyList<string> GetRegisteredNames() => _factories.Keys.ToList();

        /// <summary>
        /// Registered names that are not core engine factories (Sandbox / RuntimeGameplay).
        /// Used by the editor for dynamic pure-client hosted preview resolution.
        /// </summary>
        public static IReadOnlyList<string> GetCustomRegisteredNames() =>
            _factories.Keys.Where(k => !CoreNames.Contains(k)).ToList();

        static SceneRegistry()
        {
            // Existing registrations
            Register("Sandbox", ctx => new SandboxScene(ctx.RenderContext, ctx.ControlContext, ctx.Window, ctx.Player, ctx.Server, ctx.PlayerMovement, ctx.EventBus, ctx.ModelManager));
            // Runtime gameplay scene for Play/Export (receives full rich ctx from SceneManager with reconstructed Level)
            Register("RuntimeGameplay", ctx =>
            {
                var scene = new RuntimeGameplayScene(ctx.RenderContext, ctx.ControlContext, ctx.Window, ctx.Server, ctx.EventBus, ctx);
                if (ctx.CurrentLevel != null && ctx.CurrentLevel.Entities.Count > 0)
                {
                    Console.WriteLine($"[SceneRegistry] RuntimeGameplay factory received rich ctx with {ctx.CurrentLevel.Entities.Count} entities - passing intact");
                }
                scene.LoadLevelData(ctx.LoadLevelName, ctx.PlayProjectPath);
                return scene;
            });
        }
    }
}