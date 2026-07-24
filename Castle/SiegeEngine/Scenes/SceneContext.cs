// Folder: SiegeEngine/Scenes
// File: SceneContext.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.PlayerSystem;
using System.Collections.Generic;
using System.Linq;
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
        public SceneData? SceneData { get; set; }
        public string LoadLevelName { get; set; } = "Main";
        public string PlayProjectPath { get; set; }
        public Level CurrentLevel { get; set; }
        public float[,] HeightmapSnapshot { get; set; }
        public List<Entity> RuntimeEntities { get; set; } = new List<Entity>();
        public string RuntimeSnapshotPath { get; set; }
        public static SceneContext CreateCore(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
        {
            return new SceneContext { RenderContext = renderContext, ControlContext = controlContext, Window = window, Server = server, EventBus = eventBus };
        }
        public static SceneContext CreateForRuntime(Level level, SceneData sceneData, IRenderContext rc, IControlContext cc, nint w, IGameServer s, EventBus eb)
        {
            var ctx = CreateCore(rc, cc, w, s, eb);
            ctx.CurrentLevel = level ?? new Level();
            ctx.SceneData = sceneData;
            ctx.LoadLevelName = level?.Name ?? "Main";
            ctx.ModelManager = ModelManager.Instance ?? new ModelManager(rc);
            ctx.RuntimeEntities = ctx.CurrentLevel.Entities.ToList();
            return ctx;
        }
    }
}