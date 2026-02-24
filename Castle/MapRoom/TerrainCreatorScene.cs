// Folder: MapRoom
// File: TerrainCreatorScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Terrain;
using SiegeEngine.Scenes;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
namespace MapRoom
{
    public class TerrainCreatorScene : TerrainScene
    {
        public TerrainCreatorScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
        }
        public void CreateBlank()
        {
            _heightmap = new float[_terrainWidth, _terrainHeight];
            _minHeight = float.MaxValue;
            _maxHeight = float.MinValue;
            for (int x = 0; x < _terrainWidth; x++)
            {
                for (int z = 0; z < _terrainHeight; z++)
                {
                    float h = 0f;
                    _heightmap[x, z] = h;
                    if (h < _minHeight) _minHeight = h;
                    if (h > _maxHeight) _maxHeight = h;
                }
            }
            Console.WriteLine($"[TerrainCreatorScene] Created blank {_terrainWidth}×{_terrainHeight} terrain with height range {_minHeight:F1} to {_maxHeight:F1}");
            _useCustomScale = true;
            BuildWireframeMesh(1); // full resolution grid for custom editor terrain (step=1 in heightmap indices)
            // Center camera at real-world scale
            float centerX = (_terrainWidth * _worldScaleX) / 2f;
            float centerZ = (_terrainHeight * _worldScaleZ) / 2f;
            _flyCamera.Position = new Vector3(centerX, _maxHeight * 1.5f + 10f, centerZ + 50f);
            _flyCamera.Yaw = 0f;
            _flyCamera.Pitch = -MathF.PI / 6f;
        }
        // FULL SUPPORT FOR ALL FORM FIELDS (Resolution = grid cell size = meters per heightmap cell / data density)
        public void CreateTerrain(TerrainCreationParams parameters)
        {
            if (parameters == null)
            {
                CreateBlank();
                return;
            }
            float cellSize = parameters.Resolution > 0 ? parameters.Resolution : 1.0f;
            // Calculate number of grid cells from physical size + cell size
            int numCellsX = (int)Math.Ceiling(parameters.Width / cellSize);
            int numCellsZ = (int)Math.Ceiling(parameters.Depth / cellSize);
            _terrainWidth = numCellsX + 1; // vertices
            _terrainHeight = numCellsZ + 1;
            // World scale = meters per grid cell (data density/sparsity) — unchanged
            _worldScaleX = cellSize;
            _worldScaleZ = cellSize;
            if (!string.IsNullOrEmpty(parameters.ImportPath))
            {
                Console.WriteLine($"[TerrainCreatorScene] Loading GeoTIFF: {parameters.ImportPath}");
                LoadTerrain(parameters.ImportPath);
            }
            else
            {
                _heightmap = new float[_terrainWidth, _terrainHeight];
                _minHeight = parameters.InitialHeight;
                _maxHeight = parameters.InitialHeight;
                for (int x = 0; x < _terrainWidth; x++)
                {
                    for (int z = 0; z < _terrainHeight; z++)
                    {
                        _heightmap[x, z] = parameters.InitialHeight * parameters.VerticalExaggeration;
                    }
                }
                Console.WriteLine($"[TerrainCreatorScene] SUCCESS: Created {parameters.Width}m × {parameters.Depth}m terrain ({numCellsX}×{numCellsZ} cells, {cellSize}m spacing) at base height {parameters.InitialHeight}, vert exag {parameters.VerticalExaggeration}");
                _useCustomScale = true;
                BuildWireframeMesh(1); // full resolution grid for custom editor terrain (step=1 in heightmap indices) — Resolution controls data density, not render step
                // Center camera at real-world scale
                float centerX = (_terrainWidth * _worldScaleX) / 2f;
                float centerZ = (_terrainHeight * _worldScaleZ) / 2f;
                _flyCamera.Position = new Vector3(centerX, _maxHeight * 1.5f + 10f, centerZ + 50f);
                _flyCamera.Yaw = 0f;
                _flyCamera.Pitch = -MathF.PI / 6f;
            }
        }
        public override void LoadTerrain(string path)
        {
            base.LoadTerrain(path);
            if (!_terrainGeoRef.IsValid)
            {
                float sX, sZ;
                if (TerrainParser.TryGetCustomScale(path, out sX, out sZ))
                {
                    _worldScaleX = sX;
                    _worldScaleZ = sZ;
                    _useCustomScale = true;
                    Console.WriteLine($"[TerrainCreatorScene] Restored exact custom scale from private tags: {sX:F2}m/cell");
                    BuildWireframeMesh(1);
                    float centerX = (_terrainWidth * _worldScaleX) / 2f;
                    float centerZ = (_terrainHeight * _worldScaleZ) / 2f;
                    _flyCamera.Position = new Vector3(centerX, _maxHeight * 1.5f + 10f, centerZ + 50f);
                    _flyCamera.Yaw = 0f;
                    _flyCamera.Pitch = -MathF.PI / 6f;
                }
            }
        }
        public void SetColorTexture(string path)
        {
            base.SetColorTexture(path);
        }
        public void SaveTerrain(string terrainName)
        {
            if (string.IsNullOrEmpty(terrainName))
                terrainName = "UntitledTerrain";
            string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", "Saved");
            Directory.CreateDirectory(saveDir);
            string tifPath = Path.Combine(saveDir, terrainName + ".tif");
            string pngPath = Path.Combine(saveDir, terrainName + ".png");
            SaveAsPng(pngPath);
            SaveAsTiff(tifPath);
            Console.WriteLine($"[TerrainCreatorScene] Saved terrain '{terrainName}' → 32-bit float TIFF (cm-scale fidelity, no geo tags) + PNG preview");
        }
        private void SaveAsPng(string path)
        {
            int w = _terrainWidth;
            int h = _terrainHeight;
            using var bmp = new Bitmap(w, h);
            float range = _maxHeight - _minHeight;
            if (range <= 0) range = 1f;
            for (int x = 0; x < w; x++)
            {
                for (int z = 0; z < h; z++)
                {
                    float norm = (_heightmap[x, z] - _minHeight) / range;
                    byte gray = (byte)Math.Clamp((int)(norm * 255), 0, 255);
                    bmp.SetPixel(x, z, Color.FromArgb(gray, gray, gray));
                }
            }
            bmp.Save(path, ImageFormat.Png);
        }
        private void SaveAsTiff(string path)
        {
            TerrainParser.SaveFloatTiff(path, _heightmap, _worldScaleX, _worldScaleZ);
        }
    }
}