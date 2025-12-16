// Folder: SiegeEngine.Interfaces
// File: IPanel.cs
using System.Numerics;

namespace SiegeEngine.Core.Interfaces
{
    public enum DockState
    {
        Floating,
        DockedLeft,
        DockedRight,
        DockedTop,
        DockedBottom,
        Tabbed
    }

    public interface IPanel
    {
        void Init();
        void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased);
        void Render();
        void Dispose();
        DockState DockState { get; set; }
        void Detach();
        Vector2 Position { get; set; }
        Vector2 Size { get; set; }
        bool Visible { get; set; }
        void OnPanelResize(float w, float h);
        bool AllowDragging { get; set; }
    }
}