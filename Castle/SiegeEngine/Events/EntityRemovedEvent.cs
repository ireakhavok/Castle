// Folder: SiegeEngine.Events
// File: EntityRemovedEvent.cs
using System.Text.Json;
using System.Text;

namespace SiegeEngine.Events
{
    public class EntityRemovedEvent : IEvent
    {
        public string Type => "EntityRemoved";
        public int Id { get; set; }

        public EntityRemovedEvent(int id)
        {
            Id = id;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<EntityRemovedEvent>(json);
            Id = obj.Id;
        }
    }
}