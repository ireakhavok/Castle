// Folder: CastleBuilder
// File: CompanionLayoutHelper.cs
using Keystone;
using MapRoom;
using ReadingChamber;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using ToolChest;

namespace CastleBuilder
{
    public static class CompanionLayoutHelper
    {
        private static IRenderContext _renderContext;
        private static IControlContext _controlContext;
        private static nint _window;
        private static EventBus _eventBus;
        private static bool _bound;

        public static string CurrentContext { get; private set; } = "Scene Editor";

        public static void Bind(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _bound = renderContext != null && controlContext != null && eventBus != null;
            ProjectLayoutManager.OpenDefaultCompanions = OpenDefaultLayout;
        }

        public static void EnsureLayout(string context)
        {
            if (!_bound || string.IsNullOrEmpty(context)) return;
            CurrentContext = context;
            ProjectLayoutManager.EnsureLayoutForContext(context);
        }

        public static void OpenDefaultPanels()
        {
            if (!_bound) return;
            OpenDefaultLayout(CurrentContext ?? "Scene Editor");
            ProjectLayoutManager.SaveCurrentLayout(CurrentContext ?? "Scene Editor");
        }

        public static void OpenDefaultLayout(string context)
        {
            if (!_bound || string.IsNullOrEmpty(context)) return;
            CurrentContext = context;
            CloseCompanionTypes(CompanionTypeNames(context));

            IPanel[] left = Array.Empty<IPanel>();
            IPanel[] center = Array.Empty<IPanel>();
            IPanel[] right = Array.Empty<IPanel>();
            IPanel[] bottom = Array.Empty<IPanel>();

            if (string.Equals(context, "Terrain", StringComparison.OrdinalIgnoreCase))
            {
                var terrain = OpenDocked(new TerrainCreatorPanel(_renderContext, _controlContext, _window, _eventBus, (string)null));
                var hierarchy = OpenDocked(new TreeViewPanel(_renderContext, _controlContext, _window, _eventBus));
                var properties = OpenDocked(new PropertiesPanel(_renderContext, _controlContext, _window, _eventBus));
                left = new IPanel[] { hierarchy };
                center = new IPanel[] { terrain };
                right = new IPanel[] { properties };
            }
            else if (string.Equals(context, "Animator", StringComparison.OrdinalIgnoreCase))
            {
                var viewer = OpenDocked(new AnimationViewerPanel(_renderContext, _controlContext, _window, _eventBus));
                var hierarchy = OpenDocked(new TreeViewPanel(_renderContext, _controlContext, _window, _eventBus));
                var properties = OpenDocked(new PropertiesPanel(_renderContext, _controlContext, _window, _eventBus));
                var timeline = OpenDocked(new AnimationTimelinePanel(_renderContext, _controlContext, _window, _eventBus));
                var blend = OpenDocked(new AnimationBlendPanel(_renderContext, _controlContext, _window, _eventBus));
                left = new IPanel[] { hierarchy };
                center = new IPanel[] { viewer };
                right = new IPanel[] { properties };
                bottom = new IPanel[] { timeline, blend };
            }
            else if (string.Equals(context, "Scene Editor", StringComparison.OrdinalIgnoreCase))
            {
                var scene = OpenDocked(new SceneEditorPanel(_renderContext, _controlContext, _window, _eventBus));
                var hierarchy = OpenDocked(new TreeViewPanel(_renderContext, _controlContext, _window, _eventBus));
                var properties = OpenDocked(new PropertiesPanel(_renderContext, _controlContext, _window, _eventBus));
                var assets = OpenDocked(new AssetBrowserPanel(_renderContext, _controlContext, _window, _eventBus));
                var post = OpenDocked(new PostProcessPanel(_renderContext, _controlContext, _window, _eventBus));
                left = new IPanel[] { hierarchy };
                center = new IPanel[] { scene };
                right = new IPanel[] { properties };
                bottom = new IPanel[] { assets, post };
            }
            else if (string.Equals(context, "Configuration", StringComparison.OrdinalIgnoreCase))
            {
                var scripts = OpenDocked(new ScriptEditorPanel(_renderContext, _controlContext, _window, _eventBus));
                var console = OpenDocked(new ConsolePanel(_renderContext, _controlContext, _window, _eventBus));
                var playHost = OpenDocked(new PlayHostPanel(_renderContext, _controlContext, _window, _eventBus));
                center = new IPanel[] { scripts };
                right = new IPanel[] { playHost };
                bottom = new IPanel[] { console };
            }
            else
            {
                return;
            }

            PanelManager.Current?.IDEStrategy?.ApplyCompanionSeed(left, center, right, bottom);
        }

        private static IPanel OpenDocked(IPanel panel)
        {
            if (panel == null) return null;
            panel.DockingMode = DockingMode.IDE;
            panel.DockState = DockState.Tabbed;
            panel.IsModal = false;
            panel.HasTitleBar = true;
            panel.IsClosable = true;
            panel.AllowDragging = true;
            _eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
            return panel;
        }

        private static void CloseCompanionTypes(string[] typeNames)
        {
            var pm = PanelManager.Current;
            if (pm == null || typeNames == null || typeNames.Length == 0) return;
            var toClose = new List<IPanel>();
            foreach (var panel in pm.GetAllPanels())
            {
                if (panel == null) continue;
                string name = panel.GetType().Name;
                for (int i = 0; i < typeNames.Length; i++)
                {
                    if (string.Equals(name, typeNames[i], StringComparison.Ordinal))
                    {
                        toClose.Add(panel);
                        break;
                    }
                }
            }
            for (int i = 0; i < toClose.Count; i++)
                toClose[i].Close();
        }

        private static string[] CompanionTypeNames(string context)
        {
            if (string.Equals(context, "Terrain", StringComparison.OrdinalIgnoreCase))
                return new[] { "TerrainCreatorPanel", "TreeViewPanel", "PropertiesPanel" };
            if (string.Equals(context, "Animator", StringComparison.OrdinalIgnoreCase))
                return new[] { "AnimationViewerPanel", "AnimationTimelinePanel", "AnimationBlendPanel", "TreeViewPanel", "PropertiesPanel" };
            if (string.Equals(context, "Scene Editor", StringComparison.OrdinalIgnoreCase))
                return new[] { "SceneEditorPanel", "TreeViewPanel", "PropertiesPanel", "AssetBrowserPanel", "PostProcessPanel" };
            if (string.Equals(context, "Configuration", StringComparison.OrdinalIgnoreCase))
                return new[] { "ScriptEditorPanel", "ConsolePanel", "PlayHostPanel" };
            return Array.Empty<string>();
        }
    }
}
