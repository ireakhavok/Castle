// Folder: SiegeEngine.Core.Managers
// File: CaptureManager.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering.ContextManagement;
using System.Numerics;

namespace SiegeEngine.Core.Managers
{
    public class CaptureManager
    {
        private readonly IControlContext _controlContext;
        private IPanel _currentOwner;
        public IPanel CurrentOwner => _currentOwner;
        public bool IsCapturing => _currentOwner != null;

        private Vector2 _captureCenter;
        private bool _wasCapturingLastFrame;

        public CaptureManager(IControlContext controlContext)
        {
            _controlContext = controlContext;
        }

        public void RequestCapture(IPanel panel)
        {
            if (_currentOwner == panel) return;
            ReleaseCapture();

            _currentOwner = panel;

            float contentY = panel.Position.Y + panel.HeaderHeight;
            float contentH = panel.Size.Y - panel.HeaderHeight;
            _captureCenter = new Vector2(
                panel.Position.X + panel.Size.X * 0.5f,
                contentY + contentH * 0.5f
            );

            var viewport = new Viewport(panel.Position.X, contentY, panel.Size.X, contentH);
            _controlContext.PushViewport(viewport);

            _controlContext.SetInputMode(panel.WindowHandle, CursorAttribute.Cursor, CursorMode.Disabled);
            _controlContext.SetCursorPos(panel.WindowHandle, _captureCenter.X, _captureCenter.Y);

            _wasCapturingLastFrame = true;
        }

        public void ReleaseCapture()
        {
            if (_currentOwner != null)
            {
                _controlContext.PopViewport();

                if (_wasCapturingLastFrame)
                {
                    _controlContext.SetInputMode(_currentOwner.WindowHandle, CursorAttribute.Cursor, CursorMode.Normal);
                    _wasCapturingLastFrame = false;
                }

                _currentOwner = null;
            }
        }

        public void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta)
        {
            if (_currentOwner != null && _currentOwner.Visible)
            {
                _currentOwner.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            }
        }
    }
}