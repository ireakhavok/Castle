// Folder: SiegeEngine/Core/Events
// File: EntitySelectedEvent.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class EntitySelectedEvent : IEvent
    {
        public string Type => "EntitySelected";
        public List<int> SelectedEntityIds { get; set; } = new List<int>();
        public Vector3? HitPoint { get; set; }
        public bool Additive { get; set; }

        public EntitySelectedEvent() { }

        public EntitySelectedEvent(int entityId, Vector3? hitPoint = null, bool additive = false)
        {
            SelectedEntityIds = new List<int> { entityId };
            HitPoint = hitPoint;
            Additive = additive;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new
            {
                Type,
                SelectedEntityIds,
                HitPoint,
                Additive
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<EntitySelectedEvent>(json);
            SelectedEntityIds = obj.SelectedEntityIds ?? new List<int>();
            HitPoint = obj.HitPoint;
            Additive = obj.Additive;
        }
    }
}