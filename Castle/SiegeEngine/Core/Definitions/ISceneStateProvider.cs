// Folder: SiegeEngine/Core/Definitions
// File: ISceneStateProvider.cs
using SiegeEngine.Core.Definitions;
using System;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    /// <summary>
    /// Narrow, pure interface for core engine scenes to access live terrain state
    /// without knowing about ProjectSettings, ProjectStateManager, or any editor code.
    /// This preserves the strict IDE / Core boundary.
    /// </summary>
    public interface ISceneStateProvider
    {
        float[,] GetHeightmap();
        float GetInterpolatedHeight(float worldX, float worldY);
        void ApplyBrushModification(Vector3 worldPos, float radius, float strength, string operation, string shape, string falloff, int paintLayer); // primitives only - core pure
        int GetColorVersion();
        void SyncColorTextureIfNeeded(); // protected hook in TerrainScene
    }
}