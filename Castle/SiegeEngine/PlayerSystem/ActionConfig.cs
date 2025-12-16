using System;
using System.Collections.Generic;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Events;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;

namespace SiegeEngine.PlayerSystem
{
    public class ActionConfig
    {
        private readonly EventBus _eventBus;
        private readonly Dictionary<string, Action> _actionMap = new Dictionary<string, Action>();

        private readonly SteamEngine _steamEngine;

        public void Trigger(string actionName)
        {
            if (_actionMap.TryGetValue(actionName, out var action))
                action.Invoke();
            else
                Console.WriteLine($"ActionConfig: Unknown action '{actionName}'");
        }

        private void TriggerSelectBrush(string brushType)
        {
            if (_steamEngine != null)
            {
                ulong playerId = _steamEngine.GetSteamId();
                _eventBus.Publish(new SelectBrushEvent(playerId, brushType), true);
                Console.WriteLine($"ActionConfig: Published SelectBrushEvent for {brushType}, PlayerId: {playerId}");
            }
        }
    }

    public class SaveLevelEvent { }
    public class SwitchMenuEvent { public string MenuName { get; set; } public SwitchMenuEvent(string menuName) => MenuName = menuName; }
}