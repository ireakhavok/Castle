// Folder: SiegeEngine/PlayerSystem
// File: GameActionConfig.cs
using System;
using System.Collections.Generic;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Networking;
namespace SiegeEngine.PlayerSystem
{
    public class GameActionConfig
    {
        protected readonly EventBus _eventBus;
        protected readonly SteamEngine _steamEngine;
        private readonly Dictionary<string, Action> _actionMap = new Dictionary<string, Action>();
        public GameActionConfig(EventBus eventBus, SteamEngine steamEngine)
        {
            _eventBus = eventBus;
            _steamEngine = steamEngine;
        }
        public void RegisterAction(string actionName, Action handler)
        {
            _actionMap[actionName] = handler;
        }
        public void Trigger(string actionName)
        {
            if (_actionMap.TryGetValue(actionName, out var action))
                action.Invoke();
            else
                Console.WriteLine($"GameActionConfig: Unknown action '{actionName}'");
        }
    }
    public class SaveLevelEvent { }
    public class SwitchMenuEvent { public string MenuName { get; set; } public SwitchMenuEvent(string menuName) => MenuName = menuName; }
}