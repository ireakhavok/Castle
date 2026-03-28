using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
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
        private readonly IDockingStrategy _strategy;
        public DockManager(IRenderContext renderContext, IControlContext controlContext, EventBus eventBus)
        {
            _strategy = new DesktopDockingStrategy(renderContext, controlContext, eventBus);
        }
        public void AddPanel(IPanel panel) => _strategy.AddPanel(panel);
        public void RemovePanel(IPanel panel) => _strategy.RemovePanel(panel);
        public void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus, int winW, int winH)
        {
            _strategy.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta, eventBus, winW, winH);
        }
        public void Render(IRenderContext renderContext, int winW, int winH)
        {
            _strategy.Render(renderContext, winW, winH);
        }
        public void ComputeLayout(int winW, int winH)
        {
            _strategy.ComputeLayout(winW, winH);
        }
    }
}