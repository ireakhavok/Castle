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
        public float Px, Py, Pz;
        public float Rx, Ry, Rz, Rw;
        public float AnimTime;
        public uint AckTick;
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
            return GetDeltas(entities, 0);
        }

        public List<EntityNetDelta> GetDeltas(IReadOnlyList<Entity> entities, uint ackTick)
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
                var d = new EntityNetDelta { Id = entity.Id, AckTick = ackTick };

                if (physics != null)
                {
                    if (!_lastPositions.TryGetValue(entity.Id, out var lastPos) || lastPos != physics.Position)
                    {
                        dirty |= DirtyPos;
                        d.Px = physics.Position.X;
                        d.Py = physics.Position.Y;
                        d.Pz = physics.Position.Z;
                        _lastPositions[entity.Id] = physics.Position;
                    }
                    if (!_lastRotations.TryGetValue(entity.Id, out var lastRot) || lastRot != physics.Rotation)
                    {
                        dirty |= DirtyRot;
                        d.Rx = physics.Rotation.X;
                        d.Ry = physics.Rotation.Y;
                        d.Rz = physics.Rotation.Z;
                        d.Rw = physics.Rotation.W;
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
                    if (!_lastAnimTimes.TryGetValue(entity.Id, out var lastT) || lastT != animTime)
                    {
                        dirty |= DirtyAnim;
                        d.AnimTime = animTime;
                        _lastAnimTimes[entity.Id] = animTime;
                    }
                }

                if (dirty == 0) continue;
                d.Dirty = dirty;
                deltas.Add(d);
            }
            return deltas;
        }

        public static EntityNetDelta FromPose(int id, Vector3 pos, Quaternion rot, float animTime, uint ackTick)
        {
            return new EntityNetDelta
            {
                Id = id,
                Dirty = (byte)(DirtyPos | DirtyRot | DirtyAnim),
                Px = pos.X, Py = pos.Y, Pz = pos.Z,
                Rx = rot.X, Ry = rot.Y, Rz = rot.Z, Rw = rot.W,
                AnimTime = animTime,
                AckTick = ackTick
            };
        }

        public static void Apply(Entity entity, EntityNetDelta d)
        {
            if (entity == null || d == null) return;
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics != null)
            {
                if ((d.Dirty & DirtyPos) != 0)
                    physics.Position = new Vector3(d.Px, d.Py, d.Pz);
                if ((d.Dirty & DirtyRot) != 0)
                    physics.Rotation = new Quaternion(d.Rx, d.Ry, d.Rz, d.Rw);
            }
            if ((d.Dirty & DirtyAnim) != 0)
            {
                var blend = entity.GetComponent<BlendedAnimationComponent>();
                if (blend != null) blend.GlobalTime = d.AnimTime;
                var anim = entity.GetComponent<AnimationComponent>();
                if (anim != null) anim.Time = d.AnimTime;
            }
        }
    }
}
