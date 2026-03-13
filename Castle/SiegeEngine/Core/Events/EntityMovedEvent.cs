// Folder: SiegeEngine/Core/Events
// File: EntityMovedEvent.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class EntityMovedEvent : IEvent
    {
        public string Type => "EntityMoved";
        public int EntityId { get; set; }
        public Vector2 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public ulong? PlayerId { get; set; }

        public EntityMovedEvent(int entityId, Vector2 position, Quaternion rotation, ulong? playerId = 0)
        {
            EntityId = entityId;
            Position = position;
            Rotation = rotation;
            PlayerId = playerId;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new
            {
                Type,
                EntityId,
                Position = new { Position.X, Position.Y },
                Rotation = new { Rotation.X, Rotation.Y, Rotation.Z, Rotation.W },
                PlayerId
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            EntityId = Convert.ToInt32(obj["EntityId"]);
            var pos = JsonSerializer.Deserialize<Dictionary<string, float>>(obj["Position"].ToString());
            Position = new Vector2(pos["X"], pos["Y"]);
            var rot = JsonSerializer.Deserialize<Dictionary<string, float>>(obj["Rotation"].ToString());
            Rotation = new Quaternion(rot["X"], rot["Y"], rot["Z"], rot["W"]);
            PlayerId = obj["PlayerId"] != null ? Convert.ToUInt64(obj["PlayerId"]) : null;
        }
    }
}