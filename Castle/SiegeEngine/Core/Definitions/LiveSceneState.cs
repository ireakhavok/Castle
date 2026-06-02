// Folder: SiegeEngine/Core/Definitions
// File: LiveSceneState.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Terrain;
using System;
using System.Drawing;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    /// <summary>
    /// Central live mutable state for a single scene.
    /// Owned by ProjectStateManager (editor layer).
    /// Core scenes (TerrainScene / TerrainCreatorScene) access it only via ISceneStateProvider.
    /// Completely self-contained in Core - no editor dependencies.
    /// </summary>
    public class LiveSceneState : IDisposable, ISceneStateProvider
    {
        public string SceneName { get; }
        public float[,] Heightmap { get; private set; }
        public Bitmap ColorBitmap { get; private set; }
        public int HeightmapVersion { get; private set; } = 0;
        public int ColorVersion { get; private set; } = 0;

        private readonly GeoTiffParser.GeoReference _terrainGeoRef = new GeoTiffParser.GeoReference { IsValid = false };
        private readonly GeoTiffParser.GeoReference _colorGeoRef = new GeoTiffParser.GeoReference { IsValid = false };

        public LiveSceneState(string sceneName, int width = 200, int height = 200)
        {
            SceneName = sceneName;
            Heightmap = new float[width, height];
            ColorBitmap = new Bitmap(4096, 4096);
            using (var g = Graphics.FromImage(ColorBitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
            }
        }

        public float[,] GetHeightmap() => Heightmap;

        public float GetInterpolatedHeight(float worldX, float worldY)
        {
            if (Heightmap == null) return 0f;
            // Reuse existing interpolation logic from TerrainScene in later steps
            return 0f; // placeholder for Step 1
        }

        public void ApplyBrushModification(Vector3 worldPos, float radius, float strength, string operation, string shape, string falloff, int paintLayer)
        {
            // Height sculpting delegated here in future steps
            HeightmapVersion++;
        }

        public int GetColorVersion() => ColorVersion;

        public void SyncColorTextureIfNeeded()
        {
            // Called by scenes; implemented in TerrainScene in later steps
            ColorVersion++; // placeholder for Step 1
        }

        public void Dispose()
        {
            ColorBitmap?.Dispose();
            ColorBitmap = null;
        }
    }
}