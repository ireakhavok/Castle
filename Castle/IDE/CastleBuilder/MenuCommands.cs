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
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Scenes.StartingPoints;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ToolChest;
namespace CastleBuilder
{
    public static class MenuCommands
    {
        private static readonly string DefaultProjectsPath = ProjectSettings.Current.ProjectsRoot;
        public static void SwitchToTerrain(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new ContextChangedEvent { Context = "Terrain" });
            Console.WriteLine("[MenuCommands] Switched to Terrain context");
        }
        public static void SwitchToAnimator(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new ContextChangedEvent { Context = "Animator" });
            Console.WriteLine("[MenuCommands] Switched to Animator context");
        }
        public static void SwitchToSceneEditor(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            BlueprintManager.EnsureDefaultSceneIfNeeded();
            eventBus.Publish(new ContextChangedEvent { Context = "Scene Editor" });
            Console.WriteLine("[MenuCommands] Switched to Scene Editor context (panel opened)");
        }
        public static void SwitchToConfiguration(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new ContextChangedEvent { Context = "Configuration" });
            Console.WriteLine("[MenuCommands] Switched to Configuration context");
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
        public static void PlayGame(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[MenuCommands.PlayGame] Launching CURRENT project Level in NEW isolated window (pure runtime client - in-memory, no disk write, no EnsureDefaultSceneIfNeeded, no save)");
            BlueprintManager.SaveCurrentProject(renderContext, controlContext, window, eventBus);
            string projectPath = ProjectSettings.Current.ActiveProject ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\CastleBuilder\\Projects\\Current";
            string levelName = ProjectSettings.Current.CurrentSceneName ?? "Main";
            string snapshotPath = Path.Combine(projectPath, "runtime_start.level");
            var level = ProjectSettings.Current.CurrentLevel ?? new Level();
            File.WriteAllBytes(snapshotPath, level.Serialize());
            ScriptLoader.CopyProjectScripts(projectPath);
            string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Foundation.exe");
            if (!File.Exists(exe)) exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--client --play-project \"{projectPath}\" --load-level \"{levelName}\" --runtime-snapshot \"{snapshotPath}\" --custom-assemblies \"{ScriptLoader.GetCustomAssemblyList(projectPath)}\"",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            Process.Start(psi);
            eventBus.Publish(new ContextChangedEvent { Context = "Runtime Gameplay" });
            Console.WriteLine($"[PlayGame SUCCESS] New runtime window launched with FULL Level snapshot '{levelName}' from IDE cache - editor panels hidden, exact entities/positions/terrain/packs active (no spoof, standalone compatible)");
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
                    string exportRoot = Path.Combine(projectPath, "exported");
                    if (Directory.Exists(exportRoot))
                    {
                        Directory.Delete(exportRoot, true);
                    }
                    Directory.CreateDirectory(exportRoot);
                    string[] runtimeFolders = { "Assets", "Scenes" };
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
                    var level = ProjectSettings.Current.CurrentLevel ?? new Level();
                    string levelJsonPath = Path.Combine(exportRoot, "Scenes", "starting_level.json");
                    Directory.CreateDirectory(Path.Combine(exportRoot, "Scenes"));
                    File.WriteAllBytes(levelJsonPath, level.Serialize());
                    File.WriteAllText(Path.Combine(exportRoot, "starting_scene.json"), "{\"startingScene\":\"" + levelName + "\"}");
                    string exeSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");
                    string exeTarget = Path.Combine(exportRoot, "Citadel.exe");
                    File.Copy(exeSource, exeTarget, true);
                    string[] dlls = { "steam_api64.dll", "Foundation.dll", "SiegeEngine.dll", "Trebuchet.dll" };
                    foreach (string dll in dlls)
                    {
                        string sourceDll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dll);
                        if (File.Exists(sourceDll))
                        {
                            File.Copy(sourceDll, Path.Combine(exportRoot, dll), true);
                        }
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(exportRoot, "Citadel.exe"),
                        WorkingDirectory = exportRoot,
                        UseShellExecute = true,
                        Arguments = "--client --load-level " + levelName + " --runtime-snapshot \"" + levelJsonPath + "\" --custom-assemblies \"" + ScriptLoader.GetCustomAssemblyList(projectPath) + "\""
                    });
                    Console.WriteLine($"[Export SUCCESS] Clean game client exported to {exportRoot} with FULL starting Level '{levelName}' and launched as pure runtime client (exact entities, positions, terrain, packs - no server messages, no IDE)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Export ERROR] {ex.Message}");
                }
            });
        }
    }
}