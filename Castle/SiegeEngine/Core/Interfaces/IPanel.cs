// Folder: SiegeEngine.Core.Interfaces
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
        Tabbed,
        DockedHeader
    }

    public enum ResizeHandle
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public interface IPanel
    {
        void Init();
        void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f);
        void Render();
        void Dispose();
        DockState DockState { get; set; }
        void Detach();
        Vector2 Position { get; set; }
        Vector2 Size { get; set; }
        bool Visible { get; set; }
        void OnPanelResize(float w, float h);
        bool AllowDragging { get; set; }
        bool IsModal { get; set; }
        float HeaderHeight { get; set; }

        ResizeHandle GetResizeHandle(Vector2 absMousePos);
        void StartResize(Vector2 mousePos, ResizeHandle handle);
    }
}