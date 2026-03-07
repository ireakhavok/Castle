// Folder: CastleBuilder
// File: SaveProjectPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.IO;
using System.Numerics;

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
                    string name = nameElem?.Value ?? "NewProject";
                    string folder = folderElem?.Value ?? @"C:\Users\ireak\source\CastleBuilder\Projects";
                    BlueprintManager.SaveProjectAs(folder, name, _eventBus);
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                }
                else if (hook == "Cancel")
                {
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                }
            }
        }

        public SaveProjectPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            IsModal = true;
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
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
            _uiOverlay.RefreshUI();
        }
    }
}