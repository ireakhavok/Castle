using System;
using System.Collections.Generic;
using SiegeEngine.Events;
using SiegeEngine.Networking;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;

namespace SiegeEngine.PlayerSystem
{
    public class ActionConfig
    {
        private readonly EventBus _eventBus;
        private readonly Dictionary<string, Action> _actionMap = new Dictionary<string, Action>();
        //private readonly EditorScene _editorScene;
        //private readonly HtmlUiSystem _uiSystem;
        private readonly SteamEngine _steamEngine;

        //public ActionConfig(EventBus eventBus, EditorScene editorScene = null, 
        //    //HtmlUiSystem uiSystem = null, 
        //    SteamEngine steamEngine = null)
        //{
        //    _eventBus = eventBus;
        //    _editorScene = editorScene;
        //    //_uiSystem = uiSystem;
        //    _steamEngine = steamEngine;
        //    _actionMap["save-level"] = () => _eventBus.Publish(new SaveLevelEvent());
        //    _actionMap["back"] = () => _eventBus.Publish(new SwitchMenuEvent("MainMenu"));
        //    _actionMap["test-level"] = () => _eventBus.Publish(new SwitchMenuEvent("SandboxMode"));
        //    _actionMap["grid-snap"] = () =>
        //    {
        //        //if (_editorScene != null && _uiSystem != null)
        //        //    _editorScene.ToggleGridSnap(_uiSystem._gridSnapState);
        //    };
        //    foreach (var brush in new[] { "Wall", "Floor", "Door", "Trap", "Light", "Fire", "Roof", "Window", "Pathway", "Road", "Bridge", "Water", "Monster", "Raise", "Lower" })
        //    {
        //        _actionMap[brush] = () => TriggerSelectBrush(brush);
        //    }
        //}

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