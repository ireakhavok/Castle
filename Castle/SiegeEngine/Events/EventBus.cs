using SiegeEngine.Networking;
using SiegeEngine.Definitions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
namespace SiegeEngine.Events
{
    public interface IEvent
    {
        string Type { get; }
        byte[] Serialize();
        void Deserialize(byte[] data);
    }
    public class EventBus
    {
        private readonly Dictionary<Type, List<object>> _subscribers = new Dictionary<Type, List<object>>();
        private readonly SteamEngine _steamEngine;
        public EventBus(SteamEngine steamEngine = null)
        {
            _steamEngine = steamEngine;
        }
        public void Subscribe<T>(Action<T> handler) where T : class
        {
            Type type = typeof(T);
            if (!_subscribers.ContainsKey(type))
            {
                _subscribers[type] = new List<object>();
            }
            _subscribers[type].Add(handler);
            Console.WriteLine($"EventBus: Subscribed to {type.Name}");
        }
        public void Publish<T>(T eventData, bool networkSync = false) where T : class
        {
            Type type = typeof(T);
            bool isProtected = type.GetCustomAttribute<ProtectedEventAttribute>() != null;
            if (isProtected)
            {
                StackTrace stackTrace = new StackTrace();
                bool isInternalCaller = stackTrace.GetFrames()?.Any(frame =>
                    frame.GetMethod()?.DeclaringType?.Namespace?.StartsWith("Citadel") == true) ?? false;
                if (!isInternalCaller)
                {
                    Console.WriteLine($"EventBus: Rejected publish of protected event {type.Name} from unauthorized caller");
                    return;
                }
            }
            if (_subscribers.ContainsKey(type))
            {
                foreach (var handler in _subscribers[type])
                {
                    ((Action<T>)handler)(eventData);
                }
                Console.WriteLine($"EventBus: Published {type.Name}");
            }
            if (networkSync && _steamEngine != null && !isProtected)
            {
                byte[] data = eventData is IEvent ievent ? ievent.Serialize() : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventData));
                _steamEngine.SendP2PMessage(data);
                Console.WriteLine($"EventBus: Sent networked event {type.Name}");
            }
        }
        public void ProcessNetworkMessage(byte[] data)
        {
            string message = Encoding.UTF8.GetString(data);
            if (message.StartsWith("Input:"))
            {
                var parts = message.Split(':');
                if (parts.Length >= 5 && parts[0] == "Input")
                {
                    try
                    {
                        if (parts[1] == "MousePosition")
                        {
                            float x = float.Parse(parts[2]);
                            float y = float.Parse(parts[3]);
                            ulong steamId = ulong.Parse(parts[4]);
                            var mouseEvent = new MouseInputEvent(new Vector2(x, y), (MouseButton)(-1), (InputAction)(-1), steamId);
                            Publish(mouseEvent, false);
                            Console.WriteLine($"EventBus: Processed MousePosition event: Pos=({x}, {y}), SteamID={steamId}");
                        }
                        else if (parts[1] == "MouseButton")
                        {
                            int button = int.Parse(parts[2]);
                            int action = int.Parse(parts[3]);
                            ulong steamId = ulong.Parse(parts[4]);
                            var mouseEvent = new MouseInputEvent(Vector2.Zero, (MouseButton)button, (InputAction)action, steamId);
                            Publish(mouseEvent, false);
                            Console.WriteLine($"EventBus: Processed MouseButton event: Button={button}, Action={action}, SteamID={steamId}");
                        }
                        else if (parts[1] == "Key")
                        {
                            int key = int.Parse(parts[2]);
                            int action = int.Parse(parts[3]);
                            ulong steamId = ulong.Parse(parts[4]);
                            var keyEvent = new KeyInputEvent((Key)key, (InputAction)action, steamId);
                            Publish(keyEvent, false);
                            Console.WriteLine($"EventBus: Processed Key event: Key={key}, Action={action}, SteamID={steamId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"EventBus: Error parsing input: {ex.Message}");
                    }
                }
                return;
            }
            try
            {
                var msg = JsonSerializer.Deserialize<Dictionary<string, object>>(message);
                string typeName = msg["Type"]?.ToString();
                Type type = Type.GetType($"SiegeEngine.Events.{typeName}");
                if (type != null && _subscribers.ContainsKey(type))
                {
                    bool isProtected = type.GetCustomAttribute<ProtectedEventAttribute>() != null;
                    if (isProtected)
                    {
                        Console.WriteLine($"EventBus: Rejected processing of protected networked event {typeName}");
                        return;
                    }
                    var eventData = JsonSerializer.Deserialize(message, type);
                    foreach (var handler in _subscribers[type])
                    {
                        ((Action<object>)handler)(eventData);
                    }
                    Console.WriteLine($"EventBus: Processed networked event {typeName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EventBus: Error processing network message: {ex.Message}");
            }
        }
    }
    public class LobbyCreatedEvent : IEvent
    {
        public string Type => "LobbyCreated";
        public ulong LobbyId { get; private set; }
        public LobbyCreatedEvent(ulong lobbyId) => LobbyId = lobbyId;
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, LobbyId });
            return Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<LobbyCreatedEvent>(json);
            LobbyId = obj.LobbyId;
        }
    }
    public class LobbyJoinedEvent : IEvent
    {
        public string Type => "LobbyJoined";
        public ulong LobbyId { get; private set; }
        public LobbyJoinedEvent(ulong lobbyId) => LobbyId = lobbyId;
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, LobbyId });
            return Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<LobbyJoinedEvent>(json);
            LobbyId = obj.LobbyId;
        }
    }
    public class MouseInputEvent : IEvent
    {
        public string Type => "MouseInput";
        public Vector2 Position { get; private set; }
        public MouseButton Button { get; private set; }
        public InputAction Action { get; private set; }
        public ulong SteamId { get; private set; }
        public MouseInputEvent(Vector2 position, MouseButton button, InputAction action, ulong steamId)
        {
            Position = position;
            Button = button;
            Action = action;
            SteamId = steamId;
        }
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new
            {
                Type,
                PositionX = Position.X,
                PositionY = Position.Y,
                Button = (int)Button,
                Action = (int)Action,
                SteamId
            });
            return Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            Position = new Vector2(float.Parse(obj["PositionX"].ToString()), float.Parse(obj["PositionY"].ToString()));
            Button = (MouseButton)int.Parse(obj["Button"].ToString());
            Action = (InputAction)int.Parse(obj["Action"].ToString());
            SteamId = ulong.Parse(obj["SteamId"].ToString());
        }
    }
    public class KeyInputEvent : IEvent
    {
        public string Type => "KeyInput";
        public Key Key { get; private set; }
        public InputAction Action { get; private set; }
        public ulong SteamId { get; private set; }
        public KeyInputEvent(Key key, InputAction action, ulong steamId)
        {
            Key = key;
            Action = action;
            SteamId = steamId;
        }
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new
            {
                Type,
                Key = (int)Key,
                Action = (int)Action,
                SteamId
            });
            return Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            Key = (Key)int.Parse(obj["Key"].ToString());
            Action = (InputAction)int.Parse(obj["Action"].ToString());
            SteamId = ulong.Parse(obj["SteamId"].ToString());
        }
    }
    public class ToggleGridSnapEvent : IEvent
    {
        public string Type => "ToggleGridSnap";
        public ulong PlayerId { get; private set; }
        public bool State { get; private set; }
        public ToggleGridSnapEvent(ulong playerId, bool state)
        {
            PlayerId = playerId;
            State = state;
        }
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, PlayerId, State });
            return Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<ToggleGridSnapEvent>(json);
            PlayerId = obj.PlayerId;
            State = obj.State;
        }
    }
}