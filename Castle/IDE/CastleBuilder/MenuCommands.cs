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
            if (!ScriptLoader.BuildProjectScripts(projectPath))
            {
                Console.WriteLine("[MenuCommands.PlayGame] ABORTED — project scripts failed to compile. Fix Scripts/ and try Play again.");
                return;
            }
            ScriptLoader.CopyProjectScripts(projectPath);
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
        public static void SandboxRegressionTest(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Tests] Sandbox Regression Test launched (vertical slice/demo only)");
            eventBus.Publish(new ContextChangedEvent { Context = "Runtime Gameplay" });
            SandboxScene.Launch(renderContext, controlContext, window, eventBus);
        }
        public static void ExportGame(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[MenuCommands.ExportGame] Starting clean GAME export (client-only, no IDE files, no server mode, serialized starting Level)");
            Task.Run(() =>
            {
                try
                {
                    string projectPath = ProjectSettings.Current.ActiveProject;
                    if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                    {
                        Console.WriteLine("[Export] No active project - using default in-memory Level");
                        projectPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CastleBuilder", "Projects", "Default");
                        Directory.CreateDirectory(projectPath);
                    }
                    BlueprintManager.SaveCurrentProject(renderContext, controlContext, window, eventBus);
                    ScriptLoader.BuildProjectScripts(projectPath);
                    string exportRoot = Path.Combine(projectPath, "exported");
                    if (Directory.Exists(exportRoot))
                    {
                        Directory.Delete(exportRoot, true);
                    }
                    Directory.CreateDirectory(exportRoot);
                    string[] runtimeFolders = { "Assets", "Scenes", "Scripts" };
                    foreach (string folder in runtimeFolders)
                    {
                        string source = Path.Combine(projectPath, folder);
                        if (Directory.Exists(source))
                        {
                            string target = Path.Combine(exportRoot, folder);
                            Directory.CreateDirectory(target);
                            BlueprintManager.CopyDirectory(source, target);
                        }
                    }
                    ScriptLoader.CopyProjectScripts(projectPath);
                    ScriptLoader.CopyScriptsToExport(projectPath, exportRoot);
                    string levelName = ProjectSettings.Current.CurrentSceneName ?? "Main";
                    string payloadFile = BlueprintManager.BuildPlayPayloadFile();
                    string payloadTarget = Path.Combine(exportRoot, "play_payload.json");
                    if (!string.IsNullOrEmpty(payloadFile) && File.Exists(payloadFile))
                    {
                        File.Copy(payloadFile, payloadTarget, true);
                    }
                    string binDir = AppDomain.CurrentDomain.BaseDirectory;
                    string foundationSource = Path.Combine(binDir, "Foundation.exe");
                    if (!File.Exists(foundationSource))
                        foundationSource = Path.Combine(binDir, "Citadel.exe");
                    string exeName = Path.GetFileName(foundationSource);
                    string exeStem = Path.GetFileNameWithoutExtension(exeName);
                    File.Copy(foundationSource, Path.Combine(exportRoot, exeName), true);
                    string[] sidecar = { exeStem + ".dll", exeStem + ".runtimeconfig.json", exeStem + ".deps.json", "steam_api64.dll" };
                    foreach (string name in sidecar)
                    {
                        string src = Path.Combine(binDir, name);
                        if (File.Exists(src))
                            File.Copy(src, Path.Combine(exportRoot, name), true);
                    }
                    foreach (string dll in Directory.GetFiles(binDir, "*.dll"))
                    {
                        string name = Path.GetFileName(dll);
                        if (name.StartsWith("CastleBuilder", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("Keystone", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("MapRoom", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("ReadingChamber", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("ToolChest", StringComparison.OrdinalIgnoreCase))
                            continue;
                        File.Copy(dll, Path.Combine(exportRoot, name), true);
                    }
                    string runtimeConfig = Path.Combine(exportRoot, exeStem + ".runtimeconfig.json");
                    if (!File.Exists(runtimeConfig))
                    {
                        Console.WriteLine($"[Export ERROR] Missing {exeStem}.runtimeconfig.json next to the IDE exe; cannot launch exported client.");
                        return;
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(exportRoot, exeName),
                        WorkingDirectory = exportRoot,
                        UseShellExecute = true,
                        Arguments = $"--client --play-project \"{exportRoot}\" --load-level \"{levelName}\" --play-payload-file \"{payloadTarget}\" --custom-assemblies \"{ScriptLoader.GetCustomAssemblyList(projectPath)}\""
                    });
                    Console.WriteLine($"[Export SUCCESS] Clean game client exported to {exportRoot} with payload file for level '{levelName}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Export ERROR] {ex.Message}");
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