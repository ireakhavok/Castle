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
            var viewport = new Viewport(panel.Position.X, contentY, panel.Size.X, contentH);
            _controlContext.PushViewport(viewport);
        }

        public void ReleaseCapture()
        {
            if (_currentOwner != null)
            {
                _controlContext.PopViewport();
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