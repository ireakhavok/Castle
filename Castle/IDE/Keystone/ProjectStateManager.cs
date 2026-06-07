// Folder: Keystone
// File: ProjectStateManager.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Terrain;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;

namespace Keystone
{
    public class ProjectStateManager
    {
        public static ProjectStateManager Current { get; } = new ProjectStateManager();

        private readonly Dictionary<string, LiveSceneState> _liveStates = new Dictionary<string, LiveSceneState>();

        public LiveSceneState GetOrCreateLiveState(string sceneName, int width = 200, int height = 200)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;

            if (!_liveStates.TryGetValue(sceneName, out var state))
            {
                state = new LiveSceneState(sceneName, width, height);
                _liveStates[sceneName] = state;
                Console.WriteLine($"[ProjectStateManager] Created LiveSceneState for '{sceneName}' ({width}x{height})");
            }
            return state;
        }

        public LiveSceneState GetLiveState(string sceneName)
        {
            _liveStates.TryGetValue(sceneName, out var state);
            return state;
        }

        public void BindSceneToLiveState(string sceneName, GameScene scene)
        {
            if (scene is TerrainScene terrainScene)
            {
                var state = GetOrCreateLiveState(sceneName);
                terrainScene.BindLiveState(state);
                Console.WriteLine($"[ProjectStateManager] Bound live state to TerrainScene '{sceneName}'");
            }
        }

        public void StoreUnsavedHeightmap(string sceneName, float[,] heightmap)
        {
            var state = GetLiveState(sceneName);
            if (state != null && heightmap != null)
            {
                state.Heightmap = heightmap;
                state.HeightmapVersion++;
            }
        }

        public float[,] GetUnsavedHeightmap(string sceneName)
        {
            var state = GetLiveState(sceneName);
            return state?.Heightmap;
        }

        public TerrainPaintData GetOrCreatePaintData(string sceneName, int width = 200, int height = 200)
        {
            return ProjectSettings.Current.GetOrCreatePaintData(sceneName, width, height);
        }

        public void SetCurrentTerrain(SceneData sceneData, float[,] heightmap, string sceneName = null)
        {
            if (sceneName == null && sceneData != null) sceneName = sceneData.Name;

            var state = GetOrCreateLiveState(sceneName);
            if (heightmap != null)
            {
                state.Heightmap = heightmap;
            }

            // Direct assignment (now allowed by internal setters in ProjectSettings)
            ProjectSettings.Current.CurrentSceneData = sceneData;
            ProjectSettings.Current.CurrentHeightmap = heightmap;
            if (!string.IsNullOrEmpty(sceneName)) ProjectSettings.Current.CurrentSceneName = sceneName;

            Console.WriteLine($"[ProjectStateManager] SetCurrentTerrain - live state updated for '{sceneName}'");
        }

        public void Clear()
        {
            foreach (var state in _liveStates.Values) state.Dispose();
            _liveStates.Clear();
        }
    }
}