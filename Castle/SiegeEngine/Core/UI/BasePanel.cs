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
        public const float TitleHeight = 20f;
        protected const double DoubleClickTime = 0.5;
        protected const float SnapDistance = 20f;
        protected const float MinDragDistanceForSnap = 10f;
        protected ScalingMode Scaling = ScalingMode.Fill;
        protected float BaseWidth = 800f;
        protected float BaseHeight = 600f;
        public bool AllowDragging { get; set; } = true;
        protected UIQuadRenderer _quadRenderer;
        private ResizeHandle _currentResizeHandle = ResizeHandle.None;
        private Vector2 _resizeStartMousePos;
        private Vector2 _resizeStartPosition;
        private Vector2 _resizeStartSize;
        public float HeaderHeight { get; set; } = 0f;
        protected bool IsResizing => _currentResizeHandle != ResizeHandle.None;
        public virtual bool WantsContinuousUpdate => false;
        private bool _dockable = true;
        public virtual bool Dockable
        {
            get => _dockable;
            set => _dockable = value;
        }
        private PanelChrome _chrome;
        public bool HasTitleBar { get; set; } = false;
        public bool IsClosable { get; set; } = false;

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
            if (HasTitleBar)
            {
                _chrome = new PanelChrome(this);
                HeaderHeight = TitleHeight;
            }
            _uiOverlay.ReservedHeaderHeight = HeaderHeight;
            _uiOverlay.RefreshUI();
        }

        public virtual void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            if (!Visible) return;

            if (HasTitleBar && _chrome != null)
            {
                if (_chrome.HandleUpdate(absMousePos, mousePressed, mouseReleased))
                    return;
            }

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
                return;
            }

            if (_currentResizeHandle != ResizeHandle.None)
            {
                if (mouseDown)
                {
                    Vector2 delta = absMousePos - _resizeStartMousePos;
                    Vector2 newPos = _resizeStartPosition;
                    Vector2 newSize = _resizeStartSize;
                    switch (_currentResizeHandle)
                    {
                        case ResizeHandle.Left:
                            newSize.X = _resizeStartSize.X - delta.X;
                            newPos.X = _resizeStartPosition.X + _resizeStartSize.X - newSize.X;
                            break;
                        case ResizeHandle.Right:
                            newSize.X = _resizeStartSize.X + delta.X;
                            break;
                        case ResizeHandle.Top:
                            newSize.Y = _resizeStartSize.Y - delta.Y;
                            newPos.Y = _resizeStartPosition.Y + _resizeStartSize.Y - newSize.Y;
                            break;
                        case ResizeHandle.Bottom:
                            newSize.Y = _resizeStartSize.Y + delta.Y;
                            break;
                        case ResizeHandle.TopLeft:
                            newSize.X = _resizeStartSize.X - delta.X;
                            newPos.X = _resizeStartPosition.X + _resizeStartSize.X - newSize.X;
                            newSize.Y = _resizeStartSize.Y - delta.Y;
                            newPos.Y = _resizeStartPosition.Y + _resizeStartSize.Y - newSize.Y;
                            break;
                        case ResizeHandle.TopRight:
                            newSize.X = _resizeStartSize.X + delta.X;
                            newSize.Y = _resizeStartSize.Y - delta.Y;
                            newPos.Y = _resizeStartPosition.Y + _resizeStartSize.Y - newSize.Y;
                            break;
                        case ResizeHandle.BottomLeft:
                            newSize.X = _resizeStartSize.X - delta.X;
                            newPos.X = _resizeStartPosition.X + _resizeStartSize.X - newSize.X;
                            newSize.Y = _resizeStartSize.Y + delta.Y;
                            break;
                        case ResizeHandle.BottomRight:
                            newSize.X = _resizeStartSize.X + delta.X;
                            newSize.Y = _resizeStartSize.Y + delta.Y;
                            break;
                    }
                    newSize.X = Math.Max(newSize.X, 200f);
                    newSize.Y = Math.Max(newSize.Y, 150f);
                    Position = newPos;
                    Size = newSize;
                    OnPanelResize(Size.X, Size.Y);
                    OnLiveResize(Size.X, Size.Y);
                }
                if (mouseReleased)
                {
                    _currentResizeHandle = ResizeHandle.None;
                }
                return;
            }

            bool overPanel = absMousePos.X >= Position.X && absMousePos.X <= Position.X + Size.X &&
                             absMousePos.Y >= Position.Y && absMousePos.Y <= Position.Y + Size.Y;
            if (overPanel || WantsContinuousUpdate)
            {
                Vector2 relMousePos = absMousePos - Position;
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.Scroll(scrollDelta);
                _uiOverlay.Update(deltaTime, relMousePos, mouseDown, Size.X, Size.Y);
            }
        }

        public ResizeHandle GetResizeHandle(Vector2 absMousePos)
        {
            float left = absMousePos.X - Position.X;
            float right = Position.X + Size.X - absMousePos.X;
            float top = absMousePos.Y - Position.Y;
            float bottom = Position.Y + Size.Y - absMousePos.Y;
            const float grip = 8f;
            float titleH = HasTitleBar ? TitleHeight : 0f;
            if (left < grip && top < grip) return ResizeHandle.TopLeft;
            if (right < grip && top < grip) return ResizeHandle.TopRight;
            if (left < grip && bottom < grip) return ResizeHandle.BottomLeft;
            if (right < grip && bottom < grip) return ResizeHandle.BottomRight;
            if (left < grip) return ResizeHandle.Left;
            if (right < grip) return ResizeHandle.Right;
            if (top < grip && top > titleH) return ResizeHandle.Top;
            if (bottom < grip) return ResizeHandle.Bottom;
            return ResizeHandle.None;
        }

        public void StartResize(Vector2 mousePos, ResizeHandle handle)
        {
            _currentResizeHandle = handle;
            _resizeStartMousePos = mousePos;
            _resizeStartPosition = Position;
            _resizeStartSize = Size;
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

            if (HasTitleBar && _chrome != null)
            {
                _chrome.Render(_quadRenderer, Size.X, Size.Y);
            }

            if (_isDragging || IsResizing)
            {
                _quadRenderer.DrawQuad(0, HeaderHeight, Size.X, Size.Y - HeaderHeight, new Vector4(0.15f, 0.15f, 0.15f, 0.70f), Size.X, Size.Y);
                if (IsResizing)
                {
                    _quadRenderer.DrawQuad(0, 0, Size.X, Size.Y, new Vector4(0.3f, 0.8f, 1.0f, 0.25f), Size.X, Size.Y);
                }
            }
            else
            {
                _controlContext.GetWindowSize(_window, out int winW, out int winH);
                _renderContext.Enable(_renderContext.Enums.ScissorTest);
                int scissorX = (int)Position.X;
                int scissorY = winH - (int)(Position.Y + Size.Y);
                uint scissorW = (uint)Size.X;
                uint scissorH = (uint)Size.Y;
                _renderContext.Scissor(scissorX, scissorY, scissorW, scissorH);
                _uiOverlay.Render();
                _renderContext.Disable(_renderContext.Enums.ScissorTest);
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
            if (_chrome != null) _chrome.Dispose();
        }

        public virtual void Detach() { }

        public virtual void OnPanelResize(float w, float h)
        {
            Size = new Vector2(w, h);
            if (DockState == DockState.Floating && Position.Y < HeaderHeight)
            {
                Position = new Vector2(Position.X, HeaderHeight);
            }
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.ReservedHeaderHeight = HeaderHeight;
            _uiOverlay.RefreshUI();
        }

        public virtual void OnLiveResize(float w, float h)
        {
        }

        public void StartTitleBarDrag(Vector2 mousePos)
        {
            _isDragging = true;
            _dragOffset = mousePos - Position;
            _dragStartMousePos = mousePos;
            _lastClickTime = _controlContext.GetTime();
        }

        /// <summary>
        /// Public API for any chrome or external code to request panel close.
        /// Keeps _eventBus fully encapsulated.
        /// </summary>
        public void Close()
        {
            _eventBus.Publish(new ClosePanelEvent(this));
        }
    }
}