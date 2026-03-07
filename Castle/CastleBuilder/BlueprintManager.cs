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

        public static void SaveProjectAs(string projectPath, string name, EventBus eventBus)
        {
            string dir = Path.Combine(projectPath, name);
            Directory.CreateDirectory(dir);
            eventBus.Publish(new LoadProjectEvent { Path = dir });
            Console.WriteLine($"[BlueprintManager] Project created/saved as: {dir}");
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
            Console.WriteLine($"[BlueprintManager] Active project now: {_activeProject}");
            SaveIDEState();
        }

        private void OnSaveProject(SaveProjectEvent evt)
        {
            if (string.IsNullOrEmpty(_activeProject))
            {
                Console.WriteLine("[BlueprintManager] No active project - use Save As");
                return;
            }
            Console.WriteLine($"[BlueprintManager] Saved to: {_activeProject}");
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