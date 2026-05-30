// Folder: CastleBuilder
// File: NewProjectPanel.cs
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

namespace CastleBuilder
{
    public class NewProjectPanel : BasePanel
    {
        private class NewProjectUIOverlay : UIOverlay
        {
            private readonly NewProjectPanel _parent;
            private readonly EventBus _eventBus;

            public NewProjectUIOverlay(NewProjectPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
                _eventBus = eventBus;
            }

            protected override void HandleDataHook(string hook)
            {
                if (hook == "CreateNewProjectConfirm")
                {
                    // Read form values exactly like the original BlueprintManager.CreateNewProject did
                    var nameElem = FindElementById("project-name") as InputElement;
                    var typeElem = FindElementById("game-type") as SelectElement;
                    var modeElem = FindElementById("project-mode") as SelectElement;
                    var allowModsElem = FindElementById("allow-mods") as InputElement;

                    string name = nameElem?.Value?.Trim() ?? "MyNewProject";
                    if (string.IsNullOrEmpty(name)) name = "MyNewProject";

                    string projectType = typeElem?.Value ?? "3D FPS";
                    string mode = modeElem?.Value ?? "Single Player";
                    bool allowMods = allowModsElem?.Checked ?? true;

                    // Call the exact same method that already works (we pass 'this' as the overlay so it can read the fields)
                    BlueprintManager.CreateNewProject(_renderContext, _controlContext, _window, _eventBus, this);

                    _eventBus.Publish(new ClosePanelEvent(_parent));
                    return;
                }

                if (hook == "Cancel")
                {
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                }
            }
        }

        public NewProjectPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            IsModal = true;
            RenderOrder = 1100;
            Scaling = ScalingMode.Fill;
            Size = new Vector2(420, 460);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new NewProjectUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);
        }

        public override void Init()
        {
            base.Init();

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Configs", "NewProject.html");
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
            var panel = new NewProjectPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}