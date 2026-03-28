// Folder: SiegeEngine/Core/Managers
// File: ProjectSettings.cs
namespace SiegeEngine.Core.Managers
{
    public class ProjectSettings
    {
        public static ProjectSettings Current { get; } = new ProjectSettings();

        public string ActiveProject { get; set; }
        public string CameraType { get; set; } = "Perspective";
    }
}