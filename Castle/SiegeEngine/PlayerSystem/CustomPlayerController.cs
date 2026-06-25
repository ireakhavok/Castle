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
        // Users override this for custom movement, animation triggers, sound events, etc.
        // Example: public override void Update(Player player, float deltaTime, Action<int, Vector2, Quaternion> send, CameraController camera)
        // {
        //     base.Update(player, deltaTime, send, camera);
        //     // custom: trigger animation, emit sound event, change skybox on condition
        // }
    }
}