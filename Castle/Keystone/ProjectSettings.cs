// Folder: Keystone
// File: ProjectSettings.cs
using SiegeEngine.Core.Definitions;
using System;
using System.IO;

namespace Keystone
{
    public class ProjectSettings
    {
        public static ProjectSettings Current { get; } = new ProjectSettings();

        public string ActiveProject { get; set; }
        public string CameraType { get; set; } = "Perspective";

        // NEW: Centralized, user-visible project root (eliminates all hard-coded paths)
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

        // NEW: Centralized, persistent terrain memory backend (survives panel close, blade switches, and SceneEditorPanel disposal)
        // The live heightmap array is always the exact same reference used by TerrainScene/TerrainCreatorScene.
        public SceneData CurrentSceneData { get; private set; }
        public float[,] CurrentHeightmap { get; private set; }
        public string CurrentSceneName { get; private set; }
        public string CurrentHeightmapPath { get; private set; }

        // NEW: Single hand-off point for terrain data (called immediately after TerrainManager load or CreateTerrain/CreateBlank)
        // Guarantees the heightmap array remains shared across every panel and scene.
        public void SetCurrentTerrain(SceneData sceneData, float[,] heightmap, string sceneName = null, string heightmapPath = null)
        {
            CurrentSceneData = sceneData;
            CurrentHeightmap = heightmap;
            if (!string.IsNullOrEmpty(sceneName)) CurrentSceneName = sceneName;
            if (!string.IsNullOrEmpty(heightmapPath)) CurrentHeightmapPath = heightmapPath;

            Console.WriteLine($"[ProjectSettings] SetCurrentTerrain - shared heightmap reference set ({heightmap?.GetLength(0)}x{heightmap?.GetLength(1)}) for scene '{sceneName ?? "null"}'");
        }

        // NEW: Helper for per-project unsaved layout buffer (used in Step 5)
        public string GetLayoutTempPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return null;
            return Path.Combine(projectPath, ".layout.temp");
        }
    }
}