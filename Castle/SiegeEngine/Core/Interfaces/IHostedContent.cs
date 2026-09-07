// Folder: SiegeEngine/Core/Interfaces
// File: IHostedContent.cs
namespace SiegeEngine.Core.Interfaces
{
    /// <summary>
    /// Content-only surface for game HUD and hosted views.
    /// Chrome (window handle, title bar, resize, ToggleCameraMode) stays on IPanel.
    /// Game assemblies implement this; the host wraps it in BasePanel.
    /// </summary>
    public interface IHostedContent
    {
        void Init();
        void Update(float deltaTime);
        void Render();
        void Dispose();
        string DataKey { get; }
    }
}
