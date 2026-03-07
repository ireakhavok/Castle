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
        public string Type { get; set; } = "3D_FPS";
        public string Mode { get; set; } = "SinglePlayer";
        public bool AllowMods { get; set; } = true;
        public List<string> Scenes { get; set; } = new List<string> { "Main" };
        public string Version { get; set; } = "1.0";
        public string LastOpenedScene { get; set; } = "Main";
    }

    public class BlueprintManager
    {
        private readonly EventBus _eventBus;
        private string _activeProject = null; // null = blank/temp mode in bin
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
            Console.WriteLine("[BlueprintManager] IDE opened blank (no project loaded)");
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
            Console.WriteLine($"[BlueprintManager] New project created and saved: {dir}");
        }

        private void OnLoadProject(LoadProjectEvent evt)
        {
            if (string.IsNullOrEmpty(evt.Path) || !Directory.Exists(evt.Path)) return;

            _activeProject = evt.Path;

            string jsonPath = Path.Combine(_activeProject, "project.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                var data = JsonSerializer.Deserialize<ProjectData>(json);
                Console.WriteLine($"[BlueprintManager] Loaded project '{data.Name}' from {_activeProject}");
            }

            SaveIDEState();
        }

        private void OnSaveProject(SaveProjectEvent evt)
        {
            if (string.IsNullOrEmpty(_activeProject))
            {
                Console.WriteLine("[BlueprintManager] No active project - use Save As first.");
                return;
            }

            string jsonPath = Path.Combine(_activeProject, "project.json");
            var data = new ProjectData { Name = Path.GetFileName(_activeProject) };

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[BlueprintManager] Project saved: {jsonPath}");
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
            string dir = evt.Path ?? Path.Combine("Projects", evt.Name);
            Directory.CreateDirectory(dir);
            var data = new ProjectData { Name = evt.Name };
            File.WriteAllText(Path.Combine(dir, "project.json"), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            _eventBus.Publish(new LoadProjectEvent { Path = dir });
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