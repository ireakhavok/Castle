// Folder: SiegeEngine/Core/Definitions
// File: LiveSceneState.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.Terrain;
using System;
using System.Drawing;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class LiveSceneState : IDisposable, ISceneStateProvider, IHeightProvider
    {
        public string SceneName { get; }
        public float[,] Heightmap { get; set; }
        public Bitmap ColorBitmap { get; set; }
        public SkyboxData Skybox { get; set; }
        public int HeightmapVersion { get; set; } = 0;
        public int ColorVersion { get; private set; } = 0;
        public int SkyboxVersion { get; private set; } = 0;

        // IHeightProvider scale (default 1 m/cell; TerrainScene / CustomTerrainParser can push real values later)
        public float WorldScaleX { get; set; } = 1.0f;
        public float WorldScaleZ { get; set; } = 1.0f;

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
            Skybox = null;
        }

        public float[,] GetHeightmap() => Heightmap;

        public int Width => Heightmap != null ? Heightmap.GetLength(0) : 0;
        public int Height => Heightmap != null ? Heightmap.GetLength(1) : 0;

        public float GetInterpolatedHeight(float worldX, float worldY)
        {
            if (Heightmap == null) return 0f;

            float fx = worldX / WorldScaleX;
            float fy = worldY / WorldScaleZ;
            int w = Heightmap.GetLength(0);
            int h = Heightmap.GetLength(1);

            int x0 = (int)Math.Clamp(Math.Floor(fx), 0, w - 1);
            int y0 = (int)Math.Clamp(Math.Floor(fy), 0, h - 1);
            int x1 = Math.Min(x0 + 1, w - 1);
            int y1 = Math.Min(y0 + 1, h - 1);

            float tx = fx - x0;
            float ty = fy - y0;

            float h00 = Heightmap[x0, y0];
            float h10 = Heightmap[x1, y0];
            float h01 = Heightmap[x0, y1];
            float h11 = Heightmap[x1, y1];

            float h0 = h00 * (1f - tx) + h10 * tx;
            float h1 = h01 * (1f - tx) + h11 * tx;
            return h0 * (1f - ty) + h1 * ty;
        }

        public void ApplyBrushModification(Vector3 worldPos, float radius, float strength, string operation, string shape, string falloff, int paintLayer)
        {
            HeightmapVersion++;
        }

        public int GetColorVersion() => ColorVersion;

        public void SyncColorTextureIfNeeded()
        {
            ColorVersion++;
        }

        public void SyncSkyboxIfNeeded()
        {
            SkyboxVersion++;
        }

        public void Dispose()
        {
            ColorBitmap?.Dispose();
            ColorBitmap = null;
        }
    }
}