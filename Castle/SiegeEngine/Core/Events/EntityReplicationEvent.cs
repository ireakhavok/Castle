// Folder: SiegeEngine/Core/Events
// File: EntityReplicationEvent.cs
using System.Collections.Generic;
using System.IO;
using SiegeEngine.Core.Networking;

namespace SiegeEngine.Core.Events
{
    public class EntityReplicationEvent : IEvent
    {
        public const byte Magic = 0xD1;
        public string Type => "EntityReplication";
        public List<EntityNetDelta> Deltas { get; set; } = new List<EntityNetDelta>();
        public bool Authoritative { get; set; }

        public byte[] Serialize()
        {
            var deltas = Deltas ?? new List<EntityNetDelta>();
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(Magic);
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
            Deltas = Unpack(data);
        }

        public static List<EntityNetDelta> Unpack(byte[] data)
        {
            var list = new List<EntityNetDelta>();
            if (data == null || data.Length < 5 || data[0] != Magic) return list;
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            r.ReadByte();
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
