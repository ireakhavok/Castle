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

        public SceneData CurrentSceneData => ProjectStateManager.Current.CurrentSceneData;
        public float[,] CurrentHeightmap => ProjectStateManager.Current.CurrentHeightmap;
        public string CurrentSceneName => ProjectStateManager.Current.CurrentSceneName;
        public Level CurrentLevel => ProjectStateManager.Current.CurrentLevel;
        public string CurrentHeightmapPath { get; private set; }

        public void SetCurrentTerrain(SceneData sceneData, float[,] heightmap, string sceneName = null, string heightmapPath = null)
        {
            if (!string.IsNullOrEmpty(heightmapPath)) CurrentHeightmapPath = heightmapPath;

            ProjectStateManager.Current.SetCurrentTerrain(sceneData, heightmap, sceneName);

            if (sceneName != null && heightmap != null)
            {
                ProjectStateManager.Current.StoreUnsavedHeightmap(sceneName, heightmap);
            }
            if (!string.IsNullOrEmpty(sceneName))
            {
                ProjectStateManager.Current.GetOrCreatePaintData(sceneName, heightmap?.GetLength(0) ?? 200, heightmap?.GetLength(1) ?? 200);
            }
            Console.WriteLine($"[ProjectSettings] SetCurrentTerrain - delegated to ProjectStateManager");
        }

        public void SetCurrentLevel(Level level)
        {
            ProjectStateManager.Current.SetCurrentLevel(level);
        }

        public float[,] GetUnsavedHeightmap(string sceneName)
        {
            return ProjectStateManager.Current.GetUnsavedHeightmap(sceneName);
        }

        public void StoreUnsavedHeightmap(string sceneName, float[,] heightmap)
        {
            ProjectStateManager.Current.StoreUnsavedHeightmap(sceneName, heightmap);
        }

        public List<string> GetUnsavedHeightmapKeys()
        {
            return ProjectStateManager.Current.GetUnsavedHeightmapKeys();
        }

        public TerrainPaintData GetOrCreatePaintData(string sceneName, int width = 200, int height = 200)
        {
            return ProjectStateManager.Current.GetOrCreatePaintData(sceneName, width, height);
        }

        public TerrainPaintData GetPaintData(string sceneName)
        {
            return ProjectStateManager.Current.GetPaintData(sceneName);
        }

        public string GetLayoutTempPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return null;
            return Path.Combine(projectPath, ".layout.temp");
        }
    }
}