using System;
using System.Text.Json;

namespace SiegeEngine.Events
{
    public class ItemPickedUpEvent : IEvent
    {
        public string Type => "ItemPickedUp";
        public int EntityId { get; set; }
        public string ItemId { get; set; }

        public ItemPickedUpEvent(int entityId, string itemId)
        {
            EntityId = entityId;
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, EntityId, ItemId });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<ItemPickedUpEvent>(json);
            EntityId = obj.EntityId;
            ItemId = obj.ItemId;
        }
    }
}