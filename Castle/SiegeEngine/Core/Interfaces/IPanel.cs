// Folder: SiegeEngine.Core.Interfaces
// File: IPanel.cs
using SiegeEngine.Core.Definitions;
using System.Numerics;
using System.Text.Json;

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
        DockingMode DockingMode { get; set; }
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
        bool IsOverCloseButton(Vector2 mousePos);
        void Close();
        nint WindowHandle { get; }

        // Clean core abstraction: allows PanelManager to toggle camera mode on the true topmost panel only
        // This is the ONLY way to handle Tab globally without circular dependencies or panel-specific code
        void ToggleCameraMode();

        // === CENTRALIZED MOUSE-OVER LOGIC (first iterative step only) ===
        // All "is mouse over this panel" checks are now consolidated here.
        // BasePanel will provide the basic geometric test.
        // IDEBasePanel will override to also include open nav dropdowns.
        // No other files are modified in this step.
        bool IsMouseOver(Vector2 absMousePos);
    }

    // Lightweight opt-in interface for automatic per-project panel state persistence.
    // Lives in core (no CastleBuilder dependency). Uses only System.Text.Json.
    // Panels implement this to snapshot/restore their internal runtime state (selected objects,
    // tree expansions, active brushes, SceneData references, etc.) automatically on project
    // load, context switch, and explicit save. Memory-first until FlushAllToDisk.
    public interface IDataAwarePanel
    {
        string DataKey { get; }
        JsonElement SavePanelState();
        void LoadPanelState(JsonElement state);
    }
}