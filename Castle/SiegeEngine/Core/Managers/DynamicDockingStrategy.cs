// Folder: SiegeEngine.Core.Managers
// File: DynamicDockingStrategy.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering.Renderers;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Managers
{
    public class DynamicDockingStrategy : IDockingStrategy
    {
        private readonly List<IPanel> _panels = new List<IPanel>();
        private IPanel _draggingPanel;
        private Vector2 _dragOffset;
        private IPanel _resizingPanel;
        private ResizeHandle _activeResizeHandle = ResizeHandle.None;
        private Vector2 _resizeStartMousePos;
        private Vector2 _resizeStartPosition;
        private Vector2 _resizeStartSize;
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly EventBus _eventBus;
        private readonly UIQuadRenderer _ghostRenderer;
        private Vector2 _snapPreviewPosition = Vector2.Zero;
        private Vector2 _snapPreviewSize = Vector2.Zero;
        private bool _showSnapPreview;
        private const float SnapDistance = 25f;
        private const float NeighborSnapMargin = 25f;

        public DynamicDockingStrategy(IRenderContext renderContext, IControlContext controlContext, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _eventBus = eventBus;
            _ghostRenderer = new UIQuadRenderer(renderContext);
        }

        public void AddPanel(IPanel panel)
        {
            if (panel.DockingMode != DockingMode.Dynamic) return;
            if (!_panels.Contains(panel))
            {
                _panels.Add(panel);
                panel.DockState = DockState.Floating;
                panel.AllowDragging = true;
                panel.HasTitleBar = true;
                panel.IsClosable = true;
            }
        }

        public void RemovePanel(IPanel panel)
        {
            _panels.Remove(panel);
            if (_draggingPanel == panel) _draggingPanel = null;
            if (_resizingPanel == panel) _resizingPanel = null;
        }

        public bool HasActiveContent()
        {
            return _panels.Count > 0;
        }

        // FIXED: uses the correct field name for this strategy (_panels)
        public void BringFloatingPanelToFront(BasePanel panel)
        {
            if (panel == null) return;
            _panels.Remove(panel);
            _panels.Add(panel); // last = topmost in z-order
        }

        public IPanel GetTopmostPanelAt(Vector2 mousePos)
        {
            IPanel topModal = null;
            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                if (_panels[i].IsModal && _panels[i].Visible)
                {
                    topModal = _panels[i];
                    break;
                }
            }
            if (topModal != null) return topModal;

            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                var panel = _panels[i];
                if (!panel.Visible) continue;
                bool overPanel = mousePos.X >= panel.Position.X && mousePos.X <= panel.Position.X + panel.Size.X &&
                                 mousePos.Y >= panel.Position.Y && mousePos.Y <= panel.Position.Y + panel.Size.Y;
                if (overPanel) return panel;
            }
            return null;
        }

        public void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus, int winW, int winH)
        {
            IPanel topModal = null;
            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                if (_panels[i].IsModal && _panels[i].Visible)
                {
                    topModal = _panels[i];
                    break;
                }
            }
            if (topModal != null)
            {
                if (PanelManager.Current?.GetTopmostPanelAt(mousePos) == topModal)
                {
                    topModal.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
                }
                HandleSinglePanel(topModal, mousePos, mousePressed, winW, winH);
                return;
            }

            IPanel hoveredPanel = null;
            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                var panel = _panels[i];
                if (!panel.Visible) continue;
                bool overPanel = mousePos.X >= panel.Position.X && mousePos.X <= panel.Position.X + panel.Size.X &&
                                 mousePos.Y >= panel.Position.Y && mousePos.Y <= panel.Position.Y + panel.Size.Y;
                if (overPanel)
                {
                    if (PanelManager.Current?.GetTopmostPanelAt(mousePos) == panel)
                    {
                        hoveredPanel = panel;
                        panel.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
                    }
                    if (HandleSinglePanel(panel, mousePos, mousePressed, winW, winH))
                    {
                        break;
                    }
                }
            }

            if (_draggingPanel != null && mouseDown)
            {
                _draggingPanel.Position = mousePos - _dragOffset;
                ClampToViewport(_draggingPanel, winW, winH);
                _showSnapPreview = ComputeSnapPreview(mousePos, winW, winH, out _snapPreviewPosition, out _snapPreviewSize);
            }
            if (_resizingPanel != null && mouseDown)
            {
                PerformLiveResize(mousePos, winW, winH);
            }

            if (mouseReleased)
            {
                if (_draggingPanel != null)
                {
                    bool mouseInside = mousePos.X >= 0 && mousePos.X <= winW && mousePos.Y >= 0 && mousePos.Y <= winH;
                    if (mouseInside)
                    {
                        _draggingPanel.Position = _snapPreviewPosition;
                    }
                    ClampToViewport(_draggingPanel, winW, winH);
                    _draggingPanel.OnPanelResize(_draggingPanel.Size.X, _draggingPanel.Size.Y);
                    if (_draggingPanel is BasePanel bp)
                    {
                        bp.ResetDragState();
                    }
                    _draggingPanel = null;
                    _showSnapPreview = false;
                }
                if (_resizingPanel != null)
                {
                    ClampToViewport(_resizingPanel, winW, winH);
                    _resizingPanel.OnPanelResize(_resizingPanel.Size.X, _resizingPanel.Size.Y);
                    _resizingPanel = null;
                    _activeResizeHandle = ResizeHandle.None;
                }
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
                    int idx = _panels.IndexOf(panel);
                    if (idx >= 0 && idx < _panels.Count - 1)
                    {
                        _panels.RemoveAt(idx);
                        _panels.Add(panel);
                    }
                    return true;
                }
            }

            if (mousePressed && _resizingPanel == null)
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
                    return true;
                }
            }
            return false;
        }

        private bool ComputeSnapPreview(Vector2 mousePos, int winW, int winH, out Vector2 previewPos, out Vector2 previewSize)
        {
            previewPos = _draggingPanel.Position;
            previewSize = _draggingPanel.Size;
            if (previewPos.X < SnapDistance) previewPos.X = 0;
            if (previewPos.X + previewSize.X > winW - SnapDistance) previewPos.X = winW - previewSize.X;
            if (previewPos.Y < SnapDistance) previewPos.Y = 0;
            if (previewPos.Y + previewSize.Y > winH - SnapDistance) previewPos.Y = winH - previewSize.Y;

            foreach (var other in _panels)
            {
                if (other == _draggingPanel || !other.Visible) continue;
                if (Math.Abs(previewPos.X - (other.Position.X + other.Size.X)) < NeighborSnapMargin)
                    previewPos.X = other.Position.X + other.Size.X;
                else if (Math.Abs((previewPos.X + previewSize.X) - other.Position.X) < NeighborSnapMargin)
                    previewPos.X = other.Position.X - previewSize.X;
                if (Math.Abs(previewPos.Y - (other.Position.Y + other.Size.Y)) < NeighborSnapMargin)
                    previewPos.Y = other.Position.Y + other.Size.Y;
                else if (Math.Abs((previewPos.Y + previewSize.Y) - other.Position.Y) < NeighborSnapMargin)
                    previewPos.Y = other.Position.Y - previewSize.Y;
            }
            return true;
        }

        private void ClampToViewport(IPanel panel, int winW, int winH)
        {
            if (panel == null) return;
            float x = Math.Clamp(panel.Position.X, 0f, winW - panel.Size.X);
            float y = Math.Clamp(panel.Position.Y, 0f, winH - panel.Size.Y);
            panel.Position = new Vector2(x, y);
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
            }
            ClampToViewport(_resizingPanel, winW, winH);
        }

        public void Render(IRenderContext renderContext, int winW, int winH)
        {
            foreach (var panel in _panels)
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
            if (_showSnapPreview && _draggingPanel != null)
            {
                _ghostRenderer.DrawQuad(_snapPreviewPosition.X, _snapPreviewPosition.Y, _snapPreviewSize.X, _snapPreviewSize.Y,
                    new Vector4(0.2f, 0.75f, 1.0f, 0.35f), winW, winH);
            }
        }

        public void ComputeLayout(int winW, int winH)
        {
        }
    }
}