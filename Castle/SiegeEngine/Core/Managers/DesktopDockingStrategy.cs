// Folder: SiegeEngine.Core.Managers
// File: DesktopDockingStrategy.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.Core.Managers
{
    public class DesktopDockingStrategy : IDockingStrategy
    {
        private DockNode _root;
        private List<IPanel> _floatingPanels = new List<IPanel>();
        private IPanel _draggingPanel;
        private Vector2 _dragOffset;
        private DockNode _dragOriginNode;
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly EventBus _eventBus;
        private const float SnapDistance = 20f;
        private int _lastWinW;
        private int _lastWinH;
        private IPanel _draggingFloatingPanel;
        private bool _needsLayout = true;
        private IPanel _resizingPanel;
        private ResizeHandle _activeResizeHandle = ResizeHandle.None;
        private Vector2 _resizeStartMousePos;
        private Vector2 _resizeStartPosition;
        private Vector2 _resizeStartSize;
        private readonly UIQuadRenderer _ghostRenderer;
        private Vector2 _snapPreviewPosition = Vector2.Zero;
        private Vector2 _snapPreviewSize = Vector2.Zero;
        private bool _showSnapPreview;
        public DesktopDockingStrategy(IRenderContext renderContext, IControlContext controlContext, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _eventBus = eventBus;
            _root = new DockTabbedNode();
            _ghostRenderer = new UIQuadRenderer(renderContext);
        }
        private float GetHeaderHeight()
        {
            return 0f; // exact original behavior - no forced top space
        }
        public void AddPanel(IPanel panel)
        {
            if (panel.DockState == DockState.Floating)
            {
                _floatingPanels.Add(panel);
                panel.AllowDragging = true;
                panel.HeaderHeight = GetHeaderHeight();
            }
            else
            {
                _root.AddPanel(panel);
                panel.AllowDragging = false;
                panel.HeaderHeight = GetHeaderHeight();
            }
            _needsLayout = true;
        }
        public void RemovePanel(IPanel panel)
        {
            if (_floatingPanels.Remove(panel))
            {
                if (_draggingFloatingPanel == panel) _draggingFloatingPanel = null;
                if (_resizingPanel == panel) _resizingPanel = null;
                _needsLayout = true;
                return;
            }
            if (_root.RemovePanel(panel))
            {
                _needsLayout = true;
            }
        }
        public bool HasActiveContent()
        {
            return _floatingPanels.Count > 0 || _root != null; // Desktop is always "active" once it has ever been used (root exists)
        }
        private void ApplySnap(IPanel panel, Vector2 absMousePos, int winW, int winH)
        {
            float cornerZone = winH * 0.25f;
            bool nearLeft = absMousePos.X < SnapDistance;
            bool nearRight = absMousePos.X > winW - SnapDistance;
            bool nearTop = absMousePos.Y < SnapDistance;
            bool nearBottom = absMousePos.Y > winH - SnapDistance;
            bool inTopZone = absMousePos.Y < cornerZone;
            bool inBottomZone = absMousePos.Y > winH - cornerZone;
            Vector2 newPosition = panel.Position;
            Vector2 newSize = panel.Size;
            if (nearTop && nearLeft && inTopZone)
            {
                newPosition = new Vector2(0, 0);
                newSize = new Vector2(winW / 2f, winH / 2f);
            }
            else if (nearTop && nearRight && inTopZone)
            {
                newPosition = new Vector2(winW / 2f, 0);
                newSize = new Vector2(winW / 2f, winH / 2f);
            }
            else if (nearBottom && nearLeft && inBottomZone)
            {
                newPosition = new Vector2(0, winH / 2f);
                newSize = new Vector2(winW / 2f, winH / 2f);
            }
            else if (nearBottom && nearRight && inBottomZone)
            {
                newPosition = new Vector2(winW / 2f, winH / 2f);
                newSize = new Vector2(winW / 2f, winH / 2f);
            }
            else if (nearLeft)
            {
                newPosition = new Vector2(0, 0);
                newSize = new Vector2(winW / 2f, winH);
            }
            else if (nearRight)
            {
                newPosition = new Vector2(winW - winW / 2f, 0);
                newSize = new Vector2(winW / 2f, winH);
            }
            else if (nearTop)
            {
                newPosition = new Vector2(0, 0);
                newSize = new Vector2(winW, winH);
            }
            else if (nearBottom)
            {
                newPosition = new Vector2(0, winH - winH / 2f);
                newSize = new Vector2(winW, winH / 2f);
            }
            else
            {
                return;
            }
            panel.Position = newPosition;
            panel.Size = newSize;
            panel.OnPanelResize(newSize.X, newSize.Y);
        }
        public void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus, int winW, int winH)
        {
            if (winW != _lastWinW || winH != _lastWinH)
            {
                foreach (var panel in _floatingPanels.ToArray())
                {
                    if (panel.Position == Vector2.Zero && panel.Size.X == _lastWinW && panel.Size.Y == _lastWinH)
                    {
                        panel.Size = new Vector2(winW, winH);
                        panel.OnPanelResize(winW, winH);
                    }
                }
                _lastWinW = winW;
                _lastWinH = winH;
                _needsLayout = true;
            }
            if (_needsLayout)
            {
                _root.ComputeLayout(0, 0, winW, winH);
                _needsLayout = false;
            }
            if (_draggingFloatingPanel != null)
            {
                _draggingFloatingPanel.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            }
            if (_resizingPanel != null)
            {
                _resizingPanel.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
                if (_resizingPanel.Position.Y < 0)
                {
                    _resizingPanel.Position = new Vector2(_resizingPanel.Position.X, 0);
                }
            }
            bool handled = false;
            IPanel topModal = null;
            for (int i = _floatingPanels.Count - 1; i >= 0; i--)
            {
                var panel = _floatingPanels[i];
                if (panel.IsModal && panel.Visible)
                {
                    topModal = panel;
                    break;
                }
            }
            if (topModal != null)
            {
                bool over = mousePos.X >= topModal.Position.X && mousePos.X <= topModal.Position.X + topModal.Size.X &&
                            mousePos.Y >= topModal.Position.Y && mousePos.Y <= topModal.Position.Y + topModal.Size.Y;
                bool overTitle = mousePos.Y >= topModal.Position.Y && mousePos.Y <= topModal.Position.Y + 20f;
                if (mousePressed && overTitle && topModal.AllowDragging)
                {
                    _draggingFloatingPanel = topModal;
                }
                if (over || _draggingFloatingPanel == topModal)
                {
                    // ONLY the true topmost panel ever receives content/chrome Update
                    if (PanelManager.Current?.GetTopmostPanelAt(mousePos) == topModal || _draggingFloatingPanel == topModal)
                    {
                        topModal.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
                    }
                    handled = true;
                }
                if (!handled && mouseReleased && _draggingFloatingPanel != topModal)
                {
                    eventBus.Publish(new ClosePanelEvent(topModal));
                }
                handled = true;
            }
            if (!handled)
            {
                for (int i = _floatingPanels.Count - 1; i >= 0; i--)
                {
                    var panel = _floatingPanels[i];
                    if (!panel.Visible || panel.IsModal) continue;
                    if (panel == _draggingFloatingPanel) continue;
                    Vector2 rel = mousePos - panel.Position;
                    bool over = rel.X >= 0 && rel.X <= panel.Size.X && rel.Y >= 0 && rel.Y <= panel.Size.Y;
                    if (over)
                    {
                        if (mousePressed && panel.AllowDragging && panel.DockState == DockState.Floating)
                        {
                            bool overTitle = mousePos.Y >= panel.Position.Y && mousePos.Y <= panel.Position.Y + 20f;
                            if (overTitle)
                            {
                                _draggingFloatingPanel = panel;
                            }
                            else
                            {
                                ResizeHandle handle = panel.GetResizeHandle(mousePos);
                                if (handle != ResizeHandle.None)
                                {
                                    _resizingPanel = panel;
                                    _activeResizeHandle = handle;
                                    _resizeStartMousePos = mousePos;
                                    _resizeStartPosition = panel.Position;
                                    _resizeStartSize = panel.Size;
                                    panel.StartResize(mousePos, handle);
                                }
                            }
                        }
                        handled = true;
                        break;
                    }
                }
            }
            if (!handled)
            {
                _root.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta, eventBus);
            }
            if (_draggingPanel != null)
            {
                _draggingPanel.Position = mousePos - _dragOffset;
                if (_draggingPanel.Position.Y < 0)
                {
                    _draggingPanel.Position = new Vector2(_draggingPanel.Position.X, 0);
                }
            }
            _showSnapPreview = false;
            bool isAnyDragActive = _draggingFloatingPanel != null || _draggingPanel != null;
            if (isAnyDragActive)
            {
                _showSnapPreview = ComputeSnapPreview(mousePos, winW, winH, out _snapPreviewPosition, out _snapPreviewSize);
            }
            if (mouseReleased)
            {
                if (_draggingFloatingPanel != null)
                {
                    ApplySnap(_draggingFloatingPanel, mousePos, winW, winH);
                    _draggingFloatingPanel = null;
                }
                if (_draggingPanel != null)
                {
                    DockState newState = GetDockStateFromPosition(mousePos, winW, winH);
                    if (newState != DockState.Floating)
                    {
                        _dragOriginNode.RemovePanel(_draggingPanel);
                        _floatingPanels.Remove(_draggingPanel);
                        _draggingPanel.DockState = newState;
                        _draggingPanel.AllowDragging = false;
                        DockPanel(_draggingPanel, newState);
                    }
                    _draggingPanel = null;
                    _dragOriginNode = null;
                }
                _showSnapPreview = false;
                if (_resizingPanel != null)
                {
                    _resizingPanel = null;
                    _activeResizeHandle = ResizeHandle.None;
                }
            }
        }
        private bool ComputeSnapPreview(Vector2 mousePos, int winW, int winH, out Vector2 previewPos, out Vector2 previewSize)
        {
            previewPos = Vector2.Zero;
            previewSize = Vector2.Zero;
            float headerH = 0f;
            float cornerZone = winH * 0.25f;
            bool nearLeft = mousePos.X < SnapDistance;
            bool nearRight = mousePos.X > winW - SnapDistance;
            bool nearTop = mousePos.Y < headerH + SnapDistance;
            bool nearBottom = mousePos.Y > winH - SnapDistance;
            bool inTopZone = mousePos.Y < headerH + cornerZone;
            bool inBottomZone = mousePos.Y > winH - cornerZone;
            if (nearTop && nearLeft && inTopZone)
            {
                previewPos = new Vector2(0, headerH);
                previewSize = new Vector2(winW / 2f, (winH - headerH) / 2f);
                return true;
            }
            if (nearTop && nearRight && inTopZone)
            {
                previewPos = new Vector2(winW / 2f, headerH);
                previewSize = new Vector2(winW / 2f, (winH - headerH) / 2f);
                return true;
            }
            if (nearBottom && nearLeft && inBottomZone)
            {
                previewPos = new Vector2(0, headerH + (winH - headerH) / 2f);
                previewSize = new Vector2(winW / 2f, (winH - headerH) / 2f);
                return true;
            }
            if (nearBottom && nearRight && inBottomZone)
            {
                previewPos = new Vector2(winW / 2f, headerH + (winH - headerH) / 2f);
                previewSize = new Vector2(winW / 2f, (winH - headerH) / 2f);
                return true;
            }
            if (nearLeft)
            {
                previewPos = new Vector2(0, headerH);
                previewSize = new Vector2(winW / 2f, winH - headerH);
                return true;
            }
            if (nearRight)
            {
                previewPos = new Vector2(winW - winW / 2f, headerH);
                previewSize = new Vector2(winW / 2f, winH - headerH);
                return true;
            }
            if (nearTop)
            {
                previewPos = new Vector2(0, headerH);
                previewSize = new Vector2(winW, winH - headerH);
                return true;
            }
            if (nearBottom)
            {
                previewPos = new Vector2(0, headerH + (winH - headerH) / 2f);
                previewSize = new Vector2(winW, (winH - headerH) / 2f);
                return true;
            }
            return false;
        }
        private DockState GetDockStateFromPosition(Vector2 mousePos, int winW, int winH)
        {
            float headerH = 0f;
            if (mousePos.X < SnapDistance) return DockState.DockedLeft;
            if (mousePos.X > winW - SnapDistance) return DockState.DockedRight;
            if (mousePos.Y < headerH + SnapDistance) return DockState.DockedTop;
            if (mousePos.Y > winH - SnapDistance) return DockState.DockedBottom;
            return DockState.Floating;
        }
        private void DockPanel(IPanel panel, DockState state)
        {
            DockSplitNode split = new DockSplitNode();
            split.IsVertical = state == DockState.DockedTop || state == DockState.DockedBottom;
            DockTabbedNode newTabbed = new DockTabbedNode();
            newTabbed.AddPanel(panel);
            if (state == DockState.DockedLeft || state == DockState.DockedTop)
            {
                split.Left = newTabbed;
                split.Right = _root;
            }
            else
            {
                split.Left = _root;
                split.Right = newTabbed;
            }
            _root = split;
            _needsLayout = true;
        }
        public IPanel GetTopmostPanelAt(Vector2 mousePos)
        {
            for (int i = _floatingPanels.Count - 1; i >= 0; i--)
            {
                var panel = _floatingPanels[i];
                if (!panel.Visible || panel.IsModal) continue;
                Vector2 rel = mousePos - panel.Position;
                bool over = rel.X >= 0 && rel.X <= panel.Size.X && rel.Y >= 0 && rel.Y <= panel.Size.Y;
                if (over) return panel;
            }
            if (_root != null && _root.HitTest(mousePos, out IPanel dockedHit, out _, out _, out _, out _))
            {
                if (dockedHit != null) return dockedHit;
            }
            return null;
        }
        public void Render(IRenderContext renderContext, int winW, int winH)
        {
            _root.Render(renderContext, winW, winH);
            foreach (var panel in _floatingPanels)
            {
                if (!panel.Visible) continue;
                int px = (int)panel.Position.X;
                int py = (int)(winH - panel.Position.Y - panel.Size.Y);
                uint pw = (uint)panel.Size.X;
                uint ph = (uint)panel.Size.Y;
                renderContext.Scissor(px, py, pw, ph);
                renderContext.Viewport(px, py, pw, ph);
                if (panel == _resizingPanel)
                {
                    _ghostRenderer.DrawQuad(panel.Position.X, panel.Position.Y, panel.Size.X, panel.Size.Y, new Vector4(0.3f, 0.8f, 1.0f, 0.25f), winW, winH);
                }
                panel.Render();
            }
            renderContext.Scissor(0, 0, (uint)winW, (uint)winH);
            renderContext.Viewport(0, 0, (uint)winW, (uint)winH);
            if (_showSnapPreview)
            {
                _ghostRenderer.DrawQuad(_snapPreviewPosition.X, _snapPreviewPosition.Y, _snapPreviewSize.X, _snapPreviewSize.Y, new Vector4(0.2f, 0.75f, 1.0f, 0.35f), winW, winH);
            }
        }
        public void ComputeLayout(int winW, int winH)
        {
            _root.ComputeLayout(0, 0, winW, winH);
        }
        public void BringFloatingPanelToFront(BasePanel panel)
        {
            if (panel == null) return;
            _floatingPanels.Remove(panel);
            _floatingPanels.Add(panel); // last = topmost in z-order
        }
    }
}