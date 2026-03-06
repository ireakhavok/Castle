// Folder: ToolChest
// File: EditorActionConfig.cs
using System;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using SiegeEngine.PlayerSystem; // Inherit from game version if shared logic needed
namespace ToolChest
{
    public class EditorActionConfig : GameActionConfig // Extend for shared base if useful
    {
        public EditorActionConfig(EventBus eventBus, SteamEngine steamEngine) : base(eventBus, steamEngine)
        {
        }
        private void TriggerSelectBrush(string brushType)
        {
            if (_steamEngine != null)
            {
                ulong playerId = _steamEngine.GetSteamId();
                _eventBus.Publish(new SelectBrushEvent(playerId, brushType, 10f, 1f, "GaussianCircle"), true);
                Console.WriteLine($"EditorActionConfig: Published SelectBrushEvent for {brushType}, PlayerId: {playerId}");
            }
        }
    }
}