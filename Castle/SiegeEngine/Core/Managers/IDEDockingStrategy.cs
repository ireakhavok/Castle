// Folder: SiegeEngine/Core/Managers
// File: IDEDockingStrategy.cs
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
    public class IDEDockingStrategy : IDockingStrategy
    {
        private DockNode _root;
        private List<IPanel> _floatingPanels = new List<IPanel>();
        private IPanel _draggingPanel;
        private Vector2 _dragOffset;
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly EventBus _eventBus;
        private readonly UIQuadRenderer _quadRenderer;

        private IPanel _hoveredPanelDuringDrag;
        private bool _showHoverIcons;
        private Vector2 _hoverIconCenter;
        private bool _hoveringWorkspace;

        private const float IconSize = 80f;
        private const float MenuBarHeight = 28f;

        public IDEDockingStrategy(IRenderContext renderContext, IControlContext controlContext, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _eventBus = eventBus;
            _quadRenderer = new UIQuadRenderer(renderContext);
            _root = new DockTabbedNode();
        }

        public void AddPanel(IPanel panel)
        {
            panel.HasTitleBar = true;
            panel.IsClosable = true;
            panel.HeaderHeight = BasePanel.TitleHeight;
            panel.DockingMode = DockingMode.IDE;

            if (panel.DockState == DockState.Floating || panel.DockState == DockState.DockedHeader)
            {
                _floatingPanels.Add(panel);
                panel.AllowDragging = true;
                panel.DockState = DockState.Floating;

                panel.Position = new Vector2(120f, MenuBarHeight + 40f);
                panel.Size = new Vector2(600f, 400f);
                panel.OnPanelResize(400f, 300f);
            }
            else
            {
                _root.AddPanel(panel);
                panel.AllowDragging = false;
            }
        }

        public void RemovePanel(IPanel panel)
        {
            _floatingPanels.Remove(panel);
            if (_draggingPanel == panel) _draggingPanel = null;
            _root.RemovePanel(panel);
        }

        public void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus, int winW, int winH)
        {
            // FLOATING PANELS - EXACT SAME LIFECYCLE PATTERN AS DYNAMICDOCKINGSTRATEGY (the one that works)
            // Only the hovered panel receives Update(). No other panel ever gets lifecycle.
            // This eliminates any possibility of panels moving together.
            // _root.Update is intentionally removed (as you identified - this was the source of scroll blending).

            IPanel hoveredPanel = null;
            for (int i = _floatingPanels.Count - 1; i >= 0; i--)
            {
                var panel = _floatingPanels[i];
                if (!panel.Visible) continue;

                bool overPanel = mousePos.X >= panel.Position.X && mousePos.X <= panel.Position.X + panel.Size.X &&
                                 mousePos.Y >= panel.Position.Y && mousePos.Y <= panel.Position.Y + panel.Size.Y;

                if (overPanel)
                {
                    hoveredPanel = panel;
                    panel.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);

                    if (HandleSinglePanel(panel, mousePos, mousePressed, winW, winH))
                    {
                        break;
                    }
                }
            }

            // DOCKED PANEL TEAR-OUT (IDE specific)
            if (mousePressed && _draggingPanel == null && hoveredPanel == null)
            {
                if (_root.HitTest(mousePos, out IPanel hit, out bool isTitle, out _, out _, out _))
                {
                    if (isTitle && hit.AllowDragging)
                    {
                        _root.RemovePanel(hit);
                        _floatingPanels.Add(hit);
                        hit.DockState = DockState.Floating;
                        hit.AllowDragging = true;

                        _draggingPanel = hit;
                        _dragOffset = mousePos - hit.Position;
                        if (hit is BasePanel bp)
                        {
                            bp.StartTitleBarDrag(mousePos);
                        }
                        return;
                    }
                }
            }

            if (_draggingPanel != null && mouseDown)
            {
                _draggingPanel.Position = mousePos - _dragOffset;
                if (_draggingPanel.Position.Y < MenuBarHeight)
                    _draggingPanel.Position = new Vector2(_draggingPanel.Position.X, MenuBarHeight);

                DetectHoverTarget(mousePos, winW, winH);
            }

            if (_draggingPanel != null && mouseReleased)
            {
                if (_draggingPanel is BasePanel bp) bp.ResetDragState();
                _draggingPanel = null;
                _hoveredPanelDuringDrag = null;
                _showHoverIcons = false;
                _hoveringWorkspace = false;
            }
        }

        private bool HandleSinglePanel(IPanel panel, Vector2 mousePos, bool mousePressed, int winW, int winH)
        {
            bool overPanel = mousePos.X >= panel.Position.X && mousePos.X <= panel.Position.X + panel.Size.X &&
                             mousePos.Y >= panel.Position.Y && mousePos.Y <= panel.Position.Y + panel.Size.Y;
            if (!overPanel) return false;

            if (mousePressed && panel.HasTitleBar && panel.AllowDragging && _draggingPanel == null)
            {
                bool overTitle = mousePos.Y >= panel.Position.Y && mousePos.Y <= panel.Position.Y + BasePanel.TitleHeight;
                if (overTitle)
                {
                    _draggingPanel = panel;
                    _dragOffset = mousePos - panel.Position;
                    if (panel is BasePanel bp)
                    {
                        bp.StartTitleBarDrag(mousePos);
                    }
                    // BringToFront - exact same as DynamicDockingStrategy
                    int idx = _floatingPanels.IndexOf(panel);
                    if (idx >= 0 && idx < _floatingPanels.Count - 1)
                    {
                        _floatingPanels.RemoveAt(idx);
                        _floatingPanels.Add(panel);
                    }
                    return true;
                }
            }
            return false;
        }

        private void DetectHoverTarget(Vector2 mousePos, int winW, int winH)
        {
            _hoveredPanelDuringDrag = null;
            _showHoverIcons = false;
            _hoveringWorkspace = false;

            for (int i = _floatingPanels.Count - 1; i >= 0; i--)
            {
                var p = _floatingPanels[i];
                if (p == _draggingPanel || !p.Visible) continue;
                if (mousePos.X >= p.Position.X && mousePos.X <= p.Position.X + p.Size.X &&
                    mousePos.Y >= p.Position.Y && mousePos.Y <= p.Position.Y + p.Size.Y)
                {
                    _hoveredPanelDuringDrag = p;
                    _showHoverIcons = true;
                    _hoverIconCenter = new Vector2(p.Position.X + p.Size.X * 0.5f, p.Position.Y + p.Size.Y * 0.5f);
                    return;
                }
            }

            if (_root.HitTest(mousePos, out IPanel dockedHit, out _, out _, out _, out _))
            {
                if (dockedHit != _draggingPanel)
                {
                    _hoveredPanelDuringDrag = dockedHit;
                    _showHoverIcons = true;
                    _hoverIconCenter = new Vector2(dockedHit.Position.X + dockedHit.Size.X * 0.5f, dockedHit.Position.Y + dockedHit.Size.Y * 0.5f);
                    return;
                }
            }

            if (mousePos.Y > MenuBarHeight)
            {
                _hoveringWorkspace = true;
                _showHoverIcons = true;
                _hoverIconCenter = new Vector2(winW * 0.5f, (winH + MenuBarHeight) * 0.5f);
            }
        }

        public void Render(IRenderContext renderContext, int winW, int winH)
        {
            _root.Render(renderContext, winW, winH);

            foreach (var panel in _floatingPanels)
            {
                if (!panel.Visible) continue;
                int px = (int)panel.Position.X;
                int py = winH - (int)(panel.Position.Y + panel.Size.Y);
                uint pw = (uint)panel.Size.X;
                uint ph = (uint)panel.Size.Y;
                renderContext.Scissor(px, py, pw, ph);
                renderContext.Viewport(px, py, pw, ph);
                panel.Render();
            }

            renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            renderContext.Viewport(0, 0, (uint)winW, (uint)winH);

            if (_showHoverIcons)
            {
                float cx = _hoverIconCenter.X;
                float cy = _hoverIconCenter.Y;
                float s = IconSize;

                float cs = s * 0.3f;
                _quadRenderer.DrawQuad(cx - cs * 0.5f, cy - cs * 0.5f, cs, cs, new Vector4(0.9f, 0.9f, 1f, 1f), winW, winH);
                cs = s * 0.8f;

                float shaftLen = s * 0.4f;
                float thickness = 2f;
                Vector4 ac = new Vector4(1f, 1f, 1f, 1f);

                // North
                _quadRenderer.DrawLine(cx, cy - cs * 0.5f - shaftLen, cx, cy - cs * 0.5f, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - 18, cy - cs * 0.5f - shaftLen + 28, cx, cy - cs * 0.5f - shaftLen, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + 18, cy - cs * 0.5f - shaftLen + 28, cx, cy - cs * 0.5f - shaftLen, thickness, ac, winW, winH);

                // South
                _quadRenderer.DrawLine(cx, cy + cs * 0.5f + shaftLen, cx, cy + cs * 0.5f, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - 18, cy + cs * 0.5f + shaftLen - 28, cx, cy + cs * 0.5f + shaftLen, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + 18, cy + cs * 0.5f + shaftLen - 28, cx, cy + cs * 0.5f + shaftLen, thickness, ac, winW, winH);

                // West
                _quadRenderer.DrawLine(cx - cs * 0.5f - shaftLen, cy, cx - cs * 0.5f, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - cs * 0.5f - shaftLen + 28, cy - 18, cx - cs * 0.5f - shaftLen, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - cs * 0.5f - shaftLen + 28, cy + 18, cx - cs * 0.5f - shaftLen, cy, thickness, ac, winW, winH);

                // East
                _quadRenderer.DrawLine(cx + cs * 0.5f + shaftLen, cy, cx + cs * 0.5f, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + cs * 0.5f + shaftLen - 28, cy - 18, cx + cs * 0.5f + shaftLen, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + cs * 0.5f + shaftLen - 28, cy + 18, cx + cs * 0.5f + shaftLen, cy, thickness, ac, winW, winH);
            }
        }

        public void ComputeLayout(int winW, int winH)
        {
            _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
        }
    }
}