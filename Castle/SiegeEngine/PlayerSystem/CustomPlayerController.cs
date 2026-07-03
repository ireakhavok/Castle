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
    }
}