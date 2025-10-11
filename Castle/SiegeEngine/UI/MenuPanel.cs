// Folder: SiegeEngine.UI
// File: MenuPanel.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using System;

namespace SiegeEngine.UI
{
    public class MenuPanel : BasePanel
    {
        private string _htmlPath;

        public MenuPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus, string htmlPath) : base(renderContext, controlContext, window, eventBus)
        {
            _htmlPath = htmlPath;
        }

        public override void Init()
        {
            base.Init();
            //LoadUI(_htmlPath); //needs to be implemented
        }
    }
}