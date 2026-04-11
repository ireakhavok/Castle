// Folder: Keystone
// File: ProjectSettings.cs
using SiegeEngine.Core.Definitions;
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
                    _projectsRoot = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "CastleBuilder",
                        "Projects");
                    Directory.CreateDirectory(_projectsRoot);
                }
                return _projectsRoot;
            }
            set => _projectsRoot = value;
        }

        // Per-scene unsaved heightmap cache - this is the key missing piece
        private readonly Dictionary<string, float[,]> _unsavedHeightmaps = new Dictionary<string, float[,]>();

        public SceneData CurrentSceneData { get; private set; }
        public float[,] CurrentHeightmap { get; private set; }
        public string CurrentSceneName { get; private set; }
        public string CurrentHeightmapPath { get; private set; }

        public void SetCurrentTerrain(SceneData sceneData, float[,] heightmap, string sceneName = null, string heightmapPath = null)
        {
            CurrentSceneData = sceneData;
            CurrentHeightmap = heightmap;
            if (!string.IsNullOrEmpty(sceneName)) CurrentSceneName = sceneName;
            if (!string.IsNullOrEmpty(heightmapPath)) CurrentHeightmapPath = heightmapPath;

            // Store in per-scene cache for switching
            if (sceneName != null && heightmap != null)
            {
                _unsavedHeightmaps[sceneName] = heightmap;
            }

            Console.WriteLine($"[ProjectSettings] SetCurrentTerrain - shared heightmap reference set ({heightmap?.GetLength(0)}x{heightmap?.GetLength(1)}) for scene '{sceneName ?? "null"}'");
        }

        // NEW: Get unsaved heightmap for any scene (used when switching)
        public float[,] GetUnsavedHeightmap(string sceneName)
        {
            return _unsavedHeightmaps.TryGetValue(sceneName, out var map) ? map : null;
        }

        // NEW: Store unsaved heightmap when modified (called from TerrainCreatorScene)
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