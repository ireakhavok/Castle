// Folder: SiegeEngine/Core/Interfaces
// File: IPlayViewport.cs
using System.Numerics;

namespace SiegeEngine.Core.Interfaces
{
    public interface IPlayViewport
    {
        bool IsPlaying { get; }
        Vector2 ViewportPosition { get; }
        Vector2 ViewportSize { get; }
    }
}
