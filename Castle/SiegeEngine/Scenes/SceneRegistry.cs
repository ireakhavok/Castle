// Folder: SiegeEngine.Scenes
// File: SceneRegistry.cs
using SiegeEngine.Scenes.StartingPoints;
using SiegeEngine.Core.Definitions;
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
            if (string.IsNullOrWhiteSpace(sceneName)) throw new ArgumentException("Scene name cannot be null or empty", nameof(sceneName));
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

        /// <summary>
        /// Single shared resolver used by both the Scene Editor (view-only host)
        /// and Play / Export (full interactive). Never hard-codes project type names
        /// and contains no name-mangling heuristics.
        /// Order:
        /// 1. SceneData.CustomSceneClass (explicit)
        /// 2. SceneData.CustomData["customSceneClass"] / ["implementingType"] (explicit)
        /// 3. Exact sceneName if registered via [CustomSceneEntry]
        /// 4. Exactly one non-core custom factory registered by the project
        /// 5. Fallback "RuntimeGameplay" (classic terrain / entity path)
        /// </summary>
        public static string ResolvePreferredSceneName(string sceneName, SceneData sd = null)
        {
            // 1. Explicit first-class declaration on SceneData
            if (!string.IsNullOrWhiteSpace(sd?.CustomSceneClass))
            {
                string name = sd.CustomSceneClass.Trim();
                if (IsRegistered(name)) return name;
            }

            // 2. customData bag (zero-schema-churn path)
            if (sd?.CustomData != null)
            {
                if (sd.CustomData.TryGetValue("customSceneClass", out var v) && v != null)
                {
                    string name = v.ToString().Trim();
                    if (!string.IsNullOrEmpty(name) && IsRegistered(name)) return name;
                }
                if (sd.CustomData.TryGetValue("implementingType", out var v2) && v2 != null)
                {
                    string name = v2.ToString().Trim();
                    if (!string.IsNullOrEmpty(name) && IsRegistered(name)) return name;
                }
            }

            // 3. Exact scene name itself registered via [CustomSceneEntry]
            if (!string.IsNullOrEmpty(sceneName) && IsRegistered(sceneName))
                return sceneName;

            // 4. Exactly one non-core custom factory registered by this project
            var customs = GetCustomRegisteredNames();
            if (customs.Count == 1)
                return customs[0];

            if (customs.Count > 1)
            {
                Console.WriteLine($"[SceneRegistry] Multiple custom scenes registered ({string.Join(", ", customs)}); set CustomSceneClass on the scene to choose one.");
            }

            // 5. Classic terrain / entity fallback
            return "RuntimeGameplay";
        }

        static SceneRegistry()
        {
            // Existing registrations
            Register("Sandbox", ctx => new SandboxScene(ctx.RenderContext, ctx.ControlContext, ctx.Window, ctx.Player, ctx.Server, ctx.PlayerMovement, ctx.EventBus, ctx.ModelManager));

            // Runtime gameplay scene for classic Play/Export (receives full rich ctx from SceneManager with reconstructed Level)
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