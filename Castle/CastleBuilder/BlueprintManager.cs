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
        private static BlueprintManager _instance;
        private static string _previousContext = "Scene Editor";

        private static void EnsureInitialized(EventBus eventBus)
        {
            if (_instance == null && eventBus != null)
            {
                _instance = new BlueprintManager(eventBus);
                Console.WriteLine("[BlueprintManager] Lazy-initialized (event subscriptions now active)");
            }
        }

        public BlueprintManager(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<NewProjectEvent>(OnNewProject);
            _eventBus.Subscribe<LoadProjectEvent>(OnLoadProject);
            _eventBus.Subscribe<SaveProjectEvent>(OnSaveProject);
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            _eventBus.Subscribe<ContextChangedEvent>(OnContextChanged);
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            _configPath = GetDefaultIDEPath();
            Console.WriteLine("[BlueprintManager] Constructor finished - all events subscribed");
        }

        public static void Load(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EnsureInitialized(eventBus);
            var idePanel = new IDEBasePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(idePanel) { Mode = OpenMode.Replace });
        }

        public static void CreateNewProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, UIOverlay overlay)
        {
            EnsureInitialized(eventBus);
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
            EnsureInitialized(eventBus);
            Console.WriteLine("[BlueprintManager] SaveCurrentProject called - direct save");
            DoProjectSave();
        }

        private static void DoProjectSave()
        {
            Console.WriteLine("[BlueprintManager.DoProjectSave] === DIRECT SAVE START ===");
            string projectPath = ProjectSettings.Current.ActiveProject;
            Console.WriteLine($"[BlueprintManager.DoProjectSave] ActiveProject from settings: '{projectPath}'");
            if (string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine("[BlueprintManager.DoProjectSave] ERROR: No active project - save aborted");
                return;
            }
            string jsonPath = Path.Combine(projectPath, "project.json");
            Console.WriteLine($"[BlueprintManager.DoProjectSave] Writing to: {jsonPath}");
            ProjectData data;
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                data = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();
                Console.WriteLine($"[BlueprintManager.DoProjectSave] Loaded existing project.json - Name: {data.Name}");
            }
            else
            {
                data = new ProjectData { Name = Path.GetFileName(projectPath) };
                Console.WriteLine("[BlueprintManager.DoProjectSave] Creating new project data");
            }
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("[BlueprintManager.DoProjectSave] project.json written");
            ProjectLayoutManager.SaveCurrentLayout(data.LastContext ?? "Scene Editor");
            Console.WriteLine($"[BlueprintManager.DoProjectSave] Layout saved for context '{data.LastContext}'");
        }

        public static void SaveProjectAs(string folder, string name, EventBus eventBus)
        {
            EnsureInitialized(eventBus);
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
            Console.WriteLine($"[BlueprintManager.OnLoadProject] ActiveProject set to: {evt.Path}");
            string jsonPath = Path.Combine(evt.Path, "project.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                var data = JsonSerializer.Deserialize<ProjectData>(json);
                if (data != null)
                {
                    ProjectSettings.Current.CameraType = data.CameraType;
                    _previousContext = data.LastContext ?? "Scene Editor";
                    Console.WriteLine($"[BlueprintManager.OnLoadProject] Loaded project '{data.Name}' - Last Context: {_previousContext}");
                    ProjectLayoutManager.LoadLayoutForContext(_previousContext);
                }
            }
            SaveIDEState();
        }

        private void OnSaveProject(SaveProjectEvent evt)
        {
            Console.WriteLine("[BlueprintManager.OnSaveProject] SaveProjectEvent received - calling direct save");
            DoProjectSave();
        }

        private void OnContextChanged(ContextChangedEvent evt)
        {
            string newContext = evt.Context ?? "Scene Editor";
            Console.WriteLine($"[BlueprintManager.OnContextChanged] Switching from '{_previousContext}' → '{newContext}'");

            // 1. SAVE PREVIOUS BLADE FIRST (always works, even for TempLayouts)
            if (!string.IsNullOrEmpty(_previousContext))
            {
                ProjectLayoutManager.SaveCurrentLayout(_previousContext);
            }

            // 2. CLEAR CURRENT WORKSPACE
            var strategy = PanelManager.Current?.IDEStrategy;
            strategy?.ClearAll();

            // 3. UPDATE PROJECT.JSON (only if project is open) + RESTORE NEW BLADE
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (!string.IsNullOrEmpty(projectPath))
            {
                string jsonPath = Path.Combine(projectPath, "project.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var data = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();
                    data.LastContext = newContext;
                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                }
            }

            // 4. LOAD THE NEW BLADE (ProjectLayoutManager now handles TempLayouts automatically)
            ProjectLayoutManager.LoadLayoutForContext(newContext);

            _previousContext = newContext;
            Console.WriteLine($"[BlueprintManager.OnContextChanged] Context switch complete → '{newContext}' layout restored");
        }

        private void OnFileSelected(FileSelectedEvent e)
        {
            if (string.IsNullOrEmpty(e.Path)) return;
            string projectPath = e.Path;
            if (File.Exists(projectPath) && !Directory.Exists(projectPath))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }
            if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
            {
                Console.WriteLine($"[BlueprintManager.OnFileSelected] LoadProject selected folder: {projectPath}");
                ProjectSettings.Current.ActiveProject = projectPath;
                OnLoadProject(new LoadProjectEvent { Path = projectPath });
            }
            else
            {
                Console.WriteLine($"[BlueprintManager.OnFileSelected] Ignored selection (not a valid project folder): {e.Path}");
            }
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