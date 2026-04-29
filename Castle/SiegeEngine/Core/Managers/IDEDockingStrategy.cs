using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
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
        private readonly nint _window;
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
        private IPanel _resizingPanel;
        private ResizeHandle _activeResizeHandle = ResizeHandle.None;
        private Vector2 _resizeStartMousePos;
        private Vector2 _resizeStartPosition;
        private Vector2 _resizeStartSize;
        private bool _needsLayout = true;
        private int _lastWinW;
        private int _lastWinH;
        private DockState _hoverEdge = DockState.Floating;
        private Vector2 _edgePreviewPos;
        private Vector2 _edgePreviewSize;
        // Two caches: panel objects (reuse to prevent leak) + full layout json (exact dock state)
        private readonly Dictionary<string, List<IPanel>> _bladePanelCache = new Dictionary<string, List<IPanel>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _bladeLayoutCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string _lastBlade = "Scene Editor";
        public IDEDockingStrategy(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _quadRenderer = new UIQuadRenderer(renderContext);
            _root = new DockTabbedNode();
        }
        public bool ContainsFloatingPanel(IPanel panel) => _floatingPanels.Contains(panel);
        public void ClearAll()
        {
            var pm = PanelManager.Current;
            if (pm != null)
            {
                foreach (var p in _floatingPanels.ToList())
                    pm.RemovePanel(p);
            }
            _floatingPanels.Clear();
            _draggingPanel = null;
            _resizingPanel = null;
            _originalFloatingSizes.Clear();
            _root = new DockTabbedNode();
            _needsLayout = true;
            _hoverEdge = DockState.Floating;
            Console.WriteLine("[IDEDockingStrategy.ClearAll] Workspace fully cleared");
        }
        public void SwitchBlade(string newContext)
        {
            if (newContext == _lastBlade) return;
            Console.WriteLine($"[IDEDockingStrategy] SwitchBlade '{_lastBlade}' → '{newContext}' (no leak + exact dock state)");
            // 1. Save current panels (reuse objects) and full layout for the blade we are leaving
            if (!_bladePanelCache.ContainsKey(_lastBlade))
                _bladePanelCache[_lastBlade] = new List<IPanel>();
            _bladePanelCache[_lastBlade].Clear();
            foreach (var p in _floatingPanels) _bladePanelCache[_lastBlade].Add(p);
            if (_root != null) CollectPanelsRecursive(_root, _bladePanelCache[_lastBlade]);
            _bladeLayoutCache[_lastBlade] = SerializeState();
            // 2. Clear active lists only (no Dispose)
            _floatingPanels.Clear();
            _root = new DockTabbedNode();
            _draggingPanel = null;
            _resizingPanel = null;
            _needsLayout = true;
            // 3. Restore panels for new blade (reuse same objects)
            if (!_bladePanelCache.ContainsKey(newContext))
                _bladePanelCache[newContext] = new List<IPanel>();
            var toRestore = _bladePanelCache[newContext];
            foreach (var p in toRestore)
            {
                p.Show();
                if (p.DockState == DockState.Floating)
                    _floatingPanels.Add(p);
                else
                    _root.AddPanel(p);
            }
            // 4. Apply exact previous dock layout (positions, splits, tabs) without creating new panels
            if (_bladeLayoutCache.TryGetValue(newContext, out var savedLayout) && !string.IsNullOrEmpty(savedLayout))
            {
                ApplyLayoutToExistingPanels(savedLayout, toRestore);
            }
            _lastBlade = newContext;
            Console.WriteLine($"[IDEDockingStrategy] Blade '{newContext}' restored: {toRestore.Count} panels + exact dock state");
        }
        private void ApplyLayoutToExistingPanels(string json, List<IPanel> existingPanels)
        {
            if (string.IsNullOrEmpty(json) || existingPanels.Count == 0) return;
            try
            {
                var state = JsonSerializer.Deserialize<SerializableLayoutState>(json);
                // Rebuild dock tree using the SAME panel instances
                _root = RebuildDockTree(state.Root, existingPanels);
                // Restore floating panel positions/sizes
                foreach (var fp in state.FloatingPanels)
                {
                    var panel = existingPanels.FirstOrDefault(p => p.GetType().AssemblyQualifiedName == fp.PanelType);
                    if (panel != null)
                    {
                        panel.Position = fp.Position;
                        panel.Size = fp.Size;
                        panel.DockState = DockState.Floating;
                        if (!_floatingPanels.Contains(panel))
                            _floatingPanels.Add(panel);
                    }
                }
                _needsLayout = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IDEDockingStrategy] ApplyLayoutToExistingPanels failed: {ex.Message}");
            }
        }
        private DockNode RebuildDockTree(SerializableDockNode s, List<IPanel> existingPanels)
        {
            if (s == null) return new DockTabbedNode();
            if (s.NodeType == "tabbed")
            {
                var tab = new DockTabbedNode();
                tab.ActiveIndex = s.ActiveTabIndex;
                foreach (var panelType in s.Panels)
                {
                    var panel = existingPanels.FirstOrDefault(p => p.GetType().AssemblyQualifiedName == panelType);
                    if (panel != null) tab.AddPanel(panel);
                }
                return tab;
            }
            if (s.NodeType == "split")
            {
                var split = new DockSplitNode
                {
                    IsVertical = s.IsVertical,
                    SplitRatio = s.SplitRatio
                };
                split.Left = RebuildDockTree(s.Left, existingPanels);
                split.Right = RebuildDockTree(s.Right, existingPanels);
                return split;
            }
            return new DockTabbedNode();
        }
        private void CollectPanelsRecursive(DockNode node, List<IPanel> list)
        {
            if (node == null) return;
            if (node is DockTabbedNode tab) list.AddRange(tab.Panels);
            else if (node is DockSplitNode split)
            {
                CollectPanelsRecursive(split.Left, list);
                CollectPanelsRecursive(split.Right, list);
            }
        }
        public void AddPanel(IPanel panel)
        {
            if (panel == null) return;
            bool alreadyDocked = _floatingPanels.Contains(panel) || (_root != null && _root.FindNode(panel) != null);
            if (alreadyDocked)
            {
                panel.HasTitleBar = true;
                panel.IsClosable = true;
                panel.HeaderHeight = BasePanel.TitleHeight;
                panel.DockingMode = DockingMode.IDE;
                _needsLayout = true;
                return;
            }
            panel.HasTitleBar = true;
            panel.IsClosable = true;
            panel.HeaderHeight = BasePanel.TitleHeight;
            panel.DockingMode = DockingMode.IDE;
            if (panel.DockState == DockState.Floating || panel.DockState == DockState.DockedHeader)
            {
                if (!_floatingPanels.Contains(panel))
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
            _needsLayout = true;
        }
        public void RemovePanel(IPanel panel)
        {
            _floatingPanels.Remove(panel);
            if (_draggingPanel == panel) _draggingPanel = null;
            if (_resizingPanel == panel) _resizingPanel = null;
            _originalFloatingSizes.Remove(panel);
            if (_root != null)
            {
                _root.RemovePanel(panel);
                _root = CollapseNode(_root);
                if (_root == null)
                    _root = new DockTabbedNode();
            }
            _needsLayout = true;
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
        public IPanel GetTopmostPanelAt(Vector2 mousePos)
        {
            return PanelManager.Current?.GetTopmostPanelAt(mousePos);
        }
        public void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus, int winW, int winH)
        {
            if (_floatingPanels.Count == 0 && !HasActiveContent())
                return;
            IPanel topmost = PanelManager.Current?.GetTopmostPanelAt(mousePos);
            if (topmost is BasePanel topmostBasePanel && topmostBasePanel.RenderOrder > 0)
            {
                return;
            }
            if (winW != _lastWinW || winH != _lastWinH)
            {
                _lastWinW = winW;
                _lastWinH = winH;
                _needsLayout = true;
            }
            if (_root != null)
            {
                _root = CollapseNode(_root);
                if (_root == null) _root = new DockTabbedNode();
            }
            if (_root != null && !_splitterDraggingThisFrame)
            {
                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
            }
            _splitterDraggingThisFrame = false;
            bool mouseOverFloatingPanel = topmost != null && _floatingPanels.Contains(topmost);
            if (!mouseOverFloatingPanel && _root != null)
            {
                _root.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta, eventBus);
                _splitterDraggingThisFrame = IsAnySplitterDragging(_root);
            }
            if (_splitterDraggingThisFrame)
            {
                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
            }
            else if (_root != null && !mouseOverFloatingPanel)
            {
                if (_root.HitTest(mousePos, out IPanel dockedHit, out bool isTitle, out bool isSplitter, out _, out _))
                {
                    if (dockedHit != null && !isSplitter)
                    {
                        if (mousePressed)
                        {
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
            IPanel authoritativeTop = PanelManager.Current?.GetTopmostPanelAt(mousePos);
            for (int i = _floatingPanels.Count - 1; i >= 0; i--)
            {
                var panel = _floatingPanels[i];
                if (!panel.Visible) continue;
                if (authoritativeTop == panel)
                {
                    panel.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
                }
                if (HandleSinglePanel(panel, mousePos, mousePressed, winW, winH, authoritativeTop))
                    break;
            }
            if (mousePressed && _draggingPanel == null && !_splitterDraggingThisFrame)
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
            if (_resizingPanel != null && mouseDown)
            {
                PerformLiveResize(mousePos, winW, winH);
            }
            if (_draggingPanel != null && mouseReleased)
            {
                bool shouldDock = false;
                if (_showHoverIcons)
                {
                    if (_hoveredPanelDuringDrag != null)
                    {
                        if (Vector2.Distance(mousePos, _hoverIconCenter) < IconSize * 0.8f)
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
                    }
                    else if (_hoveringWorkspace)
                    {
                        if (_root != null)
                        {
                            _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
                            var mainTab = FindLargestTabbedNode(_root);
                            if (mainTab != null)
                                mainTab.AddPanel(_draggingPanel);
                            else
                                _root.AddPanel(_draggingPanel);
                        }
                        shouldDock = true;
                    }
                    else if (_hoverEdge != DockState.Floating)
                    {
                        DockToWindowEdge(_hoverEdge, winW, winH);
                        shouldDock = true;
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
                _hoverEdge = DockState.Floating;
                _needsLayout = true;
            }
            if (_resizingPanel != null && mouseReleased)
            {
                _resizingPanel.OnPanelResize(_resizingPanel.Size.X, _resizingPanel.Size.Y);
                _resizingPanel = null;
                _activeResizeHandle = ResizeHandle.None;
                _needsLayout = true;
            }
            if (_needsLayout && _root != null)
            {
                _root.ComputeLayout(0, MenuBarHeight, winW, winH - MenuBarHeight);
                _needsLayout = false;
            }
        }
        private DockTabbedNode FindLargestTabbedNode(DockNode node)
        {
            if (node == null) return null;
            if (node is DockTabbedNode tab) return tab;
            if (node is DockSplitNode split)
            {
                var leftTab = FindLargestTabbedNode(split.Left);
                var rightTab = FindLargestTabbedNode(split.Right);
                if (leftTab == null) return rightTab;
                if (rightTab == null) return leftTab;
                float leftArea = leftTab.Rect.Z * leftTab.Rect.W;
                float rightArea = rightTab.Rect.Z * rightTab.Rect.W;
                return leftArea >= rightArea ? leftTab : rightTab;
            }
            return null;
        }
        private void PerformLiveResize(Vector2 mousePos, int winW, int winH)
        {
            if (_resizingPanel == null) return;
            Vector2 delta = mousePos - _resizeStartMousePos;
            Vector2 newPos = _resizeStartPosition;
            Vector2 newSize = _resizeStartSize;
            switch (_activeResizeHandle)
            {
                case ResizeHandle.Left:
                    newSize.X = Math.Max(200f, _resizeStartSize.X - delta.X);
                    newPos.X = _resizeStartPosition.X + _resizeStartSize.X - newSize.X;
                    break;
                case ResizeHandle.Right:
                    newSize.X = Math.Max(200f, _resizeStartSize.X + delta.X);
                    break;
                case ResizeHandle.Top:
                    newSize.Y = Math.Max(150f, _resizeStartSize.Y - delta.Y);
                    newPos.Y = _resizeStartPosition.Y + _resizeStartSize.Y - newSize.Y;
                    break;
                case ResizeHandle.Bottom:
                    newSize.Y = Math.Max(150f, _resizeStartSize.Y + delta.Y);
                    break;
                case ResizeHandle.TopLeft:
                    newSize.X = Math.Max(200f, _resizeStartSize.X - delta.X);
                    newPos.X = _resizeStartPosition.X + _resizeStartSize.X - newSize.X;
                    newSize.Y = Math.Max(150f, _resizeStartSize.Y - delta.Y);
                    newPos.Y = _resizeStartPosition.Y + _resizeStartSize.Y - newSize.Y;
                    break;
                case ResizeHandle.TopRight:
                    newSize.X = Math.Max(200f, _resizeStartSize.X + delta.X);
                    newSize.Y = Math.Max(150f, _resizeStartSize.Y - delta.Y);
                    newPos.Y = _resizeStartPosition.Y + _resizeStartSize.Y - newSize.Y;
                    break;
                case ResizeHandle.BottomLeft:
                    newSize.X = Math.Max(200f, _resizeStartSize.X - delta.X);
                    newPos.X = _resizeStartPosition.X + _resizeStartSize.X - newSize.X;
                    newSize.Y = Math.Max(150f, _resizeStartSize.Y + delta.Y);
                    break;
                case ResizeHandle.BottomRight:
                    newSize.X = Math.Max(200f, _resizeStartSize.X + delta.X);
                    newSize.Y = Math.Max(150f, _resizeStartSize.Y + delta.Y);
                    break;
            }
            _resizingPanel.Position = newPos;
            _resizingPanel.Size = newSize;
            if (_resizingPanel is BasePanel bp)
            {
                bp.OnLiveResize(newSize.X, newSize.Y);
                bp._uiOverlay?.RefreshUI();
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
            _needsLayout = true;
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
        private bool HandleSinglePanel(IPanel panel, Vector2 mousePos, bool mousePressed, int winW, int winH, IPanel authoritativeTop)
        {
            bool overPanel = mousePos.X >= panel.Position.X && mousePos.X <= panel.Position.X + panel.Size.X &&
                             mousePos.Y >= panel.Position.Y && mousePos.Y <= panel.Position.Y + panel.Size.Y;
            if (!overPanel) return false;
            if (mousePressed && panel.HasTitleBar && panel.AllowDragging && _draggingPanel == null)
            {
                if (authoritativeTop != panel) return false;
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
                ResizeHandle handle = panel.GetResizeHandle(mousePos);
                if (handle != ResizeHandle.None)
                {
                    _resizingPanel = panel;
                    _activeResizeHandle = handle;
                    _resizeStartMousePos = mousePos;
                    _resizeStartPosition = panel.Position;
                    _resizeStartSize = panel.Size;
                    panel.StartResize(mousePos, handle);
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
            _hoverEdge = DockState.Floating;
            const float EdgeThreshold = 90f;
            bool nearLeft = mousePos.X < EdgeThreshold;
            bool nearRight = mousePos.X > winW - EdgeThreshold;
            bool nearBottom = mousePos.Y > winH - EdgeThreshold;
            if (nearLeft)
            {
                _hoverEdge = DockState.DockedLeft;
                _showHoverIcons = true;
                _edgePreviewPos = new Vector2(0, MenuBarHeight);
                _edgePreviewSize = new Vector2(winW * 0.2f, winH - MenuBarHeight);
                return;
            }
            if (nearRight)
            {
                _hoverEdge = DockState.DockedRight;
                _showHoverIcons = true;
                _edgePreviewPos = new Vector2(winW * 0.8f, MenuBarHeight);
                _edgePreviewSize = new Vector2(winW * 0.2f, winH - MenuBarHeight);
                return;
            }
            if (nearBottom)
            {
                _hoverEdge = DockState.DockedBottom;
                _showHoverIcons = true;
                _edgePreviewPos = new Vector2(0, winH * 0.8f);
                _edgePreviewSize = new Vector2(winW, winH * 0.2f);
                return;
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
        private void DockToWindowEdge(DockState edge, int winW, int winH)
        {
            if (_root == null)
                _root = new DockTabbedNode();
            var newTab = new DockTabbedNode();
            newTab.AddPanel(_draggingPanel);
            const float ratio = 0.2f;
            DockSplitNode newRoot = new DockSplitNode();
            if (edge == DockState.DockedLeft)
            {
                newRoot.IsVertical = false;
                newRoot.SplitRatio = ratio;
                newRoot.Left = newTab;
                newRoot.Right = _root;
            }
            else if (edge == DockState.DockedRight)
            {
                newRoot.IsVertical = false;
                newRoot.SplitRatio = 1f - ratio;
                newRoot.Left = _root;
                newRoot.Right = newTab;
            }
            else if (edge == DockState.DockedBottom)
            {
                newRoot.IsVertical = true;
                newRoot.SplitRatio = 1f - ratio;
                newRoot.Left = _root;
                newRoot.Right = newTab;
            }
            _root = newRoot;
            _needsLayout = true;
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
                if (panel == null || !panel.Visible) continue;
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
            renderContext.Disable(renderContext.Enums.DepthTest);
            if (_showHoverIcons)
            {
                if (_hoveredPanelDuringDrag != null || _hoveringWorkspace)
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
                if (_hoverEdge != DockState.Floating)
                {
                    Vector4 barColor = new Vector4(0.1f, 0.7f, 1.0f, 0.75f);
                    float barThickness = 14f;
                    float arrowLen = 32f;
                    float arrowThickness = 5f;
                    Vector4 arrowColor = new Vector4(1f, 1f, 1f, 1f);
                    if (_hoverEdge == DockState.DockedLeft)
                    {
                        _quadRenderer.DrawQuad(0, MenuBarHeight, barThickness, winH - MenuBarHeight, barColor, winW, winH);
                        float ax = barThickness + 12f;
                        float ay = (winH - MenuBarHeight) * 0.5f + MenuBarHeight;
                        _quadRenderer.DrawLine(ax, ay, ax + arrowLen, ay, arrowThickness, arrowColor, winW, winH);
                        _quadRenderer.DrawLine(ax + arrowLen - 12f, ay - 12f, ax + arrowLen, ay, arrowThickness, arrowColor, winW, winH);
                        _quadRenderer.DrawLine(ax + arrowLen - 12f, ay + 12f, ax + arrowLen, ay, arrowThickness, arrowColor, winW, winH);
                    }
                    else if (_hoverEdge == DockState.DockedRight)
                    {
                        _quadRenderer.DrawQuad(winW - barThickness, MenuBarHeight, barThickness, winH - MenuBarHeight, barColor, winW, winH);
                        float ax = winW - barThickness - 12f - arrowLen;
                        float ay = (winH - MenuBarHeight) * 0.5f + MenuBarHeight;
                        _quadRenderer.DrawLine(ax + arrowLen, ay, ax, ay, arrowThickness, arrowColor, winW, winH);
                        _quadRenderer.DrawLine(ax + 12f, ay - 12f, ax, ay, arrowThickness, arrowColor, winW, winH);
                        _quadRenderer.DrawLine(ax + 12f, ay + 12f, ax, ay, arrowThickness, arrowColor, winW, winH);
                    }
                    else if (_hoverEdge == DockState.DockedBottom)
                    {
                        _quadRenderer.DrawQuad(0, winH - barThickness, winW, barThickness, barColor, winW, winH);
                        float ax = winW * 0.5f;
                        float ay = winH - barThickness - 12f - arrowLen;
                        _quadRenderer.DrawLine(ax, ay + arrowLen, ax, ay, arrowThickness, arrowColor, winW, winH);
                        _quadRenderer.DrawLine(ax - 12f, ay + arrowLen - 12f, ax, ay, arrowThickness, arrowColor, winW, winH);
                        _quadRenderer.DrawLine(ax + 12f, ay + arrowLen - 12f, ax, ay, arrowThickness, arrowColor, winW, winH);
                    }
                }
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
        private class SerializableLayoutState
        {
            public SerializableDockNode Root { get; set; }
            public List<SerializableFloatingPanel> FloatingPanels { get; set; } = new List<SerializableFloatingPanel>();
        }
        private class SerializableDockNode
        {
            public string NodeType { get; set; }
            public List<string> Panels { get; set; } = new List<string>();
            public int ActiveTabIndex { get; set; } = -1;
            public bool IsVertical { get; set; }
            public float SplitRatio { get; set; } = 0.5f;
            public SerializableDockNode Left { get; set; }
            public SerializableDockNode Right { get; set; }
        }
        private class SerializableFloatingPanel
        {
            public string PanelType { get; set; }
            public Vector2 Position { get; set; }
            public Vector2 Size { get; set; }
        }
        public string SerializeState()
        {
            var state = new SerializableLayoutState();
            state.Root = SerializeNode(_root);
            foreach (var panel in _floatingPanels)
            {
                if (panel is BasePanel bp)
                {
                    state.FloatingPanels.Add(new SerializableFloatingPanel
                    {
                        PanelType = bp.GetType().AssemblyQualifiedName,
                        Position = bp.Position,
                        Size = bp.Size
                    });
                }
            }
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(state, options);
        }
        private SerializableDockNode SerializeNode(DockNode node)
        {
            if (node == null) return null;
            if (node is DockTabbedNode tab)
            {
                return new SerializableDockNode
                {
                    NodeType = "tabbed",
                    Panels = tab.Panels.Select(p => (p as BasePanel)?.GetType().AssemblyQualifiedName ?? p.GetType().AssemblyQualifiedName).ToList(),
                    ActiveTabIndex = tab.ActiveIndex
                };
            }
            if (node is DockSplitNode split)
            {
                return new SerializableDockNode
                {
                    NodeType = "split",
                    IsVertical = split.IsVertical,
                    SplitRatio = split.SplitRatio,
                    Left = SerializeNode(split.Left),
                    Right = SerializeNode(split.Right)
                };
            }
            return null;
        }
        public void DeserializeState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                ClearAll();
                var state = JsonSerializer.Deserialize<SerializableLayoutState>(json);
                if (state.Root != null)
                {
                    _root = DeserializeNode(state.Root);
                    RegisterAllPanelsInTree(_root);
                }
                foreach (var fp in state.FloatingPanels)
                {
                    var panel = CreatePanelByType(fp.PanelType);
                    if (panel != null)
                    {
                        panel.Position = fp.Position;
                        panel.Size = fp.Size;
                        panel.DockState = DockState.Floating;
                        _eventBus.Publish(new OpenPanelEvent(panel));
                        Console.WriteLine($"[IDEDockingStrategy] Restored floating panel {fp.PanelType}");
                    }
                }
                _needsLayout = true;
                Console.WriteLine($"[IDEDockingStrategy] SUCCESS: Restored full nested tree (all panels centrally registered)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IDEDockingStrategy] DeserializeState failed: {ex.Message}");
            }
        }
        private void RegisterAllPanelsInTree(DockNode node)
        {
            if (node == null) return;
            if (node is DockTabbedNode tab)
            {
                foreach (var p in tab.Panels)
                    _eventBus.Publish(new OpenPanelEvent(p));
            }
            else if (node is DockSplitNode split)
            {
                RegisterAllPanelsInTree(split.Left);
                RegisterAllPanelsInTree(split.Right);
            }
        }
        private IPanel CreatePanelByType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            try
            {
                Type t = Type.GetType(typeName);
                if (t != null && typeof(IPanel).IsAssignableFrom(t))
                {
                    var panel = (IPanel)Activator.CreateInstance(t, _renderContext, _controlContext, _window, _eventBus);
                    return panel;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IDEDockingStrategy] Could not create panel {typeName}: {ex.Message}");
            }
            return null;
        }
        private DockNode DeserializeNode(SerializableDockNode s)
        {
            if (s == null) return null;
            if (s.NodeType == "tabbed")
            {
                var tab = new DockTabbedNode();
                tab.ActiveIndex = s.ActiveTabIndex;
                foreach (var panelType in s.Panels)
                {
                    var panel = CreatePanelByType(panelType);
                    if (panel != null)
                    {
                        tab.AddPanel(panel);
                    }
                }
                return tab;
            }
            if (s.NodeType == "split")
            {
                var split = new DockSplitNode
                {
                    IsVertical = s.IsVertical,
                    SplitRatio = s.SplitRatio
                };
                split.Left = DeserializeNode(s.Left);
                split.Right = DeserializeNode(s.Right);
                return split;
            }
            return null;
        }
    }
}