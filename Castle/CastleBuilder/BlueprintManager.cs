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
    }
    public class BlueprintManager
    {
        private readonly EventBus _eventBus;
        private string _activeProject = null;
        private readonly string _configPath;
        public BlueprintManager(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<NewProjectEvent>(OnNewProject);
            _eventBus.Subscribe<LoadProjectEvent>(OnLoadProject);
            _eventBus.Subscribe<SaveProjectEvent>(OnSaveProject);
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            _configPath = GetDefaultIDEPath();
        }
        public static void Load(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var idePanel = new IDEBasePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(idePanel) { Mode = OpenMode.Replace });
        }
        // Called directly from NewProject.html "Create" button via MenuPanel reflection
        // Now reads REAL form fields from the UIOverlay passed by the updated MenuUIOverlay
        public static void CreateNewProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, UIOverlay overlay)
        {
            var nameElem = overlay.FindElementById("project-name") as InputElement;
            var typeElem = overlay.FindElementById("game-type") as SelectElement;
            var modeElem = overlay.FindElementById("project-mode") as SelectElement;
            var allowModsElem = overlay.FindElementById("allow-mods") as InputElement;

            string name = nameElem?.Value?.Trim() ?? "MyNewProject";
            string projectType = typeElem?.Value ?? "3D FPS";
            string mode = modeElem?.Value ?? "Single Player";
            bool allowMods = allowModsElem?.Checked ?? true;

            if (string.IsNullOrEmpty(name)) name = "MyNewProject";

            string path = @"C:\Users\ireak\source\CastleBuilder\Projects";
            string dir = Path.Combine(path, name.Replace(" ", ""));

            Directory.CreateDirectory(dir);
            var data = new ProjectData
            {
                Name = name,
                Type = projectType,
                Mode = mode,
                AllowMods = allowMods
            };
            string jsonPath = Path.Combine(dir, "project.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            Directory.CreateDirectory(Path.Combine(dir, "Scenes"));
            Directory.CreateDirectory(Path.Combine(dir, "Assets"));
            Directory.CreateDirectory(Path.Combine(dir, "Mods"));
            eventBus.Publish(new LoadProjectEvent { Path = dir });
            Console.WriteLine($"[BlueprintManager] New project created with REAL form data and IDE opened: {dir}");
            Load(renderContext, controlContext, window, eventBus);
        }
        public static void SaveCurrentProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new SaveProjectEvent());
        }
        public static void SaveProjectAs(string folder, string name, EventBus eventBus)
        {
            string dir = Path.Combine(folder, name);
            Directory.CreateDirectory(dir);
            var data = new ProjectData { Name = name };
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
            string mappedType = evt.ProjectType.Replace(" ", "_");
            string dir = evt.Path ?? Path.Combine("Projects", evt.Name);
            Directory.CreateDirectory(dir);
            string template = GetTemplate(mappedType);
            if (string.IsNullOrEmpty(template))
            {
                Console.WriteLine($"BlueprintManager: Template not found for type {mappedType}");
                return;
            }
            string projectJson = template.Replace("{name}", evt.Name).Replace("{mode}", evt.Mode ?? "Single Player").Replace("{allowMods}", evt.AllowMods.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
            File.WriteAllText(Path.Combine(dir, "project.json"), projectJson);
            Directory.CreateDirectory(Path.Combine(dir, "Scenes"));
            Directory.CreateDirectory(Path.Combine(dir, "Assets"));
            _eventBus.Publish(new LoadProjectEvent { Path = dir });
        }
        private void OnLoadProject(LoadProjectEvent evt)
        {
            if (string.IsNullOrEmpty(evt.Path) || !Directory.Exists(evt.Path)) return;
            _activeProject = evt.Path;
            SaveIDEState();
        }
        private void OnSaveProject(SaveProjectEvent evt)
        {
            if (string.IsNullOrEmpty(_activeProject)) return;
            string jsonPath = Path.Combine(_activeProject, "project.json");
            var data = new ProjectData { Name = Path.GetFileName(_activeProject) };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        private string GetTemplate(string type)
        {
            string templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            string templateFile = Path.Combine(templatesPath, $"{type}.json");
            if (File.Exists(templateFile))
            {
                return File.ReadAllText(templateFile);
            }
            return null;
        }
        private string GetDefaultIDEPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CastleBuilder", "config.json");
        }
        private void SaveIDEState()
        {
            if (string.IsNullOrEmpty(_activeProject)) return;
            var config = new Dictionary<string, string> { { "active_project", _activeProject } };
            string json = JsonSerializer.Serialize(config);
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
            File.WriteAllText(_configPath, json);
        }
    }
}