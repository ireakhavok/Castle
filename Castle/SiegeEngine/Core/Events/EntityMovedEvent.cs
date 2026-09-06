// Folder: SiegeEngine/Core/Events
// File: EntityMovedEvent.cs
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using SiegeEngine.Core.Networking;

namespace SiegeEngine.Core.Events
{
    public class EntityMovedEvent : IEvent
    {
        public const byte Magic = 0xD1;
        public string Type => "EntityMoved";
        public int EntityId { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float AnimTime { get; set; }
        public ulong? PlayerId { get; set; }
        public uint AckTick { get; set; }
        public bool Authoritative { get; set; }
        public List<EntityNetDelta> Deltas { get; set; }

        public EntityMovedEvent() { }

        public EntityMovedEvent(int entityId, Vector3 position, Quaternion rotation, ulong? playerId = 0, uint ackTick = 0, float animTime = 0f)
        {
            EntityId = entityId;
            Position = position;
            Rotation = rotation;
            PlayerId = playerId;
            AckTick = ackTick;
            AnimTime = animTime;
            Deltas = new List<EntityNetDelta>
            {
                EntityDeltaTracker.FromPose(entityId, position, rotation, animTime, ackTick)
            };
        }

        public List<EntityNetDelta> AllDeltas()
        {
            if (Deltas != null && Deltas.Count > 0) return Deltas;
            return new List<EntityNetDelta>
            {
                EntityDeltaTracker.FromPose(EntityId, Position, Rotation, AnimTime, AckTick)
            };
        }

        public byte[] Serialize()
        {
            var deltas = AllDeltas();
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(Magic);
            w.Write(Authoritative);
            w.Write(deltas.Count);
            for (int i = 0; i < deltas.Count; i++)
            {
                var d = deltas[i];
                w.Write(d.Id);
                w.Write(d.Dirty);
                w.Write(d.AckTick);
                if ((d.Dirty & EntityDeltaTracker.DirtyPos) != 0)
                {
                    w.Write(d.Px); w.Write(d.Py); w.Write(d.Pz);
                }
                if ((d.Dirty & EntityDeltaTracker.DirtyRot) != 0)
                {
                    w.Write(d.Rx); w.Write(d.Ry); w.Write(d.Rz); w.Write(d.Rw);
                }
                if ((d.Dirty & EntityDeltaTracker.DirtyAnim) != 0)
                    w.Write(d.AnimTime);
            }
            return ms.ToArray();
        }

        public void Deserialize(byte[] data)
        {
            Deltas = Unpack(data, out bool auth);
            Authoritative = auth;
            if (Deltas.Count == 0) return;
            var d = Deltas[0];
            EntityId = d.Id;
            Position = new Vector3(d.Px, d.Py, d.Pz);
            Rotation = new Quaternion(d.Rx, d.Ry, d.Rz, d.Rw);
            AnimTime = d.AnimTime;
            AckTick = d.AckTick;
        }

        public static List<EntityNetDelta> Unpack(byte[] data, out bool authoritative)
        {
            authoritative = false;
            var list = new List<EntityNetDelta>();
            if (data == null || data.Length < 6 || data[0] != Magic) return list;
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            r.ReadByte();
            authoritative = r.ReadBoolean();
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var d = new EntityNetDelta
                {
                    Id = r.ReadInt32(),
                    Dirty = r.ReadByte(),
                    AckTick = r.ReadUInt32()
                };
                if ((d.Dirty & EntityDeltaTracker.DirtyPos) != 0)
                {
                    d.Px = r.ReadSingle(); d.Py = r.ReadSingle(); d.Pz = r.ReadSingle();
                }
                if ((d.Dirty & EntityDeltaTracker.DirtyRot) != 0)
                {
                    d.Rx = r.ReadSingle(); d.Ry = r.ReadSingle(); d.Rz = r.ReadSingle(); d.Rw = r.ReadSingle();
                }
                if ((d.Dirty & EntityDeltaTracker.DirtyAnim) != 0)
                    d.AnimTime = r.ReadSingle();
                list.Add(d);
            }
            return list;
        }
    }
}
