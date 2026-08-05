// Folder: SiegeEngine/Core/Physics
// File: HeightmapAdapter.cs
using System;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Thin IHeightProvider over a raw float[,] heightmap.
    /// Used by pure-runtime paths (RuntimeGameplayScene) that do not own a LiveSceneState.
    /// Bilinear sampling matches LiveSceneState.
    /// </summary>
    public class HeightmapAdapter : IHeightProvider
    {
        private readonly float[,] _heightmap;

        public float WorldScaleX { get; }
        public float WorldScaleZ { get; }

        public int Width => _heightmap != null ? _heightmap.GetLength(0) : 0;
        public int Height => _heightmap != null ? _heightmap.GetLength(1) : 0;

        public HeightmapAdapter(float[,] heightmap, float worldScaleX = 1.0f, float worldScaleZ = 1.0f)
        {
            _heightmap = heightmap ?? throw new ArgumentNullException(nameof(heightmap));
            WorldScaleX = worldScaleX;
            WorldScaleZ = worldScaleZ;
        }

        public float GetInterpolatedHeight(float worldX, float worldY)
        {
            if (_heightmap == null) return 0f;

            float fx = worldX / WorldScaleX;
            float fy = worldY / WorldScaleZ;
            int w = _heightmap.GetLength(0);
            int h = _heightmap.GetLength(1);

            int x0 = (int)Math.Clamp(Math.Floor(fx), 0, w - 1);
            int y0 = (int)Math.Clamp(Math.Floor(fy), 0, h - 1);
            int x1 = Math.Min(x0 + 1, w - 1);
            int y1 = Math.Min(y0 + 1, h - 1);

            float tx = fx - x0;
            float ty = fy - y0;

            float h00 = _heightmap[x0, y0];
            float h10 = _heightmap[x1, y0];
            float h01 = _heightmap[x0, y1];
            float h11 = _heightmap[x1, y1];

            float h0 = h00 * (1f - tx) + h10 * tx;
            float h1 = h01 * (1f - tx) + h11 * tx;
            return h0 * (1f - ty) + h1 * ty;
        }
    }
}