// Folder: SiegeEngine/Core/Managers
// File: PanelInputRouter.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Managers
{
    /// <summary>
    /// SINGLE SOURCE OF TRUTH for panel input routing, z-order, hit-testing, and forced overdraw.
    /// All strategies, BasePanel, IDEBasePanel, NavLiElement, etc. now delegate here.
    /// Guarantees:
    /// - Each panel's Update() is called exactly once per frame.
    /// - Layout recalculation happens only when needed (via RefreshUI or size change).
    /// - Dropdowns and any "forced overdraw" elements are supported cleanly.
    /// - No collection-modified-during-enumeration crashes (uses snapshot for UpdateAll).
    /// </summary>
    public class PanelInputRouter
    {
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly EventBus _eventBus;
        private readonly nint _window;

        // All live panels in strict z-order (back to front)
        private readonly List<IPanel> _allPanels = new List<IPanel>();

        // Panels that want to be force-drawn over everything for one frame (dropdowns, tooltips, ghosts)
        private readonly HashSet<IPanel> _forcedOverdrawPanels = new HashSet<IPanel>();

        // Last frame's topmost panel (for OnContentFocusGained detection)
        private IPanel _lastTopmost;

        public PanelInputRouter(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
        }

        public void AddPanel(IPanel panel)
        {
            if (!_allPanels.Contains(panel))
            {
                _allPanels.Add(panel);
                // Higher RenderOrder floats to front; stable sort for same order
                _allPanels.Sort((a, b) =>
                {
                    int orderA = (a as BasePanel)?.RenderOrder ?? 0;
                    int orderB = (b as BasePanel)?.RenderOrder ?? 0;
                    if (orderA != orderB) return orderB.CompareTo(orderA);
                    return _allPanels.IndexOf(a).CompareTo(_allPanels.IndexOf(b));
                });
            }
        }

        public void RemovePanel(IPanel panel)
        {
            _allPanels.Remove(panel);
            _forcedOverdrawPanels.Remove(panel);
            if (_lastTopmost == panel) _lastTopmost = null;
        }

        /// <summary>
        /// Definitive topmost panel for ANY mouse position.
        /// Modals → forced-overdraw → RenderOrder descending → floating → docked.
        /// </summary>
        public IPanel GetTopmostPanelAt(Vector2 mousePos)
        {
            // Modals always win
            foreach (var p in _allPanels)
            {
                if (p is BasePanel bp && bp.IsModal && bp.Visible && bp.IsMouseOver(mousePos))
                    return p;
            }

            // Forced overdraw (dropdowns, popups, ghosts) win next
            foreach (var p in _forcedOverdrawPanels)
            {
                if (p.Visible && p.IsMouseOver(mousePos))
                    return p;
            }

            // Normal panels in current z-order
            for (int i = _allPanels.Count - 1; i >= 0; i--)
            {
                var p = _allPanels[i];
                if (p.Visible && p.IsMouseOver(mousePos))
                    return p;
            }

            return null;
        }

        /// <summary>
        /// Single place that calls Update on every panel exactly once per frame.
        /// Uses snapshot to prevent "Collection was modified" crashes when
        /// data-hooks / ClosePanelEvent / OpenPanelEvent modify the list mid-frame.
        /// </summary>
        public void UpdateAll(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta)
        {
            // === CRITICAL: snapshot prevents modification-during-enumeration ===
            IPanel[] snapshot = _allPanels.ToArray();

            IPanel currentTopmost = GetTopmostPanelAt(mousePos);

            // Focus change detection (exactly once)
            if (currentTopmost != _lastTopmost && currentTopmost != null && mousePressed)
            {
                if (currentTopmost is BasePanel bp)
                    bp.OnContentFocusGained();
            }
            _lastTopmost = currentTopmost;

            // Update every panel exactly once (using stable snapshot)
            foreach (var panel in snapshot)
            {
                if (!panel.Visible) continue;

                // Let the panel know it is (or is not) topmost this frame
                bool isTopmost = panel == currentTopmost;

                panel.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            }

            // Clear forced overdraw after the frame (they re-register if still needed)
            _forcedOverdrawPanels.Clear();
        }

        /// <summary>
        /// Single render pass. Forced-overdraw panels are drawn LAST (on top).
        /// </summary>
        public void RenderAll(IRenderContext renderContext, int winW, int winH)
        {
            renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            renderContext.Viewport(0, 0, (uint)winW, (uint)winH);

            // Normal panels first (in current z-order)
            foreach (var panel in _allPanels)
            {
                if (panel.Visible)
                {
                    panel.Render();
                }
            }

            // Forced overdraw on top
            foreach (var panel in _forcedOverdrawPanels)
            {
                if (panel.Visible)
                {
                    panel.Render();
                }
            }
        }

        /// <summary>
        /// Any panel can call this to force itself (or its dropdown) to be drawn above everything else this frame.
        /// Used by NavLiElement dropdowns, ghost previews, etc.
        /// </summary>
        public void ForceDrawOverThisFrame(IPanel panel)
        {
            if (panel != null && panel.Visible)
                _forcedOverdrawPanels.Add(panel);
        }

        /// <summary>
        /// Called by BasePanel when size or content changes – guarantees layout is recomputed only when needed.
        /// </summary>
        public void RequestLayoutRefresh(IPanel panel)
        {
            if (panel is BasePanel bp)
            {
                bp.OnPanelResize(bp.Size.X, bp.Size.Y);
            }
        }
    }
}