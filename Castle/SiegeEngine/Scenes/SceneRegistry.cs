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
    }
}