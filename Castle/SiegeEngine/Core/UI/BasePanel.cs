// Folder: SiegeEngine.Core.UI
// File: BasePanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Definitions;
using System;
using System.Numerics;
namespace SiegeEngine.Core.UI
{
    public enum ScalingMode { Fill, BestFit }
    public abstract class BasePanel : IPanel
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly nint _window;
        protected readonly EventBus _eventBus;
        protected UIOverlay _uiOverlay;
        protected int _lastW;
        protected int _lastH;
        public DockState DockState { get; set; } = DockState.Floating;
        public Vector2 Position { get; set; } = Vector2.Zero;
        public Vector2 Size { get; set; } = new Vector2(800, 600);
        public bool Visible { get; set; } = true;
        public bool IsModal { get; set; } = false;
        protected bool _isDragging;
        protected Vector2 _dragOffset;
        protected Vector2 _dragStartMousePos;
        protected double _lastClickTime;
        protected const float TitleHeight = 20f;
        protected const double DoubleClickTime = 0.5;
        protected const float SnapDistance = 20f;
        protected const float MinDragDistanceForSnap = 10f;
        protected ScalingMode Scaling = ScalingMode.Fill;
        protected float BaseWidth = 800f;
        protected float BaseHeight = 600f;
        public bool AllowDragging { get; set; } = true;
        protected UIQuadRenderer _quadRenderer;

        protected BasePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
            _window = window;
            _eventBus = eventBus;
            _uiOverlay = CreateUIOverlay();
        }
        protected virtual UIOverlay CreateUIOverlay()
        {
            return new UIOverlay(_renderContext, _controlContext, _window);
        }
        public virtual void Init()
        {
            _uiOverlay.Init();
            _quadRenderer = new UIQuadRenderer(_renderContext);
            _controlContext.GetWindowSize(_window, out int w, out int h);
            Size = new Vector2(w, h);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }
        public virtual void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased)
        {
            if (!Visible) return;
            // Dragging continues as long as mouse button is held — no bounds check, no early return
            if (_isDragging)
            {
                if (mouseDown)
                {
                    Position = absMousePos - _dragOffset;
                }
                if (mouseReleased)
                {
                    float dragDist = Vector2.Distance(absMousePos, _dragStartMousePos);
                    if (dragDist > MinDragDistanceForSnap)
                    {
                        _controlContext.GetWindowSize(_window, out int winW, out int winH);
                        ApplySnap(absMousePos, winW, winH);
                    }
                    _isDragging = false;
                }
            }
            // Start dragging only when clicking title bar
            bool overTitle = absMousePos.Y >= Position.Y && absMousePos.Y <= Position.Y + TitleHeight;
            if (AllowDragging && DockState == DockState.Floating && mousePressed && overTitle)
            {
                double currentTime = _controlContext.GetTime();
                if (currentTime - _lastClickTime < DoubleClickTime)
                {
                    _controlContext.GetWindowSize(_window, out int w, out int h);
                    Position = Vector2.Zero;
                    Size = new Vector2(w, h);
                    _uiOverlay.PanelWidth = Size.X;
                    _uiOverlay.PanelHeight = Size.Y;
                    _uiOverlay.RefreshUI();
                    _lastClickTime = 0;
                }
                else
                {
                    _isDragging = true;
                    _dragOffset = absMousePos - Position;
                    _dragStartMousePos = absMousePos;
                    _lastClickTime = currentTime;
                }
            }
            // UI clicks only when mouse is over the panel
            bool overPanel = absMousePos.X >= Position.X && absMousePos.X <= Position.X + Size.X &&
                             absMousePos.Y >= Position.Y && absMousePos.Y <= Position.Y + Size.Y;
            if (overPanel && !_isDragging)
            {
                Vector2 relMousePos = absMousePos - Position;
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.Scroll(0f); // placeholder until real wheel delta is wired in next step
                _uiOverlay.Update(deltaTime, relMousePos, mouseDown, Size.X, Size.Y);
            }
        }
        protected void ApplySnap(Vector2 absMousePos, int winW, int winH)
        {
            float cornerZone = winH * 0.25f;
            bool nearLeft = absMousePos.X < SnapDistance;
            bool nearRight = absMousePos.X > winW - SnapDistance;
            bool nearTop = absMousePos.Y < SnapDistance;
            bool nearBottom = absMousePos.Y > winH - SnapDistance;
            bool inTopZone = absMousePos.Y < cornerZone;
            bool inBottomZone = absMousePos.Y > winH - cornerZone;
            Vector2 newPosition = Position;
            Vector2 newSize = Size;
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
            Position = newPosition;
            Size = newSize;
            OnPanelResize(newSize.X, newSize.Y);
        }
        public virtual void Render()
        {
            if (!Visible) return;
            if (_lastW != (int)Size.X || _lastH != (int)Size.Y)
            {
                _lastW = (int)Size.X;
                _lastH = (int)Size.Y;
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _quadRenderer.DrawQuad(0, 0, Size.X, TitleHeight, new Vector4(0.2f, 0.2f, 0.2f, 1.0f), Size.X, Size.Y);
            if (!_isDragging)
            {
                // === PANEL-LEVEL SCISSOR CLIPPING FOR CONTENT AREA (below title) ===
                _controlContext.GetWindowSize(_window, out int winW, out int winH);
                _renderContext.Enable(_renderContext.Enums.ScissorTest);
                int scissorX = (int)Position.X;
                int scissorY = winH - (int)(Position.Y + Size.Y);
                uint scissorW = (uint)Size.X;
                uint scissorH = (uint)(Size.Y - TitleHeight);
                _renderContext.Scissor(scissorX, scissorY, scissorW, scissorH);
                _uiOverlay.Render();
                _renderContext.Disable(_renderContext.Enums.ScissorTest);
            }
            else
            {
                _quadRenderer.DrawQuad(0, TitleHeight, Size.X, Size.Y - TitleHeight, new Vector4(0.15f, 0.15f, 0.15f, 0.70f), Size.X, Size.Y);
            }
            float bw = 2f;
            Vector4 bc = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            _quadRenderer.DrawQuad(0, Size.Y - bw, Size.X, bw, bc, Size.X, Size.Y);
            _quadRenderer.DrawQuad(0, 0, bw, Size.Y, bc, Size.X, Size.Y);
            _quadRenderer.DrawQuad(Size.X - bw, 0, bw, Size.Y, bc, Size.X, Size.Y);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }
        public virtual void Dispose()
        {
            _uiOverlay.Dispose();
        }
        public virtual void Detach() { }
        public virtual void OnPanelResize(float w, float h)
        {
            Size = new Vector2(w, h);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }
    }
}