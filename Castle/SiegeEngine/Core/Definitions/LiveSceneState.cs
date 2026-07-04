// Folder: SiegeEngine/Core/Definitions
// File: LiveSceneState.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Terrain;
using System;
using System.Drawing;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class LiveSceneState : IDisposable, ISceneStateProvider
    {
        public string SceneName { get; }
        public float[,] Heightmap { get; set; }
        public Bitmap ColorBitmap { get; set; }
        public SkyboxData Skybox { get; set; }
        public int HeightmapVersion { get; set; } = 0;
        public int ColorVersion { get; private set; } = 0;
        public int SkyboxVersion { get; private set; } = 0;

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

        public float GetInterpolatedHeight(float worldX, float worldY)
        {
            if (Heightmap == null) return 0f;
            return 0f;
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