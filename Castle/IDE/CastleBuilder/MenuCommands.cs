// Folder: CastleBuilder
// File: MenuCommands.cs
using CastleBuilder.Events;
using Keystone;
using MapRoom;
using ReadingChamber;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Scenes.StartingPoints;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ToolChest;
namespace CastleBuilder
{
    public static class MenuCommands
    {
        private static readonly string DefaultProjectsPath = ProjectSettings.Current.ProjectsRoot;
        public static void SwitchToTerrain(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            CompanionLayoutHelper.Bind(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new ContextChangedEvent { Context = "Terrain" });
            Console.WriteLine("[MenuCommands] Switched to Terrain context");
        }
        public static void SwitchToAnimator(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            CompanionLayoutHelper.Bind(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new ContextChangedEvent { Context = "Animator" });
            Console.WriteLine("[MenuCommands] Switched to Animator context");
        }
        public static void SwitchToSceneEditor(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            CompanionLayoutHelper.Bind(renderContext, controlContext, window, eventBus);
            BlueprintManager.EnsureDefaultSceneIfNeeded();
            eventBus.Publish(new ContextChangedEvent { Context = "Scene Editor" });
            Console.WriteLine("[MenuCommands] Switched to Scene Editor context (panel opened)");
        }
        public static void SwitchToWorkshop(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            CompanionLayoutHelper.Bind(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new ContextChangedEvent { Context = "Workshop" });
            Console.WriteLine("[MenuCommands] Switched to Workshop context");
        }
        public static void OpenDefaultPanels(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            CompanionLayoutHelper.Bind(renderContext, controlContext, window, eventBus);
            CompanionLayoutHelper.OpenDefaultPanels();
        }
        public static void LoadProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            if (!Directory.Exists(DefaultProjectsPath)) Directory.CreateDirectory(DefaultProjectsPath);
            var selector = new FileSelectorPanel(renderContext, controlContext, window, eventBus, DefaultProjectsPath);
            selector.UserData = "LoadProject";
            selector.IsModal = true;
            eventBus.Publish(new OpenPanelEvent(selector) { Mode = OpenMode.Overlay });
        }
        public static void SaveProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[MenuCommands.SaveProject] Direct call to BlueprintManager save");
            BlueprintManager.SaveCurrentProject(renderContext, controlContext, window, eventBus);
        }
        public static void SaveProjectAs(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var savePanel = new SaveProjectPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(savePanel) { Mode = OpenMode.Overlay });
        }
        public static void OpenTerrain(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            TerrainCreatorPanel.OpenBlank(renderContext, controlContext, window, eventBus);
        }
        public static void OpenAnimation(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            AnimationViewerPanel.Open(renderContext, controlContext, window, eventBus);
        }
        public static void OpenAssetBrowser(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            AssetBrowserPanel.Open(renderContext, controlContext, window, eventBus);
        }
        public static void OpenProperties(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            PropertiesPanel.Open(renderContext, controlContext, window, eventBus);
        }
        public static void OpenHierarchy(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            TreeViewPanel.Open(renderContext, controlContext, window, eventBus);
        }
        public static void Open2DCreator(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            TwoDCreatorPanel.Open(renderContext, controlContext, window, eventBus);
        }
        public static void OpenEditorScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var editorPanel = new SceneEditorPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(editorPanel) { Mode = OpenMode.Overlay });
        }
        public static void CreateNewScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            NewTerrainPanel.Open(renderContext, controlContext, window, eventBus);
            Console.WriteLine("[MenuCommands.CreateNewScene] Opened NewTerrainPanel modal (central store hand-off will occur on CreateTerrain)");
        }
        public static void NewProject(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            NewProjectPanel.Open(renderContext, controlContext, window, eventBus);
            Console.WriteLine("[MenuCommands] New Project panel opened");
        }
        public static void OpenAnimationTimeline(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            AnimationTimelinePanel.Open(renderContext, controlContext, window, eventBus);
            Console.WriteLine("[MenuCommands] Animation Timeline panel opened");
        }
        public static void OpenAnimationBlend(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            AnimationBlendPanel.Open(renderContext, controlContext, window, eventBus);
            Console.WriteLine("[MenuCommands] Animation Blend panel opened");
        }
        public static void OpenAddSkybox(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            AddSkyboxPanel.Open(renderContext, controlContext, window, eventBus);
            Console.WriteLine("[MenuCommands] Opened AddSkyboxPanel");
        }
        public static void OpenAddLight(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            AddLightPanel.Open(renderContext, controlContext, window, eventBus);
            Console.WriteLine("[MenuCommands] Opened AddLightPanel");
        }
        public static void OpenPostProcess(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            PostProcessPanel.Open(renderContext, controlContext, window, eventBus);
            Console.WriteLine("[MenuCommands] Opened PostProcessPanel");
        }
        public static void OpenPlayHost(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            PlayHostPanel.Open(renderContext, controlContext, window, eventBus);
        }
        public static void PlayGame(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[MenuCommands.PlayGame] Launching CURRENT project Level in NEW isolated window (pure runtime client - pure in-memory payload via temp transfer file, no forced disk write)");
            EditorScene.Current?.FlushActiveSceneData();
            string projectPath = ProjectSettings.Current.ActiveProject ?? string.Empty;
            string levelName = ProjectSettings.Current.CurrentSceneName ?? "Main";
            string payloadFile = BlueprintManager.BuildPlayPayloadFile();
            if (!ScriptLoader.PrepareProjectForPlay(projectPath))
            {
                Console.WriteLine("[MenuCommands.PlayGame] ABORTED — project scripts failed to compile. Fix Scripts/ and try Play again.");
                return;
            }
            string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Foundation.exe");
            if (!File.Exists(exe)) exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--client --play-project \"{projectPath}\" --load-level \"{levelName}\" --play-payload-file \"{payloadFile}\" --custom-assemblies \"{ScriptLoader.GetCustomAssemblyList(projectPath)}\"",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            Process.Start(psi);
            Console.WriteLine($"[PlayGame SUCCESS] New runtime window launched with pure in-memory Level + SceneData via temp payload file (no forced save, no command-line length limit)");
        }

        private static void CopyReferencedEngineAssets(string projectPath, string exportRoot, string engineBin)
        {
            var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void ScanText(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                string[] exts = { ".wav", ".ogg", ".mp3" };
                for (int e = 0; e < exts.Length; e++)
                {
                    int i = 0;
                    while (true)
                    {
                        int hit = text.IndexOf(exts[e], i, StringComparison.OrdinalIgnoreCase);
                        if (hit < 0) break;
                        int start = hit;
                        while (start > 0)
                        {
                            char c = text[start - 1];
                            if (char.IsLetterOrDigit(c) || c == '\\' || c == '/' || c == '.' || c == ' ' || c == '_' || c == '-')
                                start--;
                            else break;
                        }
                        string rel = text.Substring(start, hit + exts[e].Length - start).Trim().Trim('"', '\'');
                        if (!string.IsNullOrWhiteSpace(rel))
                            refs.Add(rel.Replace('/', '\\'));
                        i = hit + exts[e].Length;
                    }
                }
            }
            string payload = Path.Combine(exportRoot, "play_payload.json");
            if (File.Exists(payload)) ScanText(File.ReadAllText(payload));
            string projectJson = Path.Combine(projectPath, "project.json");
            if (File.Exists(projectJson)) ScanText(File.ReadAllText(projectJson));
            string engineAssets = Path.Combine(engineBin, "Assets");
            foreach (string rel in refs)
            {
                string cleaned = rel.TrimStart('\\', '/');
                string[] sources =
                {
                    Path.Combine(engineAssets, cleaned),
                    Path.Combine(engineBin, cleaned),
                    Path.Combine(engineAssets, "Sounds", Path.GetFileName(cleaned)),
                    Path.Combine(engineAssets, "Sounds", "IDE", "Music", Path.GetFileName(cleaned))
                };
                foreach (string src in sources)
                {
                    if (!File.Exists(src)) continue;
                    string destRel = cleaned.StartsWith("Assets", StringComparison.OrdinalIgnoreCase)
                        ? cleaned
                        : Path.Combine("Assets", cleaned);
                    string dest = Path.Combine(exportRoot, destRel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(src, dest, true);
                    Console.WriteLine("[Export] Copied referenced asset " + destRel);
                    break;
                }
            }
        }

        public static void SandboxRegressionTest(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Tests] Sandbox Regression Test launched (vertical slice/demo only)");
            eventBus.Publish(new ContextChangedEvent { Context = "Runtime Gameplay" });
            SandboxScene.Launch(renderContext, controlContext, window, eventBus);
        }
        private static string ReadProjectSteamAppId(string projectPath)
        {
            try
            {
                string jsonPath = Path.Combine(projectPath, "project.json");
                if (File.Exists(jsonPath))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
                    var root = doc.RootElement;
                    foreach (string key in new[] { "SteamAppId", "steamAppId", "AppId", "appId" })
                    {
                        if (!root.TryGetProperty(key, out var el)) continue;
                        if (el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetUInt32(out uint n) && n != 0)
                            return n.ToString();
                        string s = el.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return s.Trim();
                    }
                }
            }
            catch { }
            return "2628760";
        }

        public static void ExportGame(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[MenuCommands.ExportGame] Writing game-only client into the project exported folder");
            Task.Run(() =>
            {
                try
                {
                    string projectPath = ProjectSettings.Current.ActiveProject;
                    if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                    {
                        Console.WriteLine("[Export] No active project.");
                        return;
                    }
                    BlueprintManager.SaveCurrentProject(renderContext, controlContext, window, eventBus);
                    ScriptLoader.PrepareProjectForPlay(projectPath);
                    string ideConfig = AppDomain.CurrentDomain.BaseDirectory.IndexOf(Path.DirectorySeparatorChar + "Debug" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
                        ? "Debug" : "Release";
                    var configs = ideConfig == "Debug"
                        ? new[] { "Debug", "Release" }
                        : new[] { "Release" };
                    string lastDir = null;
                    foreach (string config in configs)
                    {
                        string exportDir = Path.Combine(projectPath, "exported", config);
                        Directory.CreateDirectory(exportDir);
                        if (!ScriptLoader.PublishGameClient(exportDir, config))
                        {
                            Console.WriteLine("[Export ERROR] GameHost publish failed for " + config);
                            continue;
                        }
                        foreach (string folder in new[] { "Assets", "Scenes", "Scripts" })
                        {
                            string source = Path.Combine(projectPath, folder);
                            if (!Directory.Exists(source)) continue;
                            BlueprintManager.CopyDirectory(source, Path.Combine(exportDir, folder));
                        }
                        ScriptLoader.CopyScriptsToExport(projectPath, exportDir);
                        string payloadFile = BlueprintManager.BuildPlayPayloadFile();
                        string payloadTarget = Path.Combine(exportDir, "play_payload.json");
                        if (!string.IsNullOrEmpty(payloadFile) && File.Exists(payloadFile))
                            File.Copy(payloadFile, payloadTarget, true);
                        string steam = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steam_api64.dll");
                        if (File.Exists(steam))
                            File.Copy(steam, Path.Combine(exportDir, "steam_api64.dll"), true);
                        File.WriteAllText(Path.Combine(exportDir, "steam_appid.txt"), ReadProjectSteamAppId(projectPath));
                        lastDir = exportDir;
                        Console.WriteLine("[Export] Ready " + Path.Combine(exportDir, "Game.exe"));
                    }
                    if (string.IsNullOrEmpty(lastDir) || !File.Exists(Path.Combine(lastDir, "Game.exe")))
                    {
                        Console.WriteLine("[Export ERROR] No Game.exe produced.");
                        return;
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c start \"Game\" /D \"" + lastDir + "\" \"" + Path.Combine(lastDir, "Game.exe") + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    Console.WriteLine("[Export SUCCESS] " + Path.Combine(lastDir, "Game.exe") + " (double-click also works)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Export ERROR] " + ex.Message);
                }
            });
        }
        public static void BuildScripts(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            string projectPath = ProjectSettings.Current.ActiveProject ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\CastleBuilder\\Projects\\Current";
            eventBus.Publish(new GenericEvent { Hook = "ScriptsInfrastructure" });
            ScriptLoader.BuildProjectScripts(projectPath);
            Console.WriteLine("[MenuCommands.BuildScripts] Full IDE-only build pipeline executed - .cs compiled to DLL, registered, ready for Play/Export");
        }
        public static void OpenScriptsPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            ScriptEditorPanel.Open(renderContext, controlContext, window, eventBus);
            Console.WriteLine("[MenuCommands] Script Editor Panel opened");
        }
        public static void Undo(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EditorHistory.Current.Initialize(eventBus);
            EditorHistory.Current.Undo();
        }
        public static void Redo(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EditorHistory.Current.Initialize(eventBus);
            EditorHistory.Current.Redo();
        }
        public static void DeleteSelected(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EditorHistory.Current.Initialize(eventBus);
            EditorHistory.RequestDeleteSelection();
        }
        public static void DeleteScene(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            EditorHistory.Current.Initialize(eventBus);
            var editor = EditorScene.Current;
            if (editor == null) return;
            string name = editor.CurrentGameScene;
            if (string.IsNullOrEmpty(name)) return;
            var project = editor.GetProjectData();
            if (project?.Scenes == null || !project.Scenes.ContainsKey(name)) return;
            editor.FlushActiveSceneData();
            if (!project.Scenes.TryGetValue(name, out var live)) return;
            var snapshot = EditorScene.CloneSceneData(live);
            EditorHistory.Current.Execute(new DelegateCommand(
                "Delete scene",
                () => editor.DeleteScene(name),
                () => editor.RestoreScene(name, snapshot, name)));
        }
    }
}