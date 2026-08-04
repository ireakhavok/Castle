// Folder: SiegeEngine/Core/Physics
// File: IHeightProvider.cs
using System;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Thin query interface so PhysicsWorld never owns or copies heightmap data.
    /// Implemented by LiveSceneState and adapted by TerrainScene.
    /// </summary>
    public interface IHeightProvider
    {
        float GetInterpolatedHeight(float worldX, float worldY);
        float WorldScaleX { get; }
        float WorldScaleZ { get; }
        int Width { get; }
        int Height { get; }
    }
}