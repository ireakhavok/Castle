// Folder: CastleBuilder
// File: MenuCommands.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using MapRoom;
using ReadingChamber;
using ToolChest;

namespace CastleBuilder
{
    public static class MenuCommands
    {
        // Original top-level menu stubs
        public static void OpenFile(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Menu] File opened (stub)");
        }

        public static void OpenEdit(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Menu] Edit opened (stub)");
        }

        public static void OpenView(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Menu] View opened (stub)");
        }

        public static void OpenCastle(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Menu] Castle opened (stub)");
        }

        public static void OpenTools(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Menu] Tools opened (stub)");
        }

        public static void OpenWindow(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Menu] Window opened (stub)");
        }

        public static void OpenHelp(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            Console.WriteLine("[Menu] Help opened (stub)");
        }

        // Panels submenu - all requested panels
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
    }
}