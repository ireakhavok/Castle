// Folder: SiegeEngine.Core.Interfaces
// File: IPanel.cs
using SiegeEngine.Core.Definitions;
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
        DockingMode DockingMode { get; set; }   // Per-panel control - no scene conflict
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
        bool HasTitleBar { get; set; }
        bool IsClosable { get; set; }
        void StartTitleBarDrag(Vector2 mousePos);

        // Clean architectural addition: centralised close-button hit test so strategy no longer duplicates math
        bool IsOverCloseButton(Vector2 mousePos);

        // REQUIRED: DockTabbedNode (and therefore the DockManager system) now controls closing
        void Close();

        // NEW (minimal addition for CaptureManager hardware lock)
        nint WindowHandle { get; }
    }
}