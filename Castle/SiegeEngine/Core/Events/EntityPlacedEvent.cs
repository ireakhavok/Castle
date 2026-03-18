// Folder: SiegeEngine/Core/Events
// File: EntityPlacedEvent.cs
using System;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class EntityPlacedEvent : IEvent
    {
        public string Type => "EntityPlaced";
        public int EntityId { get; set; }
        public string EntityType { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public bool IsPreview { get; set; }
        public ulong? PlayerId { get; set; }

        // Sprite-specific data for full networking and client/server sync
        public string TexturePath { get; set; } = string.Empty;
        public float Width { get; set; }
        public float Height { get; set; }

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
            var json = JsonSerializer.Serialize(new
            {
                Type,
                EntityId,
                EntityType,
                Position,
                Rotation,
                IsPreview,
                PlayerId,
                TexturePath,
                Width,
                Height
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<EntityPlacedEvent>(json);
            EntityId = obj.EntityId;
            EntityType = obj.EntityType;
            Position = obj.Position;
            Rotation = obj.Rotation;
            IsPreview = obj.IsPreview;
            PlayerId = obj.PlayerId;
            TexturePath = obj.TexturePath;
            Width = obj.Width;
            Height = obj.Height;
        }
    }
}