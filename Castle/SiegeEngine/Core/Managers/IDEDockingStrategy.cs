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
            for (int i = _floatingPanels.Count - 1; i >= 0; i--)
            {
                var p = _floatingPanels[i];
                if (!p.Visible) continue;
                p.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            }

            _root.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta, eventBus);

            if (mousePressed && _draggingPanel == null)
            {
                for (int i = _floatingPanels.Count - 1; i >= 0; i--)
                {
                    var p = _floatingPanels[i];
                    if (!p.Visible) continue;
                    bool overTitle = mousePos.Y >= p.Position.Y && mousePos.Y < p.Position.Y + p.HeaderHeight;
                    if (overTitle && p.AllowDragging)
                    {
                        _draggingPanel = p;
                        _dragOffset = mousePos - p.Position;
                        return;
                    }
                }

                if (_root.HitTest(mousePos, out IPanel hit, out bool isTitle, out _, out _, out _))
                {
                    if (isTitle && hit.AllowDragging)
                    {
                        _draggingPanel = hit;
                        _dragOffset = mousePos - hit.Position;
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
                // COMPLETELY FRESH MINIMAL DROP - NO REMOVAL, NO TREE, NO NOTHING
                // Panel stays exactly where it was dragged to (normal floating behavior)
                if (_draggingPanel is BasePanel bp) bp.ResetDragState();
                _draggingPanel = null;
                _hoveredPanelDuringDrag = null;
                _showHoverIcons = false;
                _hoveringWorkspace = false;
            }
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