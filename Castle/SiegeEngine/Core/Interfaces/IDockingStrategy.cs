// Folder: SiegeEngine.Core.Managers
// File: IDockingStrategy.cs
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using System.Numerics;

namespace SiegeEngine.Core.Managers
{
    public interface IDockingStrategy
    {
        void AddPanel(IPanel panel);
        void RemovePanel(IPanel panel);
        void Update(float deltaTime, Vector2 mousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta, EventBus eventBus, int winW, int winH);
        void Render(IRenderContext renderContext, int winW, int winH);
        void ComputeLayout(int winW, int winH);
        bool HasActiveContent();

        // MINIMAL ADDITION ONLY – no other changes
        // Returns the single uppermost panel under the mouse (modals/floaters/docked)
        // Used by PanelManager to guarantee exactly one panel receives content updates
        IPanel GetTopmostPanelAt(Vector2 mousePos);
    }
}