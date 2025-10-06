// Folder: SiegeEngine.Interfaces
// File: IPanel.cs
namespace SiegeEngine.Interfaces
{
    public enum DockState
    {
        Floating,
        DockedLeft,
        Tabbed
    }

    public interface IPanel
    {
        void Init();
        void Update(float deltaTime);
        void Render();
        void Dispose();
        DockState DockState { get; set; }
        void Detach();
    }
}