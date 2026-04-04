// Folder: CastleBuilder
// File: MenuCommands.cs
using CastleBuilder.Events;
using MapRoom;
using ReadingChamber;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using System.IO;
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
            eventBus.Publish(new ContextChangedEvent { Context = "Scene Editor" });
            Console.WriteLine("[MenuCommands] Switched to Scene Editor context");
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
    }
}