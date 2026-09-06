// Folder: SiegeEngine/Core/Events
// File: EntityMovedEvent.cs
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.Networking;

namespace SiegeEngine.Core.Events
{
    public class EntityMovedEvent : IEvent
    {
        public string Type => "EntityMoved";
        public int EntityId { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float AnimTime { get; set; }
        public ulong? PlayerId { get; set; }
        public uint AckTick { get; set; }

        public EntityMovedEvent() { }

        public EntityMovedEvent(int entityId, Vector3 position, Quaternion rotation, ulong? playerId = 0, uint ackTick = 0, float animTime = 0f)
        {
            EntityId = entityId;
            Position = position;
            Rotation = rotation;
            PlayerId = playerId;
            AckTick = ackTick;
            AnimTime = animTime;
        }

        public EntityNetDelta ToDelta()
        {
            return EntityDeltaTracker.FromPose(EntityId, Position, Rotation, AnimTime, AckTick);
        }

        public byte[] Serialize()
        {
            return new EntityReplicationEvent
            {
                Deltas = new List<EntityNetDelta> { ToDelta() }
            }.Serialize();
        }

        public void Deserialize(byte[] data)
        {
            var list = EntityReplicationEvent.Unpack(data);
            if (list.Count == 0) return;
            var d = list[0];
            EntityId = d.Id;
            Position = new Vector3(d.Px, d.Py, d.Pz);
            Rotation = new Quaternion(d.Rx, d.Ry, d.Rz, d.Rw);
            AnimTime = d.AnimTime;
            AckTick = d.AckTick;
        }
    }
}
