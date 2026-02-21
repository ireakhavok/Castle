// Folder: SiegeEngine.Core.Managers
// File: DockManager.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Definitions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
namespace SiegeEngine.Core.Managers
{
    [JsonDerivedType(typeof(DockSplitNode), "split")]
    [JsonDerivedType(typeof(DockTabbedNode), "tabbed")]
    public abstract class DockNode
    {
        [JsonIgnore]
        public Vector4 Rect { get; protected set; }
        public abstract void ComputeLayout(float x, float y, float w, float h);
        public abstract bool HitTest(Vector2 mousePos, out IPanel hitPanel, out bool isTitle, out bool isSplitter, out bool isTab, out int tabIndex);
        public abstract bool Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus);
        public abstract void Render(IRenderContext renderContext, int winW, int winH);
        public abstract void AddPanel(IPanel panel);
        public abstract bool RemovePanel(IPanel panel);
        public abstract DockNode FindNode(IPanel panel);
    }
    public class DockSplitNode : DockNode
    {
        public DockNode Left { get; set; }
        public DockNode Right { get; set; }
        public float SplitRatio { get; set; } = 0.5f;
        public bool IsVertical { get; set; }
        private bool _draggingSplitter;
        private float _splitterSize = 5f;
        public override void ComputeLayout(float x, float y, float w, float h)
        {
            Rect = new Vector4(x, y, w, h);
            if (Left == null || Right == null) return;
            if (IsVertical)
            {
                float splitY = h * SplitRatio;
                Left.ComputeLayout(x, y, w, splitY);
                Right.ComputeLayout(x, y + splitY, w, h - splitY);
            }
            else
            {
                float splitX = w * SplitRatio;
                Left.ComputeLayout(x, y, splitX, h);
                Right.ComputeLayout(x + splitX, y, w - splitX, h);
            }
        }
        public override bool HitTest(Vector2 mousePos, out IPanel hitPanel, out bool isTitle, out bool isSplitter, out bool isTab, out int tabIndex)
        {
            hitPanel = null;
            isTitle = false;
            isSplitter = false;
            isTab = false;
            tabIndex = -1;
            if (Left.HitTest(mousePos, out hitPanel, out isTitle, out isSplitter, out isTab, out tabIndex))
                return true;
            if (Right.HitTest(mousePos, out hitPanel, out isTitle, out isSplitter, out isTab, out tabIndex))
                return true;
            if (IsVertical)
            {
                float splitY = Rect.Y + Rect.W * SplitRatio;
                if (mousePos.Y >= splitY - _splitterSize / 2 && mousePos.Y <= splitY + _splitterSize / 2 &&
                    mousePos.X >= Rect.X && mousePos.X <= Rect.X + Rect.Z)
                {
                    isSplitter = true;
                    return true;
                }
            }
            else
            {
                float splitX = Rect.X + Rect.Z * SplitRatio;
                if (mousePos.X >= splitX - _splitterSize / 2 && mousePos.X <= splitX + _splitterSize / 2 &&
                    mousePos.Y >= Rect.Y && mousePos.Y <= Rect.Y + Rect.W)
                {
                    isSplitter = true;
                    return true;
                }
            }
            return false;
        }
        public override bool Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus)
        {
            if (Left.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta, eventBus))
                return true;
            if (Right.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta, eventBus))
                return true;
            bool isOverSplitter = HitTest(mousePos, out _, out _, out bool isSplitter, out _, out _);
            if (isOverSplitter && isSplitter)
            {
                if (mousePressed)
                    _draggingSplitter = true;
            }
            if (_draggingSplitter && mouseDown)
            {
                if (IsVertical)
                {
                    SplitRatio = (mousePos.Y - Rect.Y) / Rect.W;
                }
                else
                {
                    SplitRatio = (mousePos.X - Rect.X) / Rect.Z;
                }
                SplitRatio = Math.Clamp(SplitRatio, 0.1f, 0.9f);
                return true;
            }
            if (mouseReleased)
                _draggingSplitter = false;
            return false;
        }
        public override void Render(IRenderContext renderContext, int winW, int winH)
        {
            Left.Render(renderContext, winW, winH);
            Right.Render(renderContext, winW, winH);
        }
        public override void AddPanel(IPanel panel)
        {
            if (Right == null)
            {
                Right = new DockTabbedNode();
            }
            Right.AddPanel(panel);
        }
        public override bool RemovePanel(IPanel panel)
        {
            if (Left.RemovePanel(panel))
            {
                if (Left is DockTabbedNode lt && lt.Panels.Count == 0)
                {
                    Left = null;
                }
                return true;
            }
            if (Right.RemovePanel(panel))
            {
                if (Right is DockTabbedNode rt && rt.Panels.Count == 0)
                {
                    Right = null;
                }
                return true;
            }
            return false;
        }
        public override DockNode FindNode(IPanel panel)
        {
            var leftNode = Left.FindNode(panel);
            if (leftNode != null) return leftNode;
            var rightNode = Right.FindNode(panel);
            if (rightNode != null) return rightNode;
            return null;
        }
    }
    public class DockTabbedNode : DockNode
    {
        public List<IPanel> Panels { get; set; } = new List<IPanel>();
        public int ActiveIndex { get; set; } = -1;
        private float _titleHeight = 20f;
        public override void ComputeLayout(float x, float y, float w, float h)
        {
            Rect = new Vector4(x, y, w, h);
            foreach (var panel in Panels)
            {
                panel.Position = new Vector2(x, y);
                panel.Size = new Vector2(w, h);
                panel.OnPanelResize(w, h);
            }
        }
        public override bool HitTest(Vector2 mousePos, out IPanel hitPanel, out bool isTitle, out bool isSplitter, out bool isTab, out int tabIndex)
        {
            hitPanel = null;
            isTitle = false;
            isSplitter = false;
            isTab = false;
            tabIndex = -1;
            if (mousePos.X < Rect.X || mousePos.X > Rect.X + Rect.Z || mousePos.Y < Rect.Y || mousePos.Y > Rect.Y + Rect.W) return false;
            if (Panels.Count == 0) return false;
            if (mousePos.Y < Rect.Y + _titleHeight)
            {
                float tabWidth = Rect.Z / Panels.Count;
                tabIndex = (int)((mousePos.X - Rect.X) / tabWidth);
                if (tabIndex >= 0 && tabIndex < Panels.Count)
                {
                    isTab = true;
                    hitPanel = Panels[tabIndex];
                    return true;
                }
            }
            else
            {
                isTitle = false;
                hitPanel = Panels[ActiveIndex];
                return true;
            }
            return false;
        }
        public override bool Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus)
        {
            if (HitTest(mousePos, out IPanel hit, out bool isTitle, out bool isSplitter, out bool isTab, out int tabIndex))
            {
                if (isTab && mousePressed)
                {
                    ActiveIndex = tabIndex;
                    return true;
                }
                if (ActiveIndex >= 0 && ActiveIndex < Panels.Count)
                {
                    Panels[ActiveIndex].Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
                    return true;
                }
            }
            return false;
        }
        public override void Render(IRenderContext renderContext, int winW, int winH)
        {
            if (ActiveIndex < 0 || ActiveIndex >= Panels.Count) return;
            int px = (int)Rect.X;
            int py = (int)(winH - Rect.Y - Rect.W);
            uint pw = (uint)Rect.Z;
            uint ph = (uint)Rect.W;
            renderContext.Scissor(px, py, pw, ph);
            renderContext.Viewport(px, py, pw, ph);
            Panels[ActiveIndex].Render();
        }
        public override void AddPanel(IPanel panel)
        {
            Panels.Add(panel);
            ActiveIndex = Panels.Count - 1;
        }
        public override bool RemovePanel(IPanel panel)
        {
            int idx = Panels.IndexOf(panel);
            if (idx >= 0)
            {
                Panels.RemoveAt(idx);
                if (ActiveIndex >= Panels.Count)
                {
                    ActiveIndex = Panels.Count - 1;
                }
                return true;
            }
            return false;
        }
        public override DockNode FindNode(IPanel panel)
        {
            if (Panels.Contains(panel)) return this;
            return null;
        }
    }
    public class DockManager
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
        public DockManager(IRenderContext renderContext, IControlContext controlContext, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _eventBus = eventBus;
            _root = new DockTabbedNode();
        }
        public void AddPanel(IPanel panel)
        {
            if (panel.DockState == DockState.Floating)
            {
                _floatingPanels.Add(panel);
                panel.AllowDragging = true;
            }
            else
            {
                _root.AddPanel(panel);
                panel.AllowDragging = false;
            }
        }
        public void RemovePanel(IPanel panel)
        {
            if (_floatingPanels.Remove(panel))
            {
                if (_draggingFloatingPanel == panel) _draggingFloatingPanel = null;
                return;
            }
            if (_root.RemovePanel(panel))
            {
            }
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
            }
            _root.ComputeLayout(0, 0, winW, winH);
            // ABSOLUTE HIGHEST PRIORITY - drag continuation for ALL panels (including modal FileSelectorPanel)
            if (_draggingFloatingPanel != null)
            {
                _draggingFloatingPanel.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
                if (mouseReleased)
                {
                    _draggingFloatingPanel = null;
                }
                return; // Skip everything else while dragging - this is why it's smooth
            }
            bool handled = false;
            // Modal handling (FileSelectorPanel stays modal but drag works)
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
                    topModal.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
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
                    Vector2 rel = mousePos - panel.Position;
                    bool over = rel.X >= 0 && rel.X <= panel.Size.X && rel.Y >= 0 && rel.Y <= panel.Size.Y;
                    if (over)
                    {
                        panel.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
                        if (mousePressed && panel.AllowDragging && panel.DockState == DockState.Floating)
                        {
                            bool overTitle = mousePos.Y >= panel.Position.Y && mousePos.Y <= panel.Position.Y + 20f;
                            if (overTitle)
                            {
                                _draggingFloatingPanel = panel;
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
                if (mouseReleased)
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
            }
            else
            {
                if (_root.HitTest(mousePos, out IPanel hitPanel, out bool isTitle, out _, out _, out _))
                {
                    if (isTitle && mousePressed)
                    {
                        _draggingPanel = hitPanel;
                        _dragOffset = mousePos - hitPanel.Position;
                        _dragOriginNode = _root.FindNode(hitPanel);
                        _floatingPanels.Add(hitPanel);
                        _dragOriginNode.RemovePanel(hitPanel);
                        hitPanel.DockState = DockState.Floating;
                        hitPanel.AllowDragging = true;
                    }
                }
            }
        }
        private DockState GetDockStateFromPosition(Vector2 mousePos, int winW, int winH)
        {
            if (mousePos.X < SnapDistance) return DockState.DockedLeft;
            if (mousePos.X > winW - SnapDistance) return DockState.DockedRight;
            if (mousePos.Y < SnapDistance) return DockState.DockedTop;
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
                panel.Render();
            }
        }
    }
}