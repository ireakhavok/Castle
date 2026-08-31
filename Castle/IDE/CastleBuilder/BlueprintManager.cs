// Folder: CastleBuilder
// File: BlueprintManager.cs
using CastleBuilder.Events;
using Keystone;
using MapRoom;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Terrain;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
                Console.WriteLine("[BlueprintManager] Lazy-initialized");
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
            EditorHistory.Current.Initialize(_eventBus);
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
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, EntityData.SerializerOptions));
            Directory.CreateDirectory(Path.Combine(dir, "Scenes"));
            Directory.CreateDirectory(Path.Combine(dir, "Assets"));
            Directory.CreateDirectory(Path.Combine(dir, "Mods"));
            eventBus.Publish(new LoadProjectEvent { Path = dir });
            Console.WriteLine($"[BlueprintManager] New project created: {dir}");
            Load(renderContext, controlContext, window, eventBus);
        }
        private void OnCreateTerrain(CreateTerrainEvent evt)
        {
            SceneData sceneData = ProjectSettings.Current.CurrentSceneData;
            if (sceneData == null) return;
            // Keep the new scene visible in the live EditorScene project data so the scene selector
            // and cache see it immediately. NEVER write project.json here — disk persistence is
            // only via explicit Save / Save As / Export / Play payload materialisation.
            if (EditorScene.Current != null)
            {
                var projectData = EditorScene.Current.GetProjectData();
                if (projectData != null)
                {
                    if (projectData.Scenes == null)
                        projectData.Scenes = new Dictionary<string, SceneData>();
                    if (!projectData.Scenes.ContainsKey(sceneData.Name))
                    {
                        projectData.Scenes[sceneData.Name] = sceneData;
                        projectData.LastOpenedScene = sceneData.Name;
                        Console.WriteLine($"[BlueprintManager.OnCreateTerrain] New scene '{sceneData.Name}' registered in-memory only (no disk write)");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[BlueprintManager.OnCreateTerrain] New scene '{sceneData.Name}' stays in central memory only (Level + cache populated by NewTerrainPanel + EditorScene)");
            }
            var panelManager = PanelManager.Current;
            if (panelManager != null)
            {
                foreach (var p in panelManager.GetAllPanels())
                {
                    if (p is SceneEditorPanel sep) sep.RefreshSceneList();
                }
            }
        }
        public static void CreateNewScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EnsureInitialized(eventBus);
            NewTerrainPanel.Open(renderContext, controlContext, window, eventBus);
        }
        public static void EnsureDefaultSceneIfNeeded()
        {
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
            {
                string jsonPath = Path.Combine(projectPath, "project.json");
                if (!File.Exists(jsonPath)) return;
                string json = File.ReadAllText(jsonPath);
                var data = JsonSerializer.Deserialize<ProjectData>(json, EntityData.SerializerOptions) ?? new ProjectData();
                if (data.Scenes == null || data.Scenes.Count == 0)
                {
                    data.Scenes = new Dictionary<string, SceneData>();
                    var level = new Level() { Name = "Main" };
                    var sceneData = new SceneData { Name = "Main", SceneType = "TerrainTest" };
                    sceneData.Entities = level.Entities.ConvertAll(e => e.ToData());
                    sceneData.Terrain = level.Terrain ?? new TerrainData();
                    sceneData.Environment = level.Environment ?? new EnvironmentSettings();
                    sceneData.Skybox = level.Skybox ?? new SkyboxData();
                    data.Scenes["Main"] = sceneData;
                    data.LastOpenedScene = "Main";
                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, EntityData.SerializerOptions));
                    ProjectSettings.Current.SetCurrentLevel(level);
                    Console.WriteLine("[BlueprintManager] Auto-created default scene 'Main' with Level as single source of truth");
                }
                return;
            }
            Console.WriteLine("[BlueprintManager.EnsureDefaultSceneIfNeeded] No active project - skipping default scene creation");
        }
        public static void SaveCurrentProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EnsureInitialized(eventBus);
            Console.WriteLine("[BlueprintManager.SaveCurrentProject] SaveCurrentProject called");
            if (string.IsNullOrEmpty(ProjectSettings.Current.ActiveProject))
            {
                Console.WriteLine("[BlueprintManager] No active project → opening Save As dialog (exact same logic as Save As + flush)");
                SaveProjectAs(renderContext, controlContext, window, eventBus);
                return;
            }
            DoProjectSave();
        }
        /// <summary>
        /// Commit any in-progress PropertiesPanel Scene Settings typing into the live buffer
        /// before Save/Play reads CurrentSceneSettings.
        /// </summary>
        private static void FlushLiveEditorState()
        {
            var pm = PanelManager.Current;
            if (pm == null) return;
            foreach (var panel in pm.GetAllPanels())
            {
                if (panel is PropertiesPanel props)
                    props.FlushLiveSettings();
            }
        }
        private static void DoProjectSave()
        {
            Console.WriteLine("[BlueprintManager.DoProjectSave] === STAGE 1: Level-Centric Save Started ===");
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
                data = JsonSerializer.Deserialize<ProjectData>(json, EntityData.SerializerOptions) ?? new ProjectData();
                Console.WriteLine($"[BlueprintManager.DoProjectSave] Loaded existing project.json - Name: {data.Name}");
            }
            else
            {
                data = new ProjectData { Name = Path.GetFileName(projectPath) };
                Console.WriteLine("[BlueprintManager.DoProjectSave] Creating new project data");
            }
            if (data.Scenes == null) data.Scenes = new Dictionary<string, SceneData>();
            // Terrain flush first (may call SetCurrentTerrain with stale SceneData from _projectData).
            EditorScene.Current?.FlushActiveSceneData();
            // Commit typed Scene Settings AFTER terrain flush so SetCurrentTerrain cannot clobber them.
            FlushLiveEditorState();
            string currentSceneName = ProjectSettings.Current.CurrentSceneName ?? "NewTerrain";
            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null)
            {
                if (!data.Scenes.TryGetValue(currentSceneName, out var sceneData))
                {
                    sceneData = new SceneData { Name = currentSceneName, SceneType = "TerrainTest" };
                    data.Scenes[currentSceneName] = sceneData;
                }
                sceneData.Entities.Clear();
                sceneData.Entities = level.Entities.ConvertAll(e => e.ToData());
                sceneData.Terrain = level.Terrain ?? new TerrainData();
                sceneData.Environment = level.Environment ?? new EnvironmentSettings();
                if (level.Skybox != null)
                {
                    EnsureSkyboxAssetsInProject(level.Skybox, projectPath, currentSceneName);
                }
                sceneData.Skybox = level.Skybox ?? new SkyboxData();
                if (level.CustomData != null) sceneData.CustomData = level.CustomData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                if (sceneData.Terrain != null)
                {
                    sceneData.Terrain.EmbeddedHeightmapData = null;
                    sceneData.Terrain.EmbeddedHeightmapWidth = 0;
                    sceneData.Terrain.EmbeddedHeightmapHeight = 0;
                }
                // Prefer explicit per-scene lookup so we never assign null when the buffer has an entry.
                sceneData.Settings = ProjectSettings.Current.GetOrCreateSceneSettings(currentSceneName)
                                    ?? ProjectSettings.Current.CurrentSceneSettings;
                Console.WriteLine($"[BlueprintManager.DoProjectSave] Synced {level.Entities.Count} entities + terrain + environment + skybox + Settings from Level (authoritative) → SceneData");
            }
            var uniquePackKeys = data.Scenes.Values
                .SelectMany(s => s.Entities ?? new List<EntityData>())
                .Where(e => !string.IsNullOrEmpty(e.AssetPackKey))
                .Select(e => e.AssetPackKey)
                .Distinct()
                .ToList();
            if (uniquePackKeys.Count > 0 && ModelManager.Instance != null)
            {
                string assetsDir = Path.Combine(projectPath, "Assets");
                Directory.CreateDirectory(assetsDir);
                foreach (var packKey in uniquePackKeys)
                {
                    ModelManager.Instance.MaterializeAssetPack(packKey, assetsDir);
                }
                Console.WriteLine($"[BlueprintManager.DoProjectSave] Materialized {uniquePackKeys.Count} asset packs to Assets/ folder");
            }
            SaveAllPanelStates(data);
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, EntityData.SerializerOptions));
            Console.WriteLine("[BlueprintManager.DoProjectSave] project.json written with Level as single source of truth + terrain + clean entities + materialized asset packs");
            if (!string.IsNullOrEmpty(_previousContext))
            {
                Console.WriteLine($"[BlueprintManager.DoProjectSave] Forcing CURRENT blade '{_previousContext}' into memory");
                ProjectLayoutManager.SaveCurrentLayout(_previousContext);
            }
            ProjectLayoutManager.FlushAllToDisk();
            Console.WriteLine("[BlueprintManager.DoProjectSave] All blades committed to disk");
        }
        public sealed class PlaySnapshot
        {
            public string LevelName;
            public Level Level;
            public SceneData SceneData;
            public float[,] Heightmap;
        }

        public static PlaySnapshot BuildCurrentPlaySnapshot()
        {
            FlushLiveEditorState();
            var level = ProjectSettings.Current.CurrentLevel ?? new Level();
            string levelName = ProjectSettings.Current.CurrentSceneName ?? level.Name ?? "Main";
            var sceneData = new SceneData
            {
                Name = levelName,
                SceneType = "Gameplay",
                Entities = level.Entities.ConvertAll(e => e.ToData()),
                Terrain = level.Terrain != null
                    ? new TerrainData
                    {
                        HeightmapPath = level.Terrain.HeightmapPath,
                        ColorTexturePath = level.Terrain.ColorTexturePath,
                        NormalTexturePath = level.Terrain.NormalTexturePath,
                        SplatMapPath = level.Terrain.SplatMapPath,
                        Materials = level.Terrain.Materials != null
                            ? new List<TerrainMaterial>(level.Terrain.Materials)
                            : new List<TerrainMaterial>(),
                        WorldScaleX = level.Terrain.WorldScaleX,
                        WorldScaleZ = level.Terrain.WorldScaleZ,
                        VerticalExaggeration = level.Terrain.VerticalExaggeration
                    }
                    : new TerrainData(),
                Environment = level.Environment ?? new EnvironmentSettings(),
                Skybox = level.Skybox,
                CustomData = level.CustomData != null
                    ? new Dictionary<string, object>(level.CustomData)
                    : new Dictionary<string, object>(),
                Settings = ProjectSettings.Current.GetOrCreateSceneSettings(levelName)
                           ?? ProjectSettings.Current.CurrentSceneSettings
            };
            // Prefer the real LiveSceneState heightmap (correct dimensions) over any default 200x200 buffer.
            float[,] liveHeightmap = null;
            var liveState = ProjectStateManager.Current.GetLiveState(levelName);
            if (liveState != null && liveState.Heightmap != null)
                liveHeightmap = liveState.Heightmap;
            if (liveHeightmap == null)
                liveHeightmap = ProjectSettings.Current.GetUnsavedHeightmap(levelName)
                                ?? ProjectSettings.Current.CurrentHeightmap;
            if (liveHeightmap != null)
            {
                int width = liveHeightmap.GetLength(0);
                int height = liveHeightmap.GetLength(1);
                if (sceneData.Terrain == null) sceneData.Terrain = new TerrainData();
                sceneData.Terrain.EmbeddedHeightmapWidth = width;
                sceneData.Terrain.EmbeddedHeightmapHeight = height;
                sceneData.Terrain.EmbeddedHeightmapData = LinearizeHeightmap(liveHeightmap);
            }
            return new PlaySnapshot
            {
                LevelName = levelName,
                Level = level,
                SceneData = sceneData,
                Heightmap = liveHeightmap
            };
        }

        public static string BuildPlayPayloadFile()
        {
            var snap = BuildCurrentPlaySnapshot();
            var level = snap.Level;
            string levelName = snap.LevelName;
            var sceneData = snap.SceneData;
            byte[] levelBytes = level.Serialize();
            string levelDataBase64 = Convert.ToBase64String(levelBytes);
            var transfer = new PlayPayloadTransfer
            {
                LevelName = levelName,
                LevelDataBase64 = levelDataBase64,
                SceneData = sceneData
            };
            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuntimeTemp");
            Directory.CreateDirectory(tempDir);
            string payloadPath = Path.Combine(tempDir, $"play_payload_{Guid.NewGuid():N}.json");
            File.WriteAllText(payloadPath, JsonSerializer.Serialize(transfer, EntityData.SerializerOptions));
            return payloadPath;
        }
        private static float[] LinearizeHeightmap(float[,] map)
        {
            if (map == null) return null;
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            var data = new float[width * height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    data[x * height + y] = map[x, y];
                }
            }
            return data;
        }
        private class PlayPayloadTransfer
        {
            public string LevelName { get; set; }
            public string LevelDataBase64 { get; set; }
            public SceneData SceneData { get; set; }
        }
        private static void EnsureSkyboxAssetsInProject(SkyboxData skybox, string projectPath, string sceneName)
        {
            if (skybox == null) return;
            string skyDir = Path.Combine(projectPath, "Assets", "Skyboxes", sceneName);
            Directory.CreateDirectory(skyDir);
            if (!string.IsNullOrEmpty(skybox.CubemapPath))
            {
                string source = skybox.CubemapPath;
                if (!Path.IsPathRooted(source))
                    source = Path.GetFullPath(Path.Combine(projectPath, source));
                if (File.Exists(source))
                {
                    string dest = Path.Combine(skyDir, Path.GetFileName(source));
                    string fullSource = Path.GetFullPath(source);
                    string fullDest = Path.GetFullPath(dest);
                    if (!string.Equals(fullSource, fullDest, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Copy(fullSource, fullDest, true);
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine($"[BlueprintManager.EnsureSkyboxAssetsInProject] Cubemap copy skipped (locked or in use): {ex.Message}");
                        }
                    }
                    skybox.CubemapPath = Path.GetRelativePath(projectPath, fullDest).Replace("\\", "/");
                }
            }
            if (skybox.Faces != null)
            {
                for (int i = 0; i < skybox.Faces.Count; i++)
                {
                    string facePath = skybox.Faces[i];
                    if (string.IsNullOrEmpty(facePath)) continue;
                    string source = facePath;
                    if (!Path.IsPathRooted(source))
                        source = Path.GetFullPath(Path.Combine(projectPath, source));
                    if (!File.Exists(source)) continue;
                    string dest = Path.Combine(skyDir, Path.GetFileName(source));
                    string fullSource = Path.GetFullPath(source);
                    string fullDest = Path.GetFullPath(dest);
                    if (!string.Equals(fullSource, fullDest, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Copy(fullSource, fullDest, true);
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine($"[BlueprintManager.EnsureSkyboxAssetsInProject] Face copy skipped (locked or in use): {ex.Message}");
                        }
                    }
                    skybox.Faces[i] = Path.GetRelativePath(projectPath, fullDest).Replace("\\", "/");
                }
            }
            Console.WriteLine($"[BlueprintManager.EnsureSkyboxAssetsInProject] Skybox assets materialized to {skyDir} with relative paths stored in Level.Skybox");
        }
        public static void SaveProjectAs(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EnsureInitialized(eventBus);
            var savePanel = new SaveProjectPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(savePanel) { Mode = OpenMode.Overlay });
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
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, EntityData.SerializerOptions));
                Directory.CreateDirectory(Path.Combine(dir, "Scenes"));
                Directory.CreateDirectory(Path.Combine(dir, "Assets"));
            }
            ProjectSettings.Current.ActiveProject = dir;
            EditorScene.Current?.FlushActiveSceneData();
            DoProjectSave();
            eventBus.Publish(new LoadProjectEvent { Path = dir });
            Console.WriteLine($"[BlueprintManager.SaveProjectAs] Save As complete - new project fully populated and active at {dir}");
        }
        public static void CopyDirectory(string sourceDir, string targetDir)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDir);
            DirectoryInfo diTarget = new DirectoryInfo(targetDir);
            if (!diTarget.Exists) diTarget.Create();
            foreach (FileInfo fi in diSource.GetFiles())
            {
                string targetFile = Path.Combine(diTarget.FullName, fi.Name);
                fi.CopyTo(targetFile, true);
            }
            foreach (DirectoryInfo diSourceSubDir in diSource.GetDirectories())
            {
                if (diSourceSubDir.Name.Equals("exported", StringComparison.OrdinalIgnoreCase) ||
                    diSourceSubDir.Name.Equals("IDE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[CopyDirectory] Skipping IDE/export folder: {diSourceSubDir.Name}");
                    continue;
                }
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
            else if (evt.Hook == "SkyboxSet")
            {
                var level = ProjectSettings.Current.CurrentLevel;
                if (level != null && evt.Data != null && evt.Data.TryGetValue("skybox", out var skyJson))
                {
                    var sky = JsonSerializer.Deserialize<SkyboxData>(skyJson);
                    level.Skybox = sky;
                    Console.WriteLine($"[BlueprintManager] SkyboxSet handled - Level.Skybox updated (Enabled={sky?.Enabled})");
                }
            }
            else if (evt.Hook == "ProjectSaveRequest")
            {
                DoProjectSave();
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
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, EntityData.SerializerOptions));
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
                var data = JsonSerializer.Deserialize<ProjectData>(json, EntityData.SerializerOptions);
                if (data != null)
                {
                    ProjectSettings.Current.CameraType = data.CameraType;
                    _previousContext = data.LastContext ?? "Scene Editor";
                    Console.WriteLine($"[BlueprintManager.OnLoadProject] Loaded project '{data.Name}' - Last Context: {_previousContext}");
                    ProjectLayoutManager.LoadLayoutForContext(_previousContext);
                    LoadAllPanelStates(data);
                    string currentScene = data.LastOpenedScene ?? (data.Scenes != null && data.Scenes.Count > 0 ? new List<string>(data.Scenes.Keys)[0] : "Main");
                    if (data.Scenes != null && data.Scenes.TryGetValue(currentScene, out var sd))
                    {
                        var level = new Level(_eventBus) { Name = currentScene };
                        if (sd.Entities != null)
                        {
                            level.Entities.Clear();
                            foreach (var ed in sd.Entities)
                                level.Entities.Add(Entity.FromData(ed));
                        }
                        level.Terrain = sd.Terrain ?? new TerrainData();
                        level.Environment = sd.Environment ?? new EnvironmentSettings();
                        level.Skybox = sd.Skybox ?? new SkyboxData();
                        ProjectSettings.Current.SetCurrentLevel(level);
                        if (sd.Settings != null)
                        {
                            ProjectSettings.Current.SetCurrentSceneSettings(sd.Settings);
                        }
                        Console.WriteLine($"[BlueprintManager] Loaded Level '{currentScene}' with {level.Entities.Count} entities as single source of truth");
                    }
                }
            }
            SaveIDEState();

            // Force the live Scene Editor viewport to rebind to the newly loaded project
            // so pure-client hosted previews (and terrain) activate for the current ActiveProject.
            if (EditorScene.Current != null)
            {
                Console.WriteLine("[BlueprintManager.OnLoadProject] Forcing EditorScene.LoadProjectData for newly loaded project");
                EditorScene.Current.LoadProjectData(true);
            }
        }
        private void OnSaveProject(SaveProjectEvent evt)
        {
            Console.WriteLine("[BlueprintManager.OnSaveProject] SaveProjectEvent received - calling Level-centric save");
            if (string.IsNullOrEmpty(ProjectSettings.Current.ActiveProject))
            {
                Console.WriteLine("[BlueprintManager.OnSaveProject] No active project on Save - opening New Project dialog");
                return;
            }
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
            if (strategy is IDEDockingStrategy ide)
            {
                ide.SwitchBlade(newContext);
            }
            else
            {
                strategy?.ClearAll();
            }
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (!string.IsNullOrEmpty(projectPath))
            {
                string jsonPath = Path.Combine(projectPath, "project.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var data = JsonSerializer.Deserialize<ProjectData>(json, EntityData.SerializerOptions) ?? new ProjectData();
                    data.LastContext = newContext;
                    SaveAllPanelStates(data);
                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, EntityData.SerializerOptions));
                }
            }
            _previousContext = newContext;
            Console.WriteLine($"[BlueprintManager.OnContextChanged] Context switch complete → '{newContext}' (memory hotswap, no close/dispose)");
        }
        private void OnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData as string != "LoadProject") return;
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
            string currentContext = _previousContext ?? "Scene Editor";
            foreach (var panel in panelManager.GetAllPanels())
            {
                if (panel is IDataAwarePanel aware)
                {
                    try
                    {
                        var state = aware.SavePanelState();
                        if (!state.ValueKind.HasFlag(JsonValueKind.Undefined))
                        {
                            data.PanelStates[currentContext + "_" + aware.DataKey] = state;
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