// Folder: SiegeEngine/Scenes
// File: SceneContext.cs
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.PlayerSystem;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.Scenes
{
    public class SceneContext
    {
        public IRenderContext RenderContext { get; set; }
        public IControlContext ControlContext { get; set; }
        public nint Window { get; set; }
        public IGameServer Server { get; set; }
        public EventBus EventBus { get; set; }
        public Player? Player { get; set; }
        public PlayerMovement? PlayerMovement { get; set; }
        public ModelManager? ModelManager { get; set; }
        public SceneData? ProjectSceneData { get; set; }
        // Modular passed params (no ProjectSettings in core)
        public string LoadLevelName { get; set; } = "Main";
        public string PlayProjectPath { get; set; }
        // Generic Level snapshot carrier for Play / Export (future-proof data bridge, respects core separation)
        public Level CurrentLevel { get; set; }
        public float[,] HeightmapSnapshot { get; set; }
        public List<Entity> RuntimeEntities { get; set; } = new List<Entity>();
        public static SceneContext CreateCore(
            IRenderContext renderContext,
            IControlContext controlContext,
            nint window,
            IGameServer server,
            EventBus eventBus)
        {
            return new SceneContext { RenderContext = renderContext, ControlContext = controlContext, Window = window, Server = server, EventBus = eventBus };
        }
        // Factory for pure runtime snapshot (used by Play/Export)
        public static SceneContext CreateForRuntime(Level level, SceneData sceneData, IRenderContext rc, IControlContext cc, nint w, IGameServer s, EventBus eb)
        {
            var ctx = CreateCore(rc, cc, w, s, eb);
            ctx.CurrentLevel = level;
            ctx.ProjectSceneData = sceneData;
            ctx.LoadLevelName = level?.Name ?? "Main";
            ctx.ModelManager = ModelManager.Instance ?? new ModelManager(rc); // ensure manager always present
            return ctx;
        }
    }
}