// Folder: SiegeEngine.Core.Managers
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
/*
 * PROTECTED PATHS:
 * 1. Title-bar tear-out (drag from workspace)
 * 2. Close-button guard (prevents close click from triggering tear-out)
 * 3. Live splitter drag (absolute mouse priority at any nesting depth, panels resize live, no snap-back)
 */
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
        private readonly Dictionary<IPanel, Vector2> _originalFloatingSizes = new Dictionary<IPanel, Vector2>();
        private bool _splitterDraggingThisFrame;
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
                _root = CollapseNode(_root);
                if (_root == null)
                    _root = new DockTabbedNode();
            }
        }
        public bool HasActiveContent()
        {
            if (_floatingPanels.Count > 0) return true;
            if (_root == null) return false;
            return HasContentRecursive(_root);
        }
        private bool HasContentRecursive(DockNode node)
        {
            if (node is DockTabbedNode tab) return tab.Panels.Count > 0;
            if (node is DockSplitNode split)
                return HasContentRecursive(split.Left) || HasContentRecursive(split.Right);
            return false;
        }
        private bool IsAnySplitterDragging(DockNode node)
        {
            if (node is DockSplitNode split && split.IsDraggingSplitter())
                return true;
            if (node is DockSplitNode splitNode)
            {
                if (IsAnySplitterDragging(splitNode.Left)) return true;
                if (IsAnySplitterDragging(splitNode.Right)) return true;
            }
            return false;
        }
        public void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus, int winW, int winH)
        {
            if (_floatingPanels.Count == 0 && !HasActiveContent())
                return;
            if (_root != null)
            {
                _root = CollapseNode(_root);
                if (_root == null) _root = new DockTabbedNode();
            }
            // PRE-INPUT LAYOUT: guarantees HitTest / FindDeepestSplitter / splitter detection use current accurate Rects
            if (_root != null)
            {
                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
            }
            _splitterDraggingThisFrame = false;
            if (_root != null)
            {
                _root.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta, eventBus);
                _splitterDraggingThisFrame = IsAnySplitterDragging(_root);
            }
            // SPLITTER PRIORITY FIRST – if any splitter is dragging, skip all title-bar logic
            if (_splitterDraggingThisFrame)
            {
                // splitter owns the mouse – do nothing else
            }
            else if (_root != null)
            {
                // normal docked panel input (title bar, close, resize)
                // NOTE: NO second Update call – DockTabbedNode.Update is the single source of truth
                // (PanelChrome.HandleUpdate → ClosePanelEvent fires here)
                if (_root.HitTest(mousePos, out IPanel dockedHit, out bool isTitle, out bool isSplitter, out _, out _))
                {
                    if (dockedHit != null && !isSplitter)
                    {
                        if (mousePressed)
                        {
                            // Close-button guard – must come BEFORE tear-out logic
                            if (dockedHit.IsOverCloseButton(mousePos))
                                return;
                            if (isTitle && dockedHit.AllowDragging)
                            {
                                TearOutPanel(dockedHit, mousePos, winW, winH);
                                return;
                            }
                            ResizeHandle handle = dockedHit.GetResizeHandle(mousePos);
                            if (handle != ResizeHandle.None)
                            {
                                dockedHit.StartResize(mousePos, handle);
                                return;
                            }
                        }
                    }
                }
            }
            // Floating panels unchanged
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
            if (mousePressed && _draggingPanel == null && hoveredPanel == null && !_splitterDraggingThisFrame)
            {
                if (_root != null && _root.HitTest(mousePos, out IPanel hit2, out bool isTitle2, out bool isSplitter2, out _, out _))
                {
                    if (isTitle2 && hit2.AllowDragging && !isSplitter2)
                    {
                        if (!hit2.IsOverCloseButton(mousePos))
                        {
                            TearOutPanel(hit2, mousePos, winW, winH);
                            return;
                        }
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
                        DockNode targetNode = null;
                        if (_root != null)
                            targetNode = _root.FindNode(_hoveredPanelDuringDrag);
                        Vector2 rel = mousePos - _hoverIconCenter;
                        float absX = Math.Abs(rel.X);
                        float absY = Math.Abs(rel.Y);
                        if (absX < IconSize * 0.35f && absY < IconSize * 0.35f)
                        {
                            if (targetNode is DockTabbedNode tabbed)
                                tabbed.AddPanel(_draggingPanel);
                            else if (_root != null)
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
                            if (_root != null)
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
                            if (_root != null)
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
                }
                else
                {
                    if (_root != null && _root.FindNode(_draggingPanel) != null)
                    {
                        _root.RemovePanel(_draggingPanel);
                        _root = CollapseNode(_root);
                        if (_root == null) _root = new DockTabbedNode();
                    }
                    if (!_floatingPanels.Contains(_draggingPanel))
                        _floatingPanels.Add(_draggingPanel);
                    _draggingPanel.DockState = DockState.Floating;
                    _draggingPanel.AllowDragging = true;
                    RestoreOriginalFloatingSize(_draggingPanel);
                }
                if (_draggingPanel is BasePanel bp) bp.ResetDragState();
                _draggingPanel = null;
                _hoveredPanelDuringDrag = null;
                _showHoverIcons = false;
                _hoveringWorkspace = false;
            }
            // POST-INPUT LAYOUT: applies new SplitRatio immediately so Render sees correct positions this frame
            if (_root != null)
            {
                _root = CollapseNode(_root);
                if (_root == null)
                    _root = new DockTabbedNode();
                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
            }
        }
        private void TearOutPanel(IPanel panel, Vector2 mousePos, int winW, int winH)
        {
            RestoreOriginalFloatingSize(panel);
            _root.RemovePanel(panel);
            _floatingPanels.Add(panel);
            panel.DockState = DockState.Floating;
            panel.AllowDragging = true;
            _draggingPanel = panel;
            _dragOffset = mousePos - panel.Position;
            if (panel is BasePanel bp)
                bp.StartTitleBarDrag(mousePos);
            _root = CollapseNode(_root);
            if (_root == null) _root = new DockTabbedNode();
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
                else if (node is DockTabbedNode tabbed && tabbed.Panels.Count == 0)
                {
                    node = null;
                    changed = true;
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
                if (panel.IsOverCloseButton(mousePos))
                    return false;
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
                if (dockedHit != null && dockedHit != _draggingPanel)
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
            {
                _root.Render(renderContext, winW, winH);
            }
            renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            renderContext.Viewport(0, 0, (uint)winW, (uint)winH);
            if (_root != null)
            {
                RenderSplitters(_root, renderContext, winW, winH);
            }
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
            // CRITICAL: explicit DepthTest disable guarantees hover icons sit above every title bar
            // (previous panel.Render calls may have re-enabled it)
            renderContext.Disable(renderContext.Enums.DepthTest);
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
                _quadRenderer.DrawLine(cx, cy - cs * 0.5f - shaftLen, cx, cy - cs * 0.5f, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - 18, cy - cs * 0.5f - shaftLen + 28, cx, cy - cs * 0.5f - shaftLen, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + 18, cy - cs * 0.5f - shaftLen + 28, cx, cy - cs * 0.5f - shaftLen, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx, cy + cs * 0.5f + shaftLen, cx, cy + cs * 0.5f, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - 18, cy + cs * 0.5f + shaftLen - 28, cx, cy + cs * 0.5f + shaftLen, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + 18, cy + cs * 0.5f + shaftLen - 28, cx, cy + cs * 0.5f + shaftLen, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - cs * 0.5f - shaftLen, cy, cx - cs * 0.5f, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - cs * 0.5f - shaftLen + 28, cy - 18, cx - cs * 0.5f - shaftLen, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx - cs * 0.5f - shaftLen + 28, cy + 18, cx - cs * 0.5f - shaftLen, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + cs * 0.5f + shaftLen, cy, cx + cs * 0.5f, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + cs * 0.5f + shaftLen - 28, cy - 18, cx + cs * 0.5f + shaftLen, cy, thickness, ac, winW, winH);
                _quadRenderer.DrawLine(cx + cs * 0.5f + shaftLen - 28, cy + 18, cx + cs * 0.5f + shaftLen, cy, thickness, ac, winW, winH);
            }
        }
        private void RenderSplitters(DockNode node, IRenderContext renderContext, int winW, int winH)
        {
            if (node is DockSplitNode split)
            {
                Vector4 splitterColor = new Vector4(0.55f, 0.55f, 0.6f, 1.0f);
                if (split.IsVertical)
                {
                    float splitY = split.Rect.Y + split.Rect.W * split.SplitRatio;
                    _quadRenderer.DrawLine(split.Rect.X, splitY, split.Rect.X + split.Rect.Z, splitY, 5f, splitterColor, winW, winH);
                }
                else
                {
                    float splitX = split.Rect.X + split.Rect.Z * split.SplitRatio;
                    _quadRenderer.DrawLine(splitX, split.Rect.Y, splitX, split.Rect.Y + split.Rect.W, 5f, splitterColor, winW, winH);
                }
                RenderSplitters(split.Left, renderContext, winW, winH);
                RenderSplitters(split.Right, renderContext, winW, winH);
            }
        }
        public void ComputeLayout(int winW, int winH)
        {
            if (_root != null)
                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
        }
    }
}