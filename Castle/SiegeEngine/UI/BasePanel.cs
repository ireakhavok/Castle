// Folder: SiegeEngine.UI
// File: BasePanel.cs
using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Rendering;
using System;
using System.Numerics;

namespace SiegeEngine.UI
{
    public enum ScalingMode { Fill, BestFit }

    public abstract class BasePanel : IPanel
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly IntPtr _window;
        protected readonly EventBus _eventBus;
        protected UIOverlay _uiOverlay;
        protected int _lastW;
        protected int _lastH;
        public DockState DockState { get; set; } = DockState.Floating;
        public Vector2 Position { get; set; } = Vector2.Zero;
        public Vector2 Size { get; set; } = new Vector2(800, 600);
        public bool Visible { get; set; } = true;
        protected bool _isDragging;
        protected Vector2 _dragOffset;
        protected double _lastClickTime;
        protected const float TitleHeight = 20f;
        protected const double DoubleClickTime = 0.5;
        protected ScalingMode Scaling = ScalingMode.Fill;
        protected float BaseWidth = 800f;
        protected float BaseHeight = 600f;
        protected virtual bool AllowDragging { get; set; } = true;
        protected UIQuadRenderer _quadRenderer;

        protected BasePanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus)
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

            bool overPanel = absMousePos.X >= Position.X && absMousePos.X <= Position.X + Size.X &&
                             absMousePos.Y >= Position.Y && absMousePos.Y <= Position.Y + Size.Y;

            if (DockState != DockState.Floating || !overPanel)
            {
                return;
            }

            bool overTitle = absMousePos.Y >= Position.Y && absMousePos.Y <= Position.Y + TitleHeight;

            if (AllowDragging && mousePressed && overTitle)
            {
                double currentTime = _controlContext.GetTime();
                if (currentTime - _lastClickTime < DoubleClickTime)
                {
                    // Maximize
                    _controlContext.GetWindowSize(_window, out int w, out int h);
                    Position = Vector2.Zero;
                    Size = new Vector2(w, h);
                    _lastClickTime = 0;
                    _uiOverlay.PanelWidth = Size.X;
                    _uiOverlay.PanelHeight = Size.Y;
                    _uiOverlay.RefreshUI();
                }
                else
                {
                    _isDragging = true;
                    _dragOffset = absMousePos - Position;
                    _lastClickTime = currentTime;
                }
            }

            if (_isDragging && mouseDown)
            {
                Position = absMousePos - _dragOffset;
            }

            if (mouseReleased)
            {
                _isDragging = false;
            }

            Vector2 relMousePos = absMousePos - Position;
            bool uCurrentMouseDown = mouseDown && overPanel;
            bool uMousePressed = mousePressed && overPanel;
            bool uMouseReleased = mouseReleased && overPanel;
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.Update(deltaTime, relMousePos, uCurrentMouseDown, Size.X, Size.Y);
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

            _renderContext.ClearColor(0.118f, 0.118f, 0.118f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Disable(_renderContext.Enums.DepthTest);

            // Render title bar
            _quadRenderer.DrawQuad(0, 0, Size.X, TitleHeight, new Vector4(0.2f, 0.2f, 0.2f, 1.0f), Size.X, Size.Y);

            // Render UI (shifted down by title height? No, assume UI layout includes top margin or adjust in HTML/CSS)

            _uiOverlay.Render();

            // Render 2px border
            float bw = 2f;
            Vector4 bc = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            // Top (but title covers)
            // Bottom
            _quadRenderer.DrawQuad(0, Size.Y - bw, Size.X, bw, bc, Size.X, Size.Y);
            // Left
            _quadRenderer.DrawQuad(0, 0, bw, Size.Y, bc, Size.X, Size.Y);
            // Right
            _quadRenderer.DrawQuad(Size.X - bw, 0, bw, Size.Y, bc, Size.X, Size.Y);

            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public virtual void Dispose()
        {
            _uiOverlay.Dispose();
        }

        public virtual void Detach()
        {
        }

        public virtual void OnPanelResize(float w, float h)
        {
            if (!AllowDragging)
            {
                Size = new Vector2(w, h);
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
        }
    }
}