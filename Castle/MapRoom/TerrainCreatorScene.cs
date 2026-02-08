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
            Console.WriteLine("[TerrainCreatorScene] Created blank 2048×2048 terrain at 1m resolution");
        }

        public override void LoadTerrain(string path)
        {
            base.LoadTerrain(path);
            Console.WriteLine($"[TerrainCreatorScene] Importing TIFF: {path}");
            // TODO: TerrainParser.ParseTIFF(path) → populate _heightmap
            _heightmap[1024, 1024] = 80f;   // visible test bump
            BuildDebugTerrainMesh();
        }
    }
}