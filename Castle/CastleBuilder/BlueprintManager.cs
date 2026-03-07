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
using SiegeEngine.Core.UI; // for IDEBasePanel

namespace CastleBuilder
{
    public class ProjectData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Mode { get; set; }
        public bool AllowMods { get; set; }
        public List<string> Scenes { get; set; } = new List<string>();
        public List<string> Mods { get; set; } = new List<string>();
        public string Version { get; set; } = "1.0";
        public string LastOpenedScene { get; set; }
    }

    public class SceneData
    {
        public string Name { get; set; }
        public string TerrainPath { get; set; }
        public string Type { get; set; } = "Terrain";
    }

    public class BlueprintManager
    {
        private readonly EventBus _eventBus;
        private string _activeProject;
        private readonly string _configPath;
        private readonly List<SceneData> _openScenes = new List<SceneData>();

        public BlueprintManager(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<NewProjectEvent>(OnNewProject);
            _eventBus.Subscribe<LoadProjectEvent>(OnLoadProject);
            _eventBus.Subscribe<SaveProjectEvent>(OnSaveProject);
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            _configPath = GetDefaultIDEPath();
            LoadIDEState();
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

            _openScenes.Clear();
            _openScenes.Add(new SceneData { Name = "Main", TerrainPath = Path.Combine(dir, "Scenes", "Main.terrain") });

            _eventBus.Publish(new LoadProjectEvent { Path = dir });
        }

        private void OnLoadProject(LoadProjectEvent evt)
        {
            string projectJsonPath = Path.Combine(evt.Path, "project.json");
            if (!File.Exists(projectJsonPath))
            {
                Console.WriteLine($"BlueprintManager: project.json not found at {projectJsonPath}");
                return;
            }
            string json = File.ReadAllText(projectJsonPath);
            var proj = JsonSerializer.Deserialize<ProjectData>(json);
            _activeProject = evt.Path;

            _openScenes.Clear();
            if (proj.Scenes.Count > 0)
            {
                foreach (var sceneName in proj.Scenes)
                {
                    _openScenes.Add(new SceneData { Name = sceneName, TerrainPath = Path.Combine(evt.Path, "Scenes", $"{sceneName}.terrain") });
                }
            }
            else
            {
                _openScenes.Add(new SceneData { Name = "Main", TerrainPath = Path.Combine(evt.Path, "Scenes", "Main.terrain") });
            }

            Console.WriteLine($"BlueprintManager: Loaded project {proj.Name} from {evt.Path} with {_openScenes.Count} scene(s)");
            SaveIDEState();
        }

        private void OnSaveProject(SaveProjectEvent evt)
        {
            string path = evt.Path ?? _activeProject;
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("BlueprintManager: No active project to save");
                return;
            }

            string projectJsonPath = Path.Combine(path, "project.json");
            if (!File.Exists(projectJsonPath))
            {
                Console.WriteLine($"BlueprintManager: project.json not found at {projectJsonPath}");
                return;
            }

            string json = File.ReadAllText(projectJsonPath);
            var proj = JsonSerializer.Deserialize<ProjectData>(json);

            proj.Scenes.Clear();
            foreach (var scene in _openScenes)
            {
                proj.Scenes.Add(scene.Name);
            }
            proj.LastOpenedScene = _openScenes.Count > 0 ? _openScenes[0].Name : null;

            json = JsonSerializer.Serialize(proj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(projectJsonPath, json);

            Console.WriteLine($"BlueprintManager: Saved project at {path} ({proj.Scenes.Count} scene(s))");
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

        private void LoadIDEState()
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (config.TryGetValue("active_project", out string activePath) && Directory.Exists(activePath))
                {
                    _eventBus.Publish(new LoadProjectEvent { Path = activePath });
                }
            }
        }

        private void SaveIDEState()
        {
            if (string.IsNullOrEmpty(_activeProject)) return;
            var config = new Dictionary<string, string>
            {
                { "active_project", _activeProject }
            };
            string json = JsonSerializer.Serialize(config);
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
            File.WriteAllText(_configPath, json);
        }

        // NEW: Called dynamically via data-hook="CastleBuilder.BlueprintManager.Load"
        // This is the relay point you asked for.
        public static void Load(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var idePanel = new IDEBasePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(idePanel) { Mode = OpenMode.Replace });
            // Restore last saved project and dock/layout state
            new BlueprintManager(eventBus).LoadIDEState();
        }
    }
}