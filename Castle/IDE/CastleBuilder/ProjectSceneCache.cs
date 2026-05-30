// Folder: CastleBuilder
// File: ProjectSceneCache.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;

namespace CastleBuilder
{
    /// <summary>
    /// IDE-only in-memory cache for project-level scenes (NewTerrain, NewTerrain123, etc.).
    /// Completely separate from the core engine's SceneManager / SceneRegistry.
    /// </summary>
    public class ProjectSceneCache
    {
        private readonly Dictionary<string, (GameScene scene, Level level)> _cache
            = new Dictionary<string, (GameScene, Level)>();

        public bool TryGet(string sceneName, out GameScene scene, out Level level)
        {
            if (_cache.TryGetValue(sceneName, out var entry))
            {
                scene = entry.scene;
                level = entry.level;
                return true;
            }
            scene = null;
            level = null;
            return false;
        }

        public void Store(string sceneName, GameScene scene, Level level)
        {
            if (_cache.TryGetValue(sceneName, out var old) && old.scene != scene)
                old.scene?.Dispose();

            _cache[sceneName] = (scene, level);
        }

        public void Clear()
        {
            foreach (var entry in _cache.Values)
                entry.scene?.Dispose();
            _cache.Clear();
        }

        public void Remove(string sceneName)
        {
            if (_cache.TryGetValue(sceneName, out var entry))
                entry.scene?.Dispose();
            _cache.Remove(sceneName);
        }
    }
}