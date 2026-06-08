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
        private SceneData _currentSceneData;
        private float[,] _currentHeightmap;
        private string _currentSceneName;
        private Level _currentLevel;
        private readonly Dictionary<string, TerrainPaintData> _openPaintData = new Dictionary<string, TerrainPaintData>();
        private readonly Dictionary<string, float[,]> _unsavedHeightmaps = new Dictionary<string, float[,]>();

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
            if (string.IsNullOrEmpty(sceneName) || heightmap == null) return;

            _unsavedHeightmaps[sceneName] = heightmap;
            var state = GetLiveState(sceneName);
            if (state != null)
            {
                state.Heightmap = heightmap;
                state.HeightmapVersion++;
            }
        }

        public float[,] GetUnsavedHeightmap(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            if (_unsavedHeightmaps.TryGetValue(sceneName, out var map)) return map;

            var state = GetLiveState(sceneName);
            return state?.Heightmap;
        }

        public List<string> GetUnsavedHeightmapKeys()
        {
            return new List<string>(_unsavedHeightmaps.Keys);
        }

        public TerrainPaintData GetOrCreatePaintData(string sceneName, int width = 200, int height = 200)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            if (!_openPaintData.TryGetValue(sceneName, out var paintData))
            {
                paintData = new TerrainPaintData(sceneName, width, height);
                _openPaintData[sceneName] = paintData;
            }
            return paintData;
        }

        public TerrainPaintData GetPaintData(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            _openPaintData.TryGetValue(sceneName, out var paintData);
            return paintData;
        }

        public void SetCurrentTerrain(SceneData sceneData, float[,] heightmap, string sceneName = null)
        {
            if (sceneName == null && sceneData != null) sceneName = sceneData.Name;

            _currentSceneData = sceneData;
            _currentHeightmap = heightmap;
            if (!string.IsNullOrEmpty(sceneName)) _currentSceneName = sceneName;

            var state = GetOrCreateLiveState(sceneName);
            if (heightmap != null)
            {
                state.Heightmap = heightmap;
            }

            Console.WriteLine($"[ProjectStateManager] SetCurrentTerrain - live state updated for '{sceneName}'");
        }

        public SceneData CurrentSceneData => _currentSceneData;
        public float[,] CurrentHeightmap => _currentHeightmap;
        public string CurrentSceneName => _currentSceneName;
        public Level CurrentLevel
        {
            get => _currentLevel;
            set
            {
                _currentLevel = value;
                Console.WriteLine($"[ProjectStateManager] CurrentLevel set to '{value?.Name ?? "null"}'");
            }
        }

        public void SetCurrentLevel(Level level)
        {
            CurrentLevel = level;
        }

        public void Clear()
        {
            foreach (var state in _liveStates.Values) state.Dispose();
            _liveStates.Clear();
            _openPaintData.Clear();
            _unsavedHeightmaps.Clear();
            _currentSceneData = null;
            _currentHeightmap = null;
            _currentSceneName = null;
            _currentLevel = null;
        }
    }
}