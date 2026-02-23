// Folder: MapRoom
// File: TerrainCreatorScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Scenes;
using System;

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
            BuildWireframeMesh(8); // default coarse step for blank/large terrain
        }

        // FULL SUPPORT FOR ALL FORM FIELDS (Resolution = grid spacing in meters per cell)
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

            _terrainWidth = numCellsX + 1;   // vertices
            _terrainHeight = numCellsZ + 1;

            // World scale = meters per grid cell
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

                BuildWireframeMesh(cellSize);
            }
        }

        public override void LoadTerrain(string path)
        {
            base.LoadTerrain(path);
        }

        public void SetColorTexture(string path)
        {
            base.SetColorTexture(path);
        }
    }
}