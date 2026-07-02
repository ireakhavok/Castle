// Folder: SiegeEngine.PlayerSystem
// File: CustomPlayerController.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Managers;
using SiegeEngine.Systems;
using System;
using System.Numerics;
namespace SiegeEngine.PlayerSystem
{
    [CustomPlayerController]
    public class CustomPlayerController : PlayerMovement
    {
        public CustomPlayerController(InputHandler inputHandler, ClientPredictionSystem predictionSystem, EventBus eventBus = null)
            : base(inputHandler, predictionSystem, eventBus)
        {
            Console.WriteLine("[CustomPlayerController] Base custom controller initialized - override Update for game-specific logic");
        }
        public override void Update(Player player, float deltaTime, Action<int, Vector2, Quaternion> sendMovementRequest, CameraController camera)
        {
            base.Update(player, deltaTime, sendMovementRequest, camera);
            Console.WriteLine("[CustomPlayerController] Custom movement override active - ready for user code (e.g. double-jump, wall-run, vehicle mount)");
        }
    }
}