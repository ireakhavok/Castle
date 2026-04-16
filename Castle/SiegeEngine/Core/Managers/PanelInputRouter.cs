// Folder: SiegeEngine/Core/Managers
// File: PanelInputRouter.cs
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Managers
{
    public class PanelInputRouter
    {
        private readonly List<IPanel> _allPanels = new List<IPanel>();
        private readonly HashSet<IPanel> _forcedOverdrawThisFrame = new HashSet<IPanel>();

        public void AddPanel(IPanel panel)
        {
            if (!_allPanels.Contains(panel))
                _allPanels.Add(panel);
        }

        public void RemovePanel(IPanel panel)
        {
            _allPanels.Remove(panel);
            _forcedOverdrawThisFrame.Remove(panel);
        }

        public IPanel GetTopmostPanelAt(Vector2 mousePos)
        {
            // Modals first
            for (int i = _allPanels.Count - 1; i >= 0; i--)
            {
                var p = _allPanels[i];
                if (p is BasePanel bp && bp.IsModal && bp.Visible && bp.IsMouseOver(mousePos))
                    return p;
            }

            // Forced overdraw (dropdowns, popups)
            foreach (var p in _forcedOverdrawThisFrame)
            {
                if (p.Visible && p.IsMouseOver(mousePos))
                    return p;
            }

            // Normal panels
            for (int i = _allPanels.Count - 1; i >= 0; i--)
            {
                var p = _allPanels[i];
                if (p.Visible && p.IsMouseOver(mousePos))
                    return p;
            }
            return null;
        }

        public void ForceDrawOverThisFrame(IPanel panel)
        {
            if (panel != null && panel.Visible)
                _forcedOverdrawThisFrame.Add(panel);
        }

        public void ClearForcedOverdraw()
        {
            _forcedOverdrawThisFrame.Clear();
        }
    }
}