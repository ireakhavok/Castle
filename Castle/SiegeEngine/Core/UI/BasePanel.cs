// Folder: SiegeEngine/Core/UI
// File: BasePanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
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
        public UIOverlay _uiOverlay;
        protected int _lastW;
        protected int _lastH;
        public DockState DockState { get; set; } = DockState.Floating;
        public DockingMode DockingMode { get; set; } = DockingMode.Desktop;
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
        protected ScalingMode Scaling = ScalingMode.Fill;
        protected float BaseWidth = 800f;
        protected float BaseHeight = 600f;
        public bool AllowDragging { get; set; } = true;
        protected UIQuadRenderer _quadRenderer;
        private LayeredUIRenderer _layeredRenderer;
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
        public PanelChrome chrome;
        public bool HasTitleBar { get; set; } = false;
        public bool IsClosable { get; set; } = false;
        public int RenderOrder { get; set; } = 0;

        public static bool MouseReleasedConsumedThisFrame { get; set; } = false;

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
            _layeredRenderer = new LayeredUIRenderer(_renderContext, _controlContext, _quadRenderer);
            Size = new Vector2(BaseWidth, BaseHeight);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            if (HasTitleBar)
            {
                chrome = new PanelChrome(this);
                HeaderHeight = TitleHeight;
            }
            _uiOverlay.ReservedHeaderHeight = HeaderHeight;
            _uiOverlay.RefreshUI();
        }

        public virtual bool IsMouseOver(Vector2 absMousePos)
        {
            if (!Visible) return false;
            return absMousePos.X >= Position.X && absMousePos.X <= Position.X + Size.X &&
                   absMousePos.Y >= Position.Y && absMousePos.Y <= Position.Y + Size.Y;
        }

        public virtual void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            if (!Visible) return;

            if (HasTitleBar && chrome != null)
            {
                if (chrome.HandleUpdate(absMousePos, mousePressed, mouseReleased))
                {
                    MouseReleasedConsumedThisFrame = true;
                    return;
                }
            }

            if (_isDragging)
            {
                if (mouseDown)
                {
                    Position = absMousePos - _dragOffset;
                }
                if (mouseReleased)
                {
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
                    OnLiveResize(Size.X, Size.Y);
                }
                if (mouseReleased)
                {
                    _currentResizeHandle = ResizeHandle.None;
                    OnPanelResize(Size.X, Size.Y);
                }
                return;
            }

            bool overPanel = IsMouseOver(absMousePos);
            bool isTopmost = PanelManager.Current?.GetTopmostPanelAt(absMousePos) == this;

            if (isTopmost && overPanel && mousePressed)
            {
                OnContentFocusGained();
            }

            // UIOverlay always receives input when this panel is topmost
            // (select dropdowns, options, etc. are handled inside _uiOverlay.Update)
            if (isTopmost)
            {
                Vector2 relMousePos = absMousePos - Position;
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.Scroll(scrollDelta);
                _uiOverlay.Update(deltaTime, relMousePos, mouseDown, Size.X, Size.Y);
            }
        }

        public virtual void OnContentFocusGained()
        {
            Console.WriteLine($"[BasePanel] OnContentFocusGained called on {GetType().Name} (default no-op)");
        }

        public virtual void ToggleCameraMode() { }

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
        public virtual void Render()
        {
            if (!Visible) return;
            if (_lastW != (int)Size.X || _lastH != (int)Size.Y)
            {
                _lastW = (int)Size.X;
                _lastH = (int)Size.Y;
                OnLiveResize(Size.X, Size.Y);
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            _layeredRenderer.RenderPanel(this);
        }
        protected internal virtual void RenderContentLayer()
        {
            RenderInnerContent();
            if (_uiOverlay != null)
            {
                _uiOverlay.Render();
            }
        }
        protected virtual void RenderInnerContent()
        {
        }
        public virtual void Dispose()
        {
            _uiOverlay.Dispose();
            if (chrome != null) chrome.Dispose();
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
        public void ResetDragState()
        {
            _isDragging = false;
        }
        public void Close()
        {
            _eventBus.Publish(new ClosePanelEvent(this));
        }
        public bool IsOverCloseButton(Vector2 mousePos)
        {
            if (!IsClosable || !HasTitleBar) return false;
            float closeX = Position.X + Size.X - 24f;
            return mousePos.X >= closeX && mousePos.X <= Position.X + Size.X &&
                   mousePos.Y >= Position.Y && mousePos.Y <= Position.Y + TitleHeight;
        }
        public nint WindowHandle => _window;
        protected internal UIQuadRenderer QuadRenderer => _quadRenderer;
    }
}