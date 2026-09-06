// Folder: SiegeEngine/Core/Networking
// File: EntityDeltaTracker.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.Definitions;

namespace SiegeEngine.Core.Networking
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

        public List<EntityNetDelta> GetDeltas(IReadOnlyList<Entity> entities)
        {
            var deltas = new List<EntityNetDelta>();
            if (entities == null) return deltas;
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity == null) continue;
                var physics = entity.GetComponent<PhysicsComponent>();
                var anim = entity.GetComponent<AnimationComponent>();
                var blend = entity.GetComponent<BlendedAnimationComponent>();
                if (physics == null && anim == null && blend == null) continue;
                if (physics != null && !physics.IsVisible) continue;

                byte dirty = 0;
                var d = new EntityNetDelta { Id = entity.Id };

                if (physics != null)
                {
                    if (!_lastPositions.TryGetValue(entity.Id, out var lastPos) || lastPos != physics.Position)
                    {
                        dirty |= DirtyPos;
                        QuantizePos(physics.Position, out d.Px, out d.Py, out d.Pz);
                        _lastPositions[entity.Id] = physics.Position;
                    }
                    if (!_lastRotations.TryGetValue(entity.Id, out var lastRot) || lastRot != physics.Rotation)
                    {
                        dirty |= DirtyRot;
                        QuantizeRot(physics.Rotation, out d.Rx, out d.Ry, out d.Rz, out d.Rw);
                        _lastRotations[entity.Id] = physics.Rotation;
                    }
                }

                float animTime = 0f;
                bool hasAnim = false;
                if (blend != null)
                {
                    animTime = blend.GlobalTime;
                    hasAnim = true;
                }
                else if (anim != null)
                {
                    animTime = anim.Time;
                    hasAnim = true;
                }
                if (hasAnim)
                {
                    if (!_lastAnimTimes.TryGetValue(entity.Id, out var lastT) || Math.Abs(lastT - animTime) > 0.001f)
                    {
                        dirty |= DirtyAnim;
                        float t = animTime;
                        if (t < 0f) t = 0f;
                        if (t > 65f) t = 65f;
                        d.AnimTime = (ushort)(t * 1000f);
                        _lastAnimTimes[entity.Id] = animTime;
                    }
                }

                if (dirty == 0) continue;
                d.Dirty = dirty;
                deltas.Add(d);
            }
            return deltas;
        }

        public static void Apply(Entity entity, EntityNetDelta d)
        {
            if (entity == null || d == null) return;
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics != null)
            {
                if ((d.Dirty & DirtyPos) != 0)
                {
                    physics.Position = DequantizePos(d.Px, d.Py, d.Pz);
                    physics.RenderPosition = physics.Position;
                }
                if ((d.Dirty & DirtyRot) != 0)
                    physics.Rotation = DequantizeRot(d.Rx, d.Ry, d.Rz, d.Rw);
            }
            if ((d.Dirty & DirtyAnim) != 0)
            {
                float t = d.AnimTime / 1000f;
                var blend = entity.GetComponent<BlendedAnimationComponent>();
                if (blend != null) blend.GlobalTime = t;
                var anim = entity.GetComponent<AnimationComponent>();
                if (anim != null) anim.Time = t;
            }
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
            float len = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
            if (len > 1e-8f) q = new Quaternion(q.X / len, q.Y / len, q.Z / len, q.W / len);
            x = ClampShort(q.X * 32767f);
            y = ClampShort(q.Y * 32767f);
            z = ClampShort(q.Z * 32767f);
            w = ClampShort(q.W * 32767f);
        }

        public static Quaternion DequantizeRot(short x, short y, short z, short w)
        {
            var q = new Quaternion(x / 32767f, y / 32767f, z / 32767f, w / 32767f);
            float len = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
            if (len > 1e-8f) q = new Quaternion(q.X / len, q.Y / len, q.Z / len, q.W / len);
            return q;
        }

        private static short ClampShort(float v)
        {
            if (v > short.MaxValue) return short.MaxValue;
            if (v < short.MinValue) return short.MinValue;
            return (short)Math.Round(v);
        }
    }
}
