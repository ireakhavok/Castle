using SiegeEngine.Core.Definitions;

public class ProjectData
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Mode { get; set; }
    public bool AllowMods { get; set; }
    public Dictionary<string, SceneData> Scenes { get; set; } = new Dictionary<string, SceneData>();
    public string Version { get; set; } = "1.0";
    public string LastOpenedScene { get; set; } = string.Empty;
    public string CameraType { get; set; } = "Perspective";
    public string LastContext { get; set; } = "Scene Editor";
}