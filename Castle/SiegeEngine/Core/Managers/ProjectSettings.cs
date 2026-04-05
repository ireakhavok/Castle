// Folder: SiegeEngine/Core/Managers
// File: ProjectSettings.cs
using System;
using System.IO;

namespace SiegeEngine.Core.Managers
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

        // NEW: Helper for per-project unsaved layout buffer (used in Step 5)
        public string GetLayoutTempPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return null;
            return Path.Combine(projectPath, ".layout.temp");
        }
    }
}