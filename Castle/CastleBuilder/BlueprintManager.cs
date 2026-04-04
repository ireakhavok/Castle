// Folder: CastleBuilder
// File: BlueprintManager.cs
using CastleBuilder.Events;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Globalization;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Core.Managers;

namespace CastleBuilder
{
    public class ProjectData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Mode { get; set; }
        public bool AllowMods { get; set; }
        public List<string> Scenes { get; set; } = new List<string> { "Main" };
        public string Version { get; set; } = "1.0";
        public string LastOpenedScene { get; set; } = "Main";
        public string CameraType { get; set; } = "Perspective";
        public string LastContext { get; set; } = "Scene Editor";
    }

    public class BlueprintManager
    {
        private readonly EventBus _eventBus;
        private readonly string _configPath;

        public BlueprintManager(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<NewProjectEvent>(OnNewProject);
            _eventBus.Subscribe<LoadProjectEvent>(OnLoadProject);
            _eventBus.Subscribe<SaveProjectEvent>(OnSaveProject);
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            _eventBus.Subscribe<ContextChangedEvent>(OnContextChanged);   // NEW - listens to blades
            _configPath = GetDefaultIDEPath();
        }

        public static void Load(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var idePanel = new IDEBasePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(idePanel) { Mode = OpenMode.Replace });
        }

        public static void CreateNewProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, UIOverlay overlay)
        {
            var nameElem = overlay.FindElementById("project-name") as InputElement;
            var typeElem = overlay.FindElementById("game-type") as SelectElement;
            var modeElem = overlay.FindElementById("project-mode") as SelectElement;
            var allowModsElem = overlay.FindElementById("allow-mods") as InputElement;

            string name = nameElem?.Value?.Trim() ?? "MyNewProject";
            if (string.IsNullOrEmpty(name)) name = "MyNewProject";

            string projectType = typeElem?.Value ?? "3D FPS";
            string mode = modeElem?.Value ?? "Single Player";
            bool allowMods = allowModsElem?.Checked ?? true;

            string root = ProjectSettings.Current.ProjectsRoot;
            string safeName = name.Replace(" ", "_").ReplaceInvalidFileChars();
            string dir = Path.Combine(root, safeName);
            Directory.CreateDirectory(dir);

            var data = new ProjectData
            {
                Name = name,
                Type = projectType,
                Mode = mode,
                AllowMods = allowMods,
                CameraType = projectType.Contains("2D") ? "AngledOrtho" : "Perspective",
                LastContext = "Scene Editor"
            };

            string jsonPath = Path.Combine(dir, "project.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

            Directory.CreateDirectory(Path.Combine(dir, "Scenes"));
            Directory.CreateDirectory(Path.Combine(dir, "Assets"));
            Directory.CreateDirectory(Path.Combine(dir, "Mods"));

            eventBus.Publish(new LoadProjectEvent { Path = dir });
            Console.WriteLine($"[BlueprintManager] New project created: {dir}");
            Load(renderContext, controlContext, window, eventBus);
        }

        public static void SaveCurrentProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new SaveProjectEvent());
        }

        public static void SaveProjectAs(string folder, string name, EventBus eventBus)
        {
            if (string.IsNullOrEmpty(folder))
                folder = ProjectSettings.Current.ProjectsRoot;

            string safeName = name.Replace(" ", "_").ReplaceInvalidFileChars();
            string dir = Path.Combine(folder, safeName);
            Directory.CreateDirectory(dir);

            var data = new ProjectData { Name = name, LastContext = "Scene Editor" };
            string jsonPath = Path.Combine(dir, "project.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

            eventBus.Publish(new LoadProjectEvent { Path = dir });
        }

        private void OnGenericEvent(GenericEvent evt)
        {
            if (evt.Hook == "CastleBuilder.NewProject")
            {
                _eventBus.Publish(new NewProjectEvent { Name = "MyProject", ProjectType = "3D_FPS" });
            }
            else if (evt.Hook == "CreateProject")
            {
                string name = evt.Data.GetValueOrDefault("name", "MyProject");
                string projectType = evt.Data.GetValueOrDefault("projectType", "3D_FPS");
                string mode = evt.Data.GetValueOrDefault("mode", "Single Player");
                bool allowMods = bool.Parse(evt.Data.GetValueOrDefault("allowMods", "false"));
                string path = evt.Data.GetValueOrDefault("path", null);
                _eventBus.Publish(new NewProjectEvent { Name = name, ProjectType = projectType, Mode = mode, AllowMods = allowMods, Path = path });
            }
        }

        private void OnNewProject(NewProjectEvent evt)
        {
            string root = ProjectSettings.Current.ProjectsRoot;
            string dir = evt.Path ?? Path.Combine(root, (evt.Name ?? "MyProject").Replace(" ", "_").ReplaceInvalidFileChars());
            Directory.CreateDirectory(dir);

            var data = new ProjectData
            {
                Name = evt.Name,
                Type = evt.ProjectType ?? "3D FPS",
                Mode = evt.Mode ?? "Single Player",
                AllowMods = evt.AllowMods,
                LastContext = "Scene Editor"
            };

            string jsonPath = Path.Combine(dir, "project.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

            Directory.CreateDirectory(Path.Combine(dir, "Scenes"));
            Directory.CreateDirectory(Path.Combine(dir, "Assets"));

            _eventBus.Publish(new LoadProjectEvent { Path = dir });
        }

        private void OnLoadProject(LoadProjectEvent evt)
        {
            if (string.IsNullOrEmpty(evt.Path) || !Directory.Exists(evt.Path)) return;
            ProjectSettings.Current.ActiveProject = evt.Path;

            string jsonPath = Path.Combine(evt.Path, "project.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                var data = JsonSerializer.Deserialize<ProjectData>(json);
                if (data != null)
                {
                    ProjectSettings.Current.CameraType = data.CameraType;
                    Console.WriteLine($"[BlueprintManager] Loaded project '{data.Name}' - Last Context: {data.LastContext}");
                }
            }
            SaveIDEState();
        }

        private void OnSaveProject(SaveProjectEvent evt)
        {
            if (string.IsNullOrEmpty(ProjectSettings.Current.ActiveProject)) return;

            string jsonPath = Path.Combine(ProjectSettings.Current.ActiveProject, "project.json");
            var data = new ProjectData
            {
                Name = Path.GetFileName(ProjectSettings.Current.ActiveProject),
                LastContext = "Scene Editor"
            };

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[BlueprintManager] Project saved: {ProjectSettings.Current.ActiveProject}");
        }

        // NEW - Step 2 handler
        private void OnContextChanged(ContextChangedEvent evt)
        {
            if (string.IsNullOrEmpty(ProjectSettings.Current.ActiveProject)) return;

            string jsonPath = Path.Combine(ProjectSettings.Current.ActiveProject, "project.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                var data = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();
                data.LastContext = evt.Context;
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }

            Console.WriteLine($"[BlueprintManager] Context saved to project.json → {evt.Context}");
        }

        private string GetTemplate(string type)
        {
            string templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            string templateFile = Path.Combine(templatesPath, $"{type}.json");
            if (File.Exists(templateFile))
                return File.ReadAllText(templateFile);

            return "{\"Name\": \"{name}\", \"Type\": \"" + type + "\", \"Mode\": \"{mode}\", \"AllowMods\": {allowMods}, \"CameraType\": \"" + (type == "2D" ? "AngledOrtho" : "Perspective") + "\", \"LastContext\": \"Scene Editor\"}";
        }

        private string GetDefaultIDEPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CastleBuilder", "config.json");
        }

        private void SaveIDEState()
        {
            if (string.IsNullOrEmpty(ProjectSettings.Current.ActiveProject)) return;
            var config = new Dictionary<string, string> { { "active_project", ProjectSettings.Current.ActiveProject } };
            string json = JsonSerializer.Serialize(config);
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
            File.WriteAllText(_configPath, json);
        }
    }

    public static class StringExtensions
    {
        public static string ReplaceInvalidFileChars(this string filename)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                filename = filename.Replace(c.ToString(), "_");
            return filename;
        }
    }
}