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

        // Remembers the exact size the panel had while floating (restored on tear-out)
        private readonly Dictionary<IPanel, Vector2> _originalFloatingSizes = new Dictionary<IPanel, Vector2>();

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
                panel.AllowDragging = true;
                panel.DockState = DockState.Tabbed;

                if (!_originalFloatingSizes.ContainsKey(panel))
                    _originalFloatingSizes[panel] = panel.Size;
            }
        }

        public void RemovePanel(IPanel panel)
        {
            _floatingPanels.Remove(panel);
            if (_draggingPanel == panel) _draggingPanel = null;
            _originalFloatingSizes.Remove(panel);

            if (_root != null)
            {
                _root.RemovePanel(panel);
                _root = CollapseNode(_root);          // collapse split completely so sibling fills space
                if (_root != null)
                    _root.ComputeLayout(0, MenuBarHeight, 1920, 1080); // full recompute to force sibling expansion
            }
        }

        public void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus, int winW, int winH)
        {
            if (_root == null) return;

            // === DOCKED / WORKSPACE PANELS – title-bar tear-out + resize + close ===
            if (_root.HitTest(mousePos, out IPanel dockedHit, out bool isTitle, out _, out _, out _))
            {
                if (dockedHit != null)
                {
                    dockedHit.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);

                    if (mousePressed)
                    {
                        // Title-bar tear-out from workspace (restores original floating size)
                        if (isTitle && dockedHit.AllowDragging)
                        {
                            RestoreOriginalFloatingSize(dockedHit);
                            _root.RemovePanel(dockedHit);
                            _floatingPanels.Add(dockedHit);
                            dockedHit.DockState = DockState.Floating;
                            dockedHit.AllowDragging = true;
                            _draggingPanel = dockedHit;
                            _dragOffset = mousePos - dockedHit.Position;
                            if (dockedHit is BasePanel bp)
                                bp.StartTitleBarDrag(mousePos);

                            _root = CollapseNode(_root);
                            if (_root != null)
                                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
                            return;
                        }

                        // Fallback manual title-bar tear-out (single-tab panels)
                        if (dockedHit.HasTitleBar && dockedHit.AllowDragging)
                        {
                            bool overTitle = mousePos.Y >= dockedHit.Position.Y && mousePos.Y <= dockedHit.Position.Y + BasePanel.TitleHeight;
                            if (overTitle)
                            {
                                RestoreOriginalFloatingSize(dockedHit);
                                _root.RemovePanel(dockedHit);
                                _floatingPanels.Add(dockedHit);
                                dockedHit.DockState = DockState.Floating;
                                dockedHit.AllowDragging = true;
                                _draggingPanel = dockedHit;
                                _dragOffset = mousePos - dockedHit.Position;
                                if (dockedHit is BasePanel bp)
                                    bp.StartTitleBarDrag(mousePos);

                                _root = CollapseNode(_root);
                                if (_root != null)
                                    _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
                                return;
                            }
                        }

                        // Resize support on docked panels
                        ResizeHandle handle = dockedHit.GetResizeHandle(mousePos);
                        if (handle != ResizeHandle.None)
                        {
                            dockedHit.StartResize(mousePos, handle);
                            return;
                        }
                    }
                }
            }

            // === FLOATING PANELS – 100% unchanged working behavior ===
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
                        break;
                }
            }

            if (mousePressed && _draggingPanel == null && hoveredPanel == null)
            {
                if (_root.HitTest(mousePos, out IPanel hit2, out bool isTitle2, out _, out _, out _))
                {
                    if (isTitle2 && hit2.AllowDragging)
                    {
                        _root.RemovePanel(hit2);
                        _floatingPanels.Add(hit2);
                        hit2.DockState = DockState.Floating;
                        hit2.AllowDragging = true;
                        _draggingPanel = hit2;
                        _dragOffset = mousePos - hit2.Position;
                        if (hit2 is BasePanel bp)
                            bp.StartTitleBarDrag(mousePos);
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
                bool shouldDock = false;
                if (_showHoverIcons)
                {
                    if (_hoveredPanelDuringDrag != null)
                    {
                        DockNode targetNode = _root.FindNode(_hoveredPanelDuringDrag);

                        Vector2 rel = mousePos - _hoverIconCenter;
                        float absX = Math.Abs(rel.X);
                        float absY = Math.Abs(rel.Y);

                        if (absX < IconSize * 0.35f && absY < IconSize * 0.35f)
                        {
                            if (targetNode is DockTabbedNode tabbed)
                                tabbed.AddPanel(_draggingPanel);
                            else
                                _root.AddPanel(_draggingPanel);
                            shouldDock = true;
                        }
                        else
                        {
                            bool horizontalSplit = absY > absX;

                            DockSplitNode newSplit = new DockSplitNode();
                            newSplit.IsVertical = horizontalSplit;
                            newSplit.SplitRatio = 0.5f;

                            if (horizontalSplit)
                            {
                                if (rel.Y < 0)
                                {
                                    newSplit.Left = new DockTabbedNode();
                                    newSplit.Right = targetNode;
                                    ((DockTabbedNode)newSplit.Left).AddPanel(_draggingPanel);
                                }
                                else
                                {
                                    newSplit.Left = targetNode;
                                    newSplit.Right = new DockTabbedNode();
                                    ((DockTabbedNode)newSplit.Right).AddPanel(_draggingPanel);
                                }
                            }
                            else
                            {
                                if (rel.X < 0)
                                {
                                    newSplit.Left = new DockTabbedNode();
                                    newSplit.Right = targetNode;
                                    ((DockTabbedNode)newSplit.Left).AddPanel(_draggingPanel);
                                }
                                else
                                {
                                    newSplit.Left = targetNode;
                                    newSplit.Right = new DockTabbedNode();
                                    ((DockTabbedNode)newSplit.Right).AddPanel(_draggingPanel);
                                }
                            }

                            _root = ReplaceInTree(_root, targetNode, newSplit);
                            shouldDock = true;
                        }
                    }
                    else if (_hoveringWorkspace)
                    {
                        float cs = IconSize * 0.3f;
                        if (mousePos.X >= _hoverIconCenter.X - cs * 0.5f && mousePos.X <= _hoverIconCenter.X + cs * 0.5f &&
                            mousePos.Y >= _hoverIconCenter.Y - cs * 0.5f && mousePos.Y <= _hoverIconCenter.Y + cs * 0.5f)
                        {
                            _root.AddPanel(_draggingPanel);
                            shouldDock = true;
                        }
                    }
                }

                if (shouldDock)
                {
                    _floatingPanels.Remove(_draggingPanel);
                    _draggingPanel.DockState = DockState.Tabbed;
                    _draggingPanel.AllowDragging = true;
                    _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
                }

                if (_draggingPanel is BasePanel bp) bp.ResetDragState();
                _draggingPanel = null;
                _hoveredPanelDuringDrag = null;
                _showHoverIcons = false;
                _hoveringWorkspace = false;
            }

            // Final safety collapse + recompute after every frame (ensures sibling always fills space)
            _root = CollapseNode(_root);
            if (_root != null)
            {
                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
                _root.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta, eventBus);
            }
        }

        private DockNode ReplaceInTree(DockNode current, DockNode oldNode, DockNode newNode)
        {
            if (current == oldNode) return newNode;
            if (current is DockSplitNode split)
            {
                split.Left = ReplaceInTree(split.Left, oldNode, newNode);
                split.Right = ReplaceInTree(split.Right, oldNode, newNode);
            }
            return current;
        }

        private DockNode CollapseNode(DockNode node)
        {
            if (node == null) return null;

            // Aggressive collapse: keep collapsing until stable
            bool changed = true;
            while (changed)
            {
                changed = false;
                if (node is DockSplitNode split)
                {
                    split.Left = CollapseNode(split.Left);
                    split.Right = CollapseNode(split.Right);

                    if (split.Left == null)
                    {
                        node = split.Right;
                        changed = true;
                    }
                    else if (split.Right == null)
                    {
                        node = split.Left;
                        changed = true;
                    }
                }
            }
            return node;
        }

        private void RestoreOriginalFloatingSize(IPanel panel)
        {
            if (_originalFloatingSizes.TryGetValue(panel, out Vector2 origSize))
            {
                panel.Size = origSize;
                panel.OnPanelResize(origSize.X, origSize.Y);
                _originalFloatingSizes.Remove(panel);
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
                        bp.StartTitleBarDrag(mousePos);
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

            if (_root != null && _root.HitTest(mousePos, out IPanel dockedHit, out _, out _, out _, out _))
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
            if (_root != null)
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
            if (_root != null)
                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
        }
    }
}