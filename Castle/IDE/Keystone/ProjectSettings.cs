// Folder: Keystone
// File: ProjectSettings.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Terrain;
using System;
using System.Collections.Generic;
using System.IO;

namespace Keystone
{
    public class ProjectSettings
    {
        public static ProjectSettings Current { get; } = new ProjectSettings();
        public string ActiveProject { get; set; }
        public string CameraType { get; set; } = "Perspective";
        private string _projectsRoot;
        public string ProjectsRoot
        {
            get
            {
                if (string.IsNullOrEmpty(_projectsRoot))
                {
                    _projectsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CastleBuilder", "Projects");
                    Directory.CreateDirectory(_projectsRoot);
                }
                return _projectsRoot;
            }
            set => _projectsRoot = value;
        }
        private readonly Dictionary<string, float[,]> _unsavedHeightmaps = new Dictionary<string, float[,]>();
        private readonly Dictionary<string, TerrainPaintData> _openPaintData = new Dictionary<string, TerrainPaintData>();

        // Step 2 fix: internal set so ProjectStateManager can assign them
        public SceneData CurrentSceneData { get; internal set; }
        public float[,] CurrentHeightmap { get; internal set; }
        public string CurrentSceneName { get; internal set; }
        public string CurrentHeightmapPath { get; private set; }
        public Level CurrentLevel { get; private set; }

        public void SetCurrentTerrain(SceneData sceneData, float[,] heightmap, string sceneName = null, string heightmapPath = null)
        {
            CurrentSceneData = sceneData;
            CurrentHeightmap = heightmap;
            if (!string.IsNullOrEmpty(sceneName)) CurrentSceneName = sceneName;
            if (!string.IsNullOrEmpty(heightmapPath)) CurrentHeightmapPath = heightmapPath;

            // One-way delegation
            ProjectStateManager.Current.SetCurrentTerrain(sceneData, heightmap, sceneName);

            if (sceneName != null && heightmap != null)
            {
                _unsavedHeightmaps[sceneName] = heightmap;
            }
            if (!string.IsNullOrEmpty(sceneName))
            {
                GetOrCreatePaintData(sceneName, heightmap?.GetLength(0) ?? 200, heightmap?.GetLength(1) ?? 200);
            }
            Console.WriteLine($"[ProjectSettings] SetCurrentTerrain - delegated to ProjectStateManager ({heightmap?.GetLength(0)}x{heightmap?.GetLength(1)}) for scene '{sceneName ?? "null"}'");
        }

        public void SetCurrentLevel(Level level)
        {
            CurrentLevel = level;
            Console.WriteLine($"[ProjectSettings] SetCurrentLevel - Level '{level?.Name ?? "null"}' is now the active persistent container");
        }

        public float[,] GetUnsavedHeightmap(string sceneName)
        {
            var fromLive = ProjectStateManager.Current.GetUnsavedHeightmap(sceneName);
            if (fromLive != null) return fromLive;
            return _unsavedHeightmaps.TryGetValue(sceneName, out var map) ? map : null;
        }

        public void StoreUnsavedHeightmap(string sceneName, float[,] heightmap)
        {
            if (sceneName != null && heightmap != null)
            {
                _unsavedHeightmaps[sceneName] = heightmap;
                ProjectStateManager.Current.StoreUnsavedHeightmap(sceneName, heightmap);
            }
        }

        public List<string> GetUnsavedHeightmapKeys() => new List<string>(_unsavedHeightmaps.Keys);

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
            _openPaintData.TryGetValue(sceneName, out var paintData);
            return paintData;
        }

        public string GetLayoutTempPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return null;
            return Path.Combine(projectPath, ".layout.temp");
        }
    }
}