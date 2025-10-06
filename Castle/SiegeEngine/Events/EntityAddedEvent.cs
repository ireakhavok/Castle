// Folder: SiegeEngine.Events
// File: EntityAddedEvent.cs
using SiegeEngine.Definitions;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System;

namespace SiegeEngine.Events
{
    public class EntityAddedEvent : IEvent
    {
        public string Type => "EntityAdded";
        public int Id { get; set; }
        public string EntityType { get; set; }
        public Dictionary<string, object> Components { get; set; } // Use string keys for type names

        public EntityAddedEvent(Entity entity)
        {
            Id = entity.Id;
            EntityType = entity.Type;
            Components = new Dictionary<string, object>();
            foreach (var comp in entity.Components)
            {
                string typeName = comp.Key.Name;
                if (comp.Value is PhysicsComponent phys)
                {
                    Components[typeName] = new { Position = phys.Position, Rotation = phys.Rotation, Size = phys.Size };
                }
                else if (comp.Value is ModelComponent model)
                {
                    Components[typeName] = new { Key = model.Key };
                }
                // Add handlers for other component types as needed
            }
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<EntityAddedEvent>(json);
            Id = obj.Id;
            EntityType = obj.EntityType;
            Components = obj.Components;
        }
    }
}