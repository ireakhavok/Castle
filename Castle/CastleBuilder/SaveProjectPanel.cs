// Folder: CastleBuilder
// File: SaveProjectPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Core.Managers;
using System;
using System.IO;
using System.Numerics;
using ToolChest;


namespace CastleBuilder
{
    public class SaveProjectPanel : BasePanel
    {
        private class SaveUIOverlay : UIOverlay
        {
            private readonly SaveProjectPanel _parent;
            private readonly EventBus _eventBus;

            public SaveUIOverlay(SaveProjectPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
                _eventBus = eventBus;
            }

            protected override void HandleDataHook(string hook)
            {
                if (hook == "SaveProjectConfirm")
                {
                    var nameElem = FindElementById("projectName") as InputElement;
                    var folderElem = FindElementById("projectFolder") as InputElement;

                    string name = nameElem?.Value?.Trim() ?? "UntitledProject";
                    if (string.IsNullOrEmpty(name)) name = "UntitledProject";

                    string folder = folderElem?.Value?.Trim();
                    if (string.IsNullOrEmpty(folder) || folder.Contains("ireak"))
                        folder = ProjectSettings.Current.ProjectsRoot;

                    BlueprintManager.SaveProjectAs(folder, name, _eventBus);
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                    return;
                }

                if (hook == "Cancel")
                {
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                }
            }
        }

        public SaveProjectPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            IsModal = true;
            RenderOrder = 1100;
            Scaling = ScalingMode.Fill;
            Size = new Vector2(420, 180);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new SaveUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);
        }

        public override void Init()
        {
            base.Init();

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SaveProjectForm.html");
            if (File.Exists(htmlPath))
            {
                string html = File.ReadAllText(htmlPath);
                _uiOverlay.LoadUI(html);
            }

            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new SaveProjectPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}