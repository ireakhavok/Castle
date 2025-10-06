// Folder: SiegeEngine.Managers
// File: PanelManager.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Interfaces;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;

namespace SiegeEngine.Managers
{
    public class PanelManager
    {
        private readonly List<IPanel> _panels = new List<IPanel>();
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;

        public PanelManager(IRenderContext renderContext, IControlContext controlContext, IntPtr window)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
        }

        public void AddPanel(IPanel panel)
        {
            panel.Init();
            _panels.Add(panel);
        }

        public void Update(float deltaTime)
        {
            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                _panels[i].Update(deltaTime);
            }
        }

        public void Render()
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                _panels[i].Render();
            }
        }

        public void RemovePanel(IPanel panel)
        {
            panel.Dispose();
            _panels.Remove(panel);
        }
    }
}