// Folder: SiegeEngine.Core.Managers
// File: IDockingStrategy.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
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
    }
}