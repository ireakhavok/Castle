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
        private readonly Dictionary<string, SceneSettings> _sceneSettings = new Dictionary<string, SceneSettings>();
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
            // Seed Settings from SceneData ONLY when the live buffer has no entry yet.
            // Never overwrite live authoring state with stale project.json Settings
            // (FlushActiveSceneData passes the disk-loaded SceneData which often has empty Settings).
            if (sceneData?.Settings != null && !string.IsNullOrEmpty(sceneName)
                && (!_sceneSettings.TryGetValue(sceneName, out var existing) || existing == null))
            {
                _sceneSettings[sceneName] = sceneData.Settings;
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
                if (value != null && !string.IsNullOrEmpty(value.Name))
                {
                    _currentSceneName = value.Name;
                }
                Console.WriteLine($"[ProjectStateManager] CurrentLevel set to '{value?.Name ?? "null"}'");
            }
        }
        public void SetCurrentLevel(Level level)
        {
            CurrentLevel = level;
        }
        public SceneSettings CurrentSceneSettings
        {
            get
            {
                if (string.IsNullOrEmpty(_currentSceneName)) return null;
                _sceneSettings.TryGetValue(_currentSceneName, out var settings);
                return settings;
            }
            set
            {
                if (string.IsNullOrEmpty(_currentSceneName)) return;
                _sceneSettings[_currentSceneName] = value;
            }
        }
        public void SetCurrentSceneSettings(SceneSettings settings)
        {
            CurrentSceneSettings = settings;
        }
        public SceneSettings GetOrCreateSceneSettings(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            if (!_sceneSettings.TryGetValue(sceneName, out var settings) || settings == null)
            {
                settings = new SceneSettings();
                _sceneSettings[sceneName] = settings;
            }
            return settings;
        }
        public SceneSettings GetSceneSettings(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            _sceneSettings.TryGetValue(sceneName, out var settings);
            return settings;
        }
        public void SetSceneSettings(string sceneName, SceneSettings settings)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            _sceneSettings[sceneName] = settings;
        }
        public void Clear()
        {
            foreach (var state in _liveStates.Values)
                state.Dispose();
            _liveStates.Clear();
            _openPaintData.Clear();
            _unsavedHeightmaps.Clear();
            _sceneSettings.Clear();
            _currentSceneData = null;
            _currentHeightmap = null;
            _currentSceneName = null;
            _currentLevel = null;
        }
    }
}