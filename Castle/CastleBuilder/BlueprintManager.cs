using CastleBuilder.Events;
using Keystone;
using MapRoom;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Terrain;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ToolChest;
namespace CastleBuilder
{
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
            _eventBus.Subscribe<CreateTerrainEvent>(OnCreateTerrain);
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
            string root = Keystone.ProjectSettings.Current.ProjectsRoot;
            string safeName = name.Replace(" ", "_").ReplaceInvalidFileChars();
            string dir = Path.Combine(root, safeName);
            Directory.CreateDirectory(dir);
            var data = new Keystone.ProjectData
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
        private void OnCreateTerrain(CreateTerrainEvent evt)
        {
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                Console.WriteLine("[BlueprintManager.OnCreateTerrain] No active project - new scene stays in central memory only");
                return;
            }
            SceneData sceneData = ProjectSettings.Current.CurrentSceneData;
            if (sceneData == null) return;
            Console.WriteLine($"[BlueprintManager.OnCreateTerrain] New scene '{sceneData.Name}' added to in-memory list - no disk write until Save");
            // === REMOVED: LoadProjectEvent publish (was causing unwanted full reload on every new scene/terrain) ===
            // The scene is already registered in memory and will be persisted on next explicit SaveProject.
        }
        public static void CreateNewScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EnsureInitialized(eventBus);
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                Console.WriteLine("[BlueprintManager.CreateNewScene] ERROR: No active project");
                return;
            }
            string jsonPath = Path.Combine(projectPath, "project.json");
            ProjectData data;
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                data = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();
            }
            else
            {
                data = new ProjectData { Name = Path.GetFileName(projectPath) };
            }
            if (data.Scenes == null) data.Scenes = new Dictionary<string, SceneData>();
            string sceneName = $"Scene_{data.Scenes.Count + 1}";
            var sceneData = new SceneData { Name = sceneName, SceneType = "TerrainTest" };
            sceneData.Terrain = new TerrainData
            {
                WorldScaleX = 1.0f,
                WorldScaleZ = 1.0f,
                VerticalExaggeration = 1.0f
            };
            int w = 200;
            int h = 200;
            float[,] newHeightmap = new float[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    newHeightmap[x, y] = 0f;
            ProjectSettings.Current.SetCurrentTerrain(sceneData, newHeightmap, sceneName);
            var level = new Level(eventBus) { Name = sceneName };
            level.Terrain = sceneData.Terrain;
            ProjectSettings.Current.SetCurrentLevel(level);
            data.Scenes[sceneName] = sceneData;
            data.LastOpenedScene = sceneName;
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[BlueprintManager] New scene created: {sceneName} (200×200 default terrain + Level container initialized)");
            var terrainPanel = new TerrainCreatorPanel(renderContext, controlContext, window, eventBus, sceneData);
            eventBus.Publish(new OpenPanelEvent(terrainPanel) { Mode = OpenMode.Replace });
            eventBus.Publish(new LoadProjectEvent { Path = projectPath });
        }
        public static void EnsureDefaultSceneIfNeeded()
        {
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return;
            string jsonPath = Path.Combine(projectPath, "project.json");
            if (!File.Exists(jsonPath)) return;
            string json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();
            if (data.Scenes == null || data.Scenes.Count == 0)
            {
                data.Scenes = new Dictionary<string, SceneData>();
                var sceneData = new SceneData { Name = "Main", SceneType = "TerrainTest" };
                sceneData.Terrain = new TerrainData
                {
                    WorldScaleX = 1.0f,
                    WorldScaleZ = 1.0f,
                    VerticalExaggeration = 1.0f
                };
                data.Scenes["Main"] = sceneData;
                data.LastOpenedScene = "Main";
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("[BlueprintManager] Auto-created default scene 'Main' with 200×200 terrain");
            }
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
            EditorScene.Current?.FlushActiveSceneData();
            if (EditorScene.Current != null)
            {
                data = EditorScene.Current.GetProjectData() ?? data;
            }
            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null)
            {
                string currentSceneName = ProjectSettings.Current.CurrentSceneName ?? "Main";
                if (data.Scenes != null && data.Scenes.TryGetValue(currentSceneName, out var sd))
                {
                    sd.Entities = level.Entities.ConvertAll(e => e.ToData());
                    sd.CustomData["Level"] = Convert.ToBase64String(level.Serialize());
                    Console.WriteLine($"[BlueprintManager.DoProjectSave] Level '{level.Name}' serialized into scene '{currentSceneName}' ({level.Entities.Count} entities)");
                }
            }
            if (ProjectSettings.Current.CurrentHeightmap != null && !string.IsNullOrEmpty(ProjectSettings.Current.CurrentSceneName))
            {
                string sceneName = ProjectSettings.Current.CurrentSceneName;
                string saveDir = Path.Combine(projectPath, "Assets", "Terrain");
                Directory.CreateDirectory(saveDir);
                string tifPath = Path.Combine(saveDir, sceneName + ".tif");
                float scaleX = level?.Terrain?.WorldScaleX ?? 1.0f;
                float scaleZ = level?.Terrain?.WorldScaleZ ?? 1.0f;
                CustomTerrainParser.SaveFloatTiff(tifPath, ProjectSettings.Current.CurrentHeightmap, scaleX, scaleZ);
                if (data.Scenes != null && data.Scenes.TryGetValue(sceneName, out var sd) && sd.Terrain != null)
                {
                    sd.Terrain.HeightmapPath = Path.GetRelativePath(projectPath, tifPath);
                    if (level != null) level.Terrain.HeightmapPath = sd.Terrain.HeightmapPath;
                }
                Console.WriteLine($"[BlueprintManager.DoProjectSave] Auto-saved heightmap for scene '{sceneName}' → {tifPath} (scale {scaleX:F2}x{scaleZ:F2})");
            }
            SaveAllPanelStates(data);
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("[BlueprintManager.DoProjectSave] project.json written with terrain reference + Level data");
            if (!string.IsNullOrEmpty(_previousContext))
            {
                Console.WriteLine($"[BlueprintManager.DoProjectSave] Forcing CURRENT blade '{_previousContext}' into memory");
                ProjectLayoutManager.SaveCurrentLayout(_previousContext);
            }
            ProjectLayoutManager.FlushAllToDisk();
            Console.WriteLine("[BlueprintManager.DoProjectSave] All blades committed to disk");
        }
        public static void SaveProjectAs(string folder, string name, EventBus eventBus)
        {
            EnsureInitialized(eventBus);
            if (string.IsNullOrEmpty(folder))
                folder = ProjectSettings.Current.ProjectsRoot;
            string safeName = name.Replace(" ", "_").ReplaceInvalidFileChars();
            string dir = Path.Combine(folder, safeName);
            Directory.CreateDirectory(dir);
            string currentProject = ProjectSettings.Current.ActiveProject;
            if (!string.IsNullOrEmpty(currentProject) && Directory.Exists(currentProject) && currentProject != dir)
            {
                CopyDirectory(currentProject, dir);
                Console.WriteLine($"[BlueprintManager.SaveProjectAs] Copied full current project structure from '{currentProject}' to new location '{dir}'");
            }
            else
            {
                var data = new ProjectData { Name = name, LastContext = "Scene Editor" };
                string jsonPath = Path.Combine(dir, "project.json");
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                Directory.CreateDirectory(Path.Combine(dir, "Scenes"));
                Directory.CreateDirectory(Path.Combine(dir, "Assets"));
            }
            ProjectSettings.Current.ActiveProject = dir;
            DoProjectSave();
            eventBus.Publish(new LoadProjectEvent { Path = dir });
            Console.WriteLine($"[BlueprintManager.SaveProjectAs] Save As complete - new project fully populated and active at {dir}");
        }
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDir);
            DirectoryInfo diTarget = new DirectoryInfo(targetDir);
            if (!diTarget.Exists)
                diTarget.Create();
            foreach (FileInfo fi in diSource.GetFiles())
            {
                string targetFile = Path.Combine(diTarget.FullName, fi.Name);
                fi.CopyTo(targetFile, true);
            }
            foreach (DirectoryInfo diSourceSubDir in diSource.GetDirectories())
            {
                CopyDirectory(diSourceSubDir.FullName, Path.Combine(diTarget.FullName, diSourceSubDir.Name));
            }
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
                    LoadAllPanelStates(data);
                    string currentScene = data.LastOpenedScene ?? (data.Scenes != null && data.Scenes.Count > 0 ? new List<string>(data.Scenes.Keys)[0] : "Main");
                    if (data.Scenes != null && data.Scenes.TryGetValue(currentScene, out var sd) && sd.CustomData != null && sd.CustomData.TryGetValue("Level", out var levelBase64))
                    {
                        try
                        {
                            byte[] levelBytes = Convert.FromBase64String(levelBase64.ToString());
                            var level = Level.Deserialize(levelBytes, _eventBus);
                            ProjectSettings.Current.SetCurrentLevel(level);
                            if (sd.Entities != null)
                            {
                                level.Entities.Clear();
                                foreach (var ed in sd.Entities)
                                    level.Entities.Add(Entity.FromData(ed));
                            }
                            Console.WriteLine($"[BlueprintManager.OnLoadProject] Level deserialized for scene '{currentScene}' ({level.Entities.Count} entities restored)");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BlueprintManager.OnLoadProject] WARNING: Failed to deserialize Level: {ex.Message}");
                        }
                    }
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
            if (!string.IsNullOrEmpty(_previousContext))
            {
                Console.WriteLine($"[BlueprintManager.OnContextChanged] Saving previous blade '{_previousContext}' to MEMORY");
                ProjectLayoutManager.SaveCurrentLayout(_previousContext);
            }
            var strategy = PanelManager.Current?.IDEStrategy;
            strategy?.ClearAll();
            ProjectLayoutManager.LoadLayoutForContext(newContext);
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (!string.IsNullOrEmpty(projectPath))
            {
                string jsonPath = Path.Combine(projectPath, "project.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var data = JsonSerializer.Deserialize<ProjectData>(json) ?? new ProjectData();
                    data.LastContext = newContext;
                    SaveAllPanelStates(data);
                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            _previousContext = newContext;
            Console.WriteLine($"[BlueprintManager.OnContextChanged] Context switch complete → '{newContext}' (memory hotswap)");
        }
        private void OnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData as string != "LoadProject")
            {
                return;
            }
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
        private static void SaveAllPanelStates(ProjectData data)
        {
            if (data == null) return;
            data.PanelStates.Clear();
            var panelManager = PanelManager.Current;
            if (panelManager == null) return;
            foreach (var panel in panelManager.GetAllPanels())
            {
                if (panel is IDataAwarePanel aware)
                {
                    try
                    {
                        var state = aware.SavePanelState();
                        if (!state.ValueKind.HasFlag(JsonValueKind.Undefined))
                        {
                            data.PanelStates[aware.DataKey] = state;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BlueprintManager] WARNING: Failed to save state for panel {aware.DataKey}: {ex.Message}");
                    }
                }
            }
        }
        private static void LoadAllPanelStates(ProjectData data)
        {
            if (data?.PanelStates == null || data.PanelStates.Count == 0) return;
            var panelManager = PanelManager.Current;
            if (panelManager == null) return;
            foreach (var panel in panelManager.GetAllPanels())
            {
                if (panel is IDataAwarePanel aware && data.PanelStates.TryGetValue(aware.DataKey, out var state))
                {
                    try
                    {
                        aware.LoadPanelState(state);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BlueprintManager] WARNING: Failed to load state for panel {aware.DataKey}: {ex.Message}");
                    }
                }
            }
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