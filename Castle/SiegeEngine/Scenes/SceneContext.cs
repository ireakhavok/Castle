// Folder: SiegeEngine.Scenes
// File: SceneContext.cs
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.PlayerSystem;
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

        public static SceneContext CreateCore(
            IRenderContext renderContext,
            IControlContext controlContext,
            nint window,
            IGameServer server,
            EventBus eventBus)
        {
            return new SceneContext
            {
                RenderContext = renderContext,
                ControlContext = controlContext,
                Window = window,
                Server = server,
                EventBus = eventBus
            };
        }
    }
}