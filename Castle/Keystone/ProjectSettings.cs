using SiegeEngine.Core.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
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
        public SceneData CurrentSceneData { get; private set; }
        public float[,] CurrentHeightmap { get; private set; }
        public string CurrentSceneName { get; private set; }
        public string CurrentHeightmapPath { get; private set; }
        public Level CurrentLevel { get; private set; }
        public void SetCurrentTerrain(SceneData sceneData, float[,] heightmap, string sceneName = null, string heightmapPath = null)
        {
            CurrentSceneData = sceneData;
            CurrentHeightmap = heightmap;
            if (!string.IsNullOrEmpty(sceneName)) CurrentSceneName = sceneName;
            if (!string.IsNullOrEmpty(heightmapPath)) CurrentHeightmapPath = heightmapPath;
            if (sceneName != null && heightmap != null)
            {
                _unsavedHeightmaps[sceneName] = heightmap;
            }
            Console.WriteLine($"[ProjectSettings] SetCurrentTerrain - shared heightmap reference set ({heightmap?.GetLength(0)}x{heightmap?.GetLength(1)}) for scene '{sceneName ?? "null"}'");
        }
        public void SetCurrentLevel(Level level)
        {
            CurrentLevel = level;
            Console.WriteLine($"[ProjectSettings] SetCurrentLevel - Level '{level?.Name ?? "null"}' is now the active persistent container");
        }
        public float[,] GetUnsavedHeightmap(string sceneName)
        {
            return _unsavedHeightmaps.TryGetValue(sceneName, out var map) ? map : null;
        }
        public void StoreUnsavedHeightmap(string sceneName, float[,] heightmap)
        {
            if (sceneName != null && heightmap != null)
            {
                _unsavedHeightmaps[sceneName] = heightmap;
            }
        }
        public string GetLayoutTempPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return null;
            return Path.Combine(projectPath, ".layout.temp");
        }
    }
}