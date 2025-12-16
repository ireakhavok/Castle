// Folder: SiegeEngine.Events
// File: MovementRequestEvent.cs
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class MovementRequestEvent : IEvent
    {
        public string Type => "MovementRequest";
        public int EntityId { get; private set; }
        public Vector2 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public ulong SteamId { get; private set; }

        public MovementRequestEvent(int entityId, Vector2 position, Quaternion rotation, ulong steamId)
        {
            EntityId = entityId;
            Position = position;
            Rotation = rotation;
            SteamId = steamId;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new
            {
                Type,
                EntityId,
                PositionX = Position.X,
                PositionY = Position.Y,
                RotationX = Rotation.X,
                RotationY = Rotation.Y,
                RotationZ = Rotation.Z,
                RotationW = Rotation.W,
                SteamId
            });
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            EntityId = int.Parse(obj["EntityId"].ToString());
            Position = new Vector2(float.Parse(obj["PositionX"].ToString()), float.Parse(obj["PositionY"].ToString()));
            Rotation = new Quaternion(float.Parse(obj["RotationX"].ToString()), float.Parse(obj["RotationY"].ToString()), float.Parse(obj["RotationZ"].ToString()), float.Parse(obj["RotationW"].ToString()));
            SteamId = ulong.Parse(obj["SteamId"].ToString());
        }
    }
}