using System;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Events
{
    public class EntityPlacedEvent : IEvent
    {
        public string Type => "EntityPlaced";
        public int EntityId { get; set; }
        public string EntityType { get; set; }
        public Vector3 Position { get; set; }
        public bool IsPreview { get; set; }
        public ulong? PlayerId { get; set; }

        public EntityPlacedEvent(int entityId, string type, Vector3 position, bool isPreview = false, ulong? playerId = null)
        {
            EntityId = entityId;
            EntityType = type;
            Position = position;
            IsPreview = isPreview;
            PlayerId = playerId;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, EntityId, EntityType, Position, IsPreview, PlayerId });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<EntityPlacedEvent>(json);
            EntityId = obj.EntityId;
            EntityType = obj.EntityType;
            Position = obj.Position;
            IsPreview = obj.IsPreview;
            PlayerId = obj.PlayerId;
        }
    }
}