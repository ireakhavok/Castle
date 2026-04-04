// Folder: SiegeEngine.Core.Managers
// File: CaptureManager.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;
using System.Numerics;

namespace SiegeEngine.Core.Managers
{
    public class CaptureManager
    {
        private readonly IControlContext _controlContext;
        private IPanel _currentOwner;
        public IPanel CurrentOwner => _currentOwner;
        public bool IsCapturing => _currentOwner != null;

        private Vector2 _captureCenter;           // center of content area (no header)
        private bool _wasCapturingLastFrame;      // track state for clean restore

        public CaptureManager(IControlContext controlContext)
        {
            _controlContext = controlContext;
        }

        public void RequestCapture(IPanel panel)
        {
            if (_currentOwner == panel) return;
            ReleaseCapture();

            _currentOwner = panel;

            // Calculate content area (excluding title bar)
            float contentY = panel.Position.Y + panel.HeaderHeight;
            float contentH = panel.Size.Y - panel.HeaderHeight;
            _captureCenter = new Vector2(
                panel.Position.X + panel.Size.X * 0.5f,
                contentY + contentH * 0.5f
            );

            var viewport = new Viewport(panel.Position.X, contentY, panel.Size.X, contentH);
            _controlContext.PushViewport(viewport);

            // HARDWARE LOCK - first iteration (smallest possible)
            _controlContext.SetInputMode(panel.WindowHandle, CursorAttribute.Cursor, CursorMode.Disabled);
            _controlContext.SetCursorPos(panel.WindowHandle, _captureCenter.X, _captureCenter.Y);

            _wasCapturingLastFrame = true;
        }

        public void ReleaseCapture()
        {
            if (_currentOwner != null)
            {
                _controlContext.PopViewport();

                // Restore normal cursor (only if we locked it)
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
                // FORCE RECENTER every frame - stops escape on fast movement
                // (recenter happens AFTER panel.Update so deltas are still clean)
                // REMOVED this line for this iteration (FlyCameraController now handles recentering again)
                // _controlContext.SetCursorPos(_currentOwner.WindowHandle, _captureCenter.X, _captureCenter.Y);

                _currentOwner.Update(deltaTime, mousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            }
        }
    }
}