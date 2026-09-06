// Folder: Citadel/Server
// File: EntityDeltaTracker.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.Definitions;

namespace Citadel.Server
{
    public class EntityNetDelta
    {
        public int Id;
        public byte Dirty;
        public short Px, Py, Pz;
        public short Rx, Ry, Rz, Rw;
        public ushort AnimTime;
    }

    public class EntityDeltaTracker
    {
        public const byte DirtyPos = 1;
        public const byte DirtyRot = 2;
        public const byte DirtyAnim = 4;

        private readonly Dictionary<int, Vector3> _lastPositions = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Quaternion> _lastRotations = new Dictionary<int, Quaternion>();
        private readonly Dictionary<int, float> _lastAnimTimes = new Dictionary<int, float>();

        public void Update(IReadOnlyList<Entity> entities)
        {
            foreach (var entity in entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                {
                    _lastPositions[entity.Id] = physics.Position;
                    _lastRotations[entity.Id] = physics.Rotation;
                }
                var anim = entity.GetComponent<AnimationComponent>();
                if (anim != null)
                    _lastAnimTimes[entity.Id] = anim.Time;
            }
        }

        public List<EntityNetDelta> GetDeltas(IReadOnlyList<Entity> entities)
        {
            var deltas = new List<EntityNetDelta>();
            foreach (var entity in entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                var anim = entity.GetComponent<AnimationComponent>();
                if (physics == null && anim == null) continue;
                if (physics != null && !physics.IsVisible) continue;

                byte dirty = 0;
                var d = new EntityNetDelta { Id = entity.Id };

                if (physics != null)
                {
                    if (!_lastPositions.TryGetValue(entity.Id, out var lastPos) || lastPos != physics.Position)
                    {
                        dirty |= DirtyPos;
                        QuantizePos(physics.Position, out d.Px, out d.Py, out d.Pz);
                    }
                    if (!_lastRotations.TryGetValue(entity.Id, out var lastRot) || lastRot != physics.Rotation)
                    {
                        dirty |= DirtyRot;
                        QuantizeRot(physics.Rotation, out d.Rx, out d.Ry, out d.Rz, out d.Rw);
                    }
                }
                if (anim != null)
                {
                    if (!_lastAnimTimes.TryGetValue(entity.Id, out var lastT) || Math.Abs(lastT - anim.Time) > 0.001f)
                    {
                        dirty |= DirtyAnim;
                        float t = anim.Time;
                        if (t < 0f) t = 0f;
                        if (t > 65f) t = 65f;
                        d.AnimTime = (ushort)(t * 1000f);
                    }
                }
                if (dirty == 0) continue;
                d.Dirty = dirty;
                deltas.Add(d);
            }
            return deltas;
        }

        public static void QuantizePos(Vector3 p, out short x, out short y, out short z)
        {
            x = ClampShort(p.X * 100f);
            y = ClampShort(p.Y * 100f);
            z = ClampShort(p.Z * 100f);
        }

        public static Vector3 DequantizePos(short x, short y, short z)
        {
            return new Vector3(x / 100f, y / 100f, z / 100f);
        }

        public static void QuantizeRot(Quaternion q, out short x, out short y, out short z, out short w)
        {
            q = Quaternion.Normalize(q);
            x = ClampShort(q.X * 32767f);
            y = ClampShort(q.Y * 32767f);
            z = ClampShort(q.Z * 32767f);
            w = ClampShort(q.W * 32767f);
        }

        public static Quaternion DequantizeRot(short x, short y, short z, short w)
        {
            var q = new Quaternion(x / 32767f, y / 32767f, z / 32767f, w / 32767f);
            return Quaternion.Normalize(q);
        }

        private static short ClampShort(float v)
        {
            if (v > short.MaxValue) return short.MaxValue;
            if (v < short.MinValue) return short.MinValue;
            return (short)Math.Round(v);
        }
    }
}
