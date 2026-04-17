// Folder: SiegeEngine/Core/Managers
// File: PanelInputRouter.cs
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Linq;
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
            // Modals first (unchanged - always absolute top)
            for (int i = _allPanels.Count - 1; i >= 0; i--)
            {
                var p = _allPanels[i];
                if (p is BasePanel bp && bp.IsModal && bp.Visible && bp.IsMouseOver(mousePos))
                    return p;
            }

            // Forced overdraw (dropdowns, popups, etc.) - these must always be checked
            foreach (var p in _forcedOverdrawThisFrame)
            {
                if (p.Visible && p.IsMouseOver(mousePos))
                    return p;
            }

            // === CRITICAL FIX: Floating panels (including IDE floating panels) must ALWAYS win ===
            // They are rendered LAST in IDEDockingStrategy.Render (after docked root), so they are visually on top.
            // We therefore check them FIRST in hit-testing, in reverse order of addition (last added = topmost).
            for (int i = _allPanels.Count - 1; i >= 0; i--)
            {
                var p = _allPanels[i];
                if (p is BasePanel bp && bp.DockState == DockState.Floating && p.Visible && bp.IsMouseOver(mousePos))
                    return p;
            }

            // NORMAL DOCKED PANELS (including IDE docked content)
            // When RenderOrder is the same (most panels are 0), the panel added last wins (last drawn = top)
            var sortedPanels = _allPanels
                .Where(p => p.Visible && !(p is BasePanel bp && (bp.IsModal || bp.DockState == DockState.Floating)))
                .OrderByDescending(p => (p as BasePanel)?.RenderOrder ?? 0)
                .ThenByDescending(p => _allPanels.IndexOf(p))
                .ToList();

            foreach (var p in sortedPanels)
            {
                if (p.IsMouseOver(mousePos))
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