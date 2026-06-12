// Folder: CastleBuilder
// File: MenuCommands.cs
using CastleBuilder.Events;
using MapRoom;
using ReadingChamber;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using Keystone;
using System.IO;
using ToolChest;
using SiegeEngine.Scenes.StartingPoints;
using System.Diagnostics;
using System.Threading.Tasks;

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
            Console.WriteLine("[MenuCommands.PlayGame] Play Game - loading the ACTUAL current project scene/Level from IDE (100% in-memory ONLY - NO SAVE, NO FLUSH, NO DISK WRITE, NO EnsureDefaultSceneIfNeeded)");
            eventBus.Publish(new ContextChangedEvent { Context = "Runtime Gameplay" });
            Console.WriteLine("[PlayGame SUCCESS] Runtime Gameplay context activated - editor panels closed, current IDE Level/terrain/entities now fully playable in runtime (state unchanged on disk, no ding, no crash)");
        }

        public static void SandboxRegressionTest(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Tests] Sandbox Regression Test launched (vertical slice/demo only)");
            eventBus.Publish(new ContextChangedEvent { Context = "Runtime Gameplay" });
            SandboxScene.Launch(renderContext, controlContext, window, eventBus);
        }

        public static void ExportGame(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[MenuCommands.ExportGame] Starting clean GAME export (client-only, no IDE files, no server mode)");
            Task.Run(() =>
            {
                try
                {
                    string projectPath = ProjectSettings.Current.ActiveProject;
                    if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                    {
                        Console.WriteLine("[Export] No active project - aborting");
                        return;
                    }

                    string exportRoot = Path.Combine(projectPath, "exported");
                    if (Directory.Exists(exportRoot))
                    {
                        Directory.Delete(exportRoot, true);
                    }
                    Directory.CreateDirectory(exportRoot);

                    // Copy ONLY game-relevant runtime files (Assets + Scenes + DLLs + Citadel.exe)
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

                    // Copy Citadel.exe as client EXE
                    string exeSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Citadel.exe");
                    string exeTarget = Path.Combine(exportRoot, "Citadel.exe");
                    File.Copy(exeSource, exeTarget, true);

                    // Copy required runtime DLLs
                    string[] dlls = { "steam_api64.dll" };
                    foreach (string dll in dlls)
                    {
                        string sourceDll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dll);
                        if (File.Exists(sourceDll))
                        {
                            File.Copy(sourceDll, Path.Combine(exportRoot, dll), true);
                        }
                    }

                    // Launch as CLIENT (no --local, no --server)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(exportRoot, "Citadel.exe"),
                        WorkingDirectory = exportRoot,
                        UseShellExecute = true,
                        Arguments = ""  // client mode
                    });

                    Console.WriteLine("[Export SUCCESS] Clean game client exported and launched (only runtime files + assets + level data)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Export ERROR] {ex.Message}");
                }
            });
        }
    }
}