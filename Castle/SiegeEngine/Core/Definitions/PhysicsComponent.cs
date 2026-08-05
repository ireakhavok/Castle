// Folder: SiegeEngine/Core/Definitions
// File: PhysicsComponent.cs
using System;
using System.Numerics;
using SiegeEngine.Core.Physics;

namespace SiegeEngine.Core.Definitions
{
    public class PhysicsComponent : IComponent, IComponentData
    {
        private float _mass = 1.0f;
        private float _health = 100f;
        private readonly TransformComponent _transform;

        public PhysicsComponent()
        {
            _transform = new TransformComponent();
            Size = new Vector3(1f, 1f, 1f);
            IsBreakable = false;
            IsBroken = false;
            Mass = 1.0f;
            Health = 100f;
            IsVisible = true;
            // Static is the safe default: entities stay exactly where the designer placed them
            // (including mid-air). Designer must explicitly set Dynamic to enable gravity/clamp.
            BodyType = BodyType.Static;
            AngularVelocity = Vector3.Zero;
            LinearDamping = 0.05f;
            AngularDamping = 0.05f;
            Friction = 0.5f;
            Restitution = 0.0f;
            IsSleeping = false;
            IslandId = -1;
            SleepThreshold = 0.05f;
            SleepTimer = 0f;
        }

        public Vector3 Position
        {
            get => _transform.Position;
            set => _transform.Position = value;
        }

        public Quaternion Rotation
        {
            get => _transform.Rotation;
            set => _transform.Rotation = value;
        }

        public Vector3 Scale
        {
            get => _transform.Scale;
            set => _transform.Scale = value;
        }

        public Vector3 WorldPosition => _transform.WorldPosition;
        public Quaternion WorldRotation => _transform.WorldRotation;
        public Vector3 WorldScale => _transform.WorldScale;
        public Matrix4x4 LocalToWorld => _transform.LocalToWorld;

        public Vector3 Velocity { get; set; } = Vector3.Zero;

        public bool IsVisible { get; set; }
        public float Mass
        {
            get => _mass;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Mass must be greater than zero.", nameof(value));
                _mass = value;
            }
        }
        public bool IsBreakable { get; set; }
        public bool IsBroken { get; private set; }
        public Vector3 Size { get; set; }
        public float Health
        {
            get => _health;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Health cannot be negative.", nameof(value));
                _health = value;
                if (_health <= 0 && IsBreakable)
                {
                    IsBroken = true;
                }
            }
        }

        public Vector3 LocalBoundsMinCm { get; set; } = new Vector3(float.MaxValue);
        public Vector3 LocalBoundsMaxCm { get; set; } = new Vector3(float.MinValue);

        // ===== Phase 1 additive fields (safe defaults, zero breakage) =====
        public BodyType BodyType { get; set; } = BodyType.Static;
        public Vector3 AngularVelocity { get; set; } = Vector3.Zero;
        public float LinearDamping { get; set; } = 0.05f;
        public float AngularDamping { get; set; } = 0.05f;
        public float Friction { get; set; } = 0.5f;
        public float Restitution { get; set; } = 0.0f;
        public bool IsSleeping { get; set; } = false;
        public int IslandId { get; set; } = -1;
        public float SleepThreshold { get; set; } = 0.05f;
        public float SleepTimer { get; set; } = 0f;

        public void Break()
        {
            if (IsBreakable && _health <= 0)
            {
                IsBroken = true;
            }
        }

        public bool RayIntersects(Vector3 rayOrigin, Vector3 rayDir, out float distance, out Vector3 hitPoint)
        {
            distance = 0f;
            hitPoint = Vector3.Zero;
            if (rayDir.LengthSquared() < 1e-8f) return false;

            Matrix4x4 scaleMat = Matrix4x4.CreateScale(0.01f);
            Matrix4x4 rotMat = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4 transMat = Matrix4x4.CreateTranslation(Position);
            Matrix4x4 modelMat = scaleMat * rotMat * transMat;

            if (!Matrix4x4.Invert(modelMat, out Matrix4x4 worldToLocal)) return false;

            Vector3 localOrigin = Vector3.Transform(rayOrigin, worldToLocal);
            Vector3 localDir = Vector3.TransformNormal(rayDir, worldToLocal);
            localDir = Vector3.Normalize(localDir);

            Vector3 boxMin;
            Vector3 boxMax;
            if (LocalBoundsMinCm.X <= LocalBoundsMaxCm.X &&
                LocalBoundsMinCm.Y <= LocalBoundsMaxCm.Y &&
                LocalBoundsMinCm.Z <= LocalBoundsMaxCm.Z)
            {
                boxMin = LocalBoundsMinCm;
                boxMax = LocalBoundsMaxCm;
            }
            else
            {
                Vector3 localHalfExtents = Size * 50f;
                boxMin = -localHalfExtents;
                boxMax = localHalfExtents;
            }

            float tmin = 0.0f;
            float tmax = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(localDir[i]) < 1e-6f)
                {
                    if (localOrigin[i] < boxMin[i] || localOrigin[i] > boxMax[i])
                        return false;
                }
                else
                {
                    float ood = 1.0f / localDir[i];
                    float t1 = (boxMin[i] - localOrigin[i]) * ood;
                    float t2 = (boxMax[i] - localOrigin[i]) * ood;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    tmin = Math.Max(tmin, t1);
                    tmax = Math.Min(tmax, t2);
                    if (tmin > tmax) return false;
                }
            }

            distance = tmin * 0.01f;
            hitPoint = rayOrigin + rayDir * distance;
            return true;
        }

        public object ToSerializableData()
        {
            return new PhysicsComponentData
            {
                Position = Position,
                Rotation = Rotation,
                Scale = Scale,
                Size = Size,
                Mass = Mass,
                Health = Health,
                IsBreakable = IsBreakable,
                IsBroken = IsBroken,
                LocalBoundsMinCm = LocalBoundsMinCm,
                LocalBoundsMaxCm = LocalBoundsMaxCm,
                Velocity = Velocity,
                BodyType = (int)BodyType,
                AngularVelocity = AngularVelocity,
                LinearDamping = LinearDamping,
                AngularDamping = AngularDamping,
                Friction = Friction,
                Restitution = Restitution,
                IsSleeping = IsSleeping,
                IslandId = IslandId,
                SleepThreshold = SleepThreshold
            };
        }

        public void FromSerializableData(object data)
        {
            if (data is PhysicsComponentData p)
            {
                Position = p.Position;
                Rotation = p.Rotation;
                Scale = p.Scale;
                Size = p.Size;
                Mass = p.Mass;
                Health = p.Health;
                IsBreakable = p.IsBreakable;
                IsBroken = p.IsBroken;
                LocalBoundsMinCm = p.LocalBoundsMinCm;
                LocalBoundsMaxCm = p.LocalBoundsMaxCm;
                Velocity = p.Velocity;
                BodyType = (BodyType)p.BodyType;
                AngularVelocity = p.AngularVelocity;
                LinearDamping = p.LinearDamping;
                AngularDamping = p.AngularDamping;
                Friction = p.Friction;
                Restitution = p.Restitution;
                IsSleeping = p.IsSleeping;
                IslandId = p.IslandId;
                SleepThreshold = p.SleepThreshold;
            }
        }

        private class PhysicsComponentData
        {
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public Vector3 Scale { get; set; }
            public Vector3 Size { get; set; }
            public float Mass { get; set; }
            public float Health { get; set; }
            public bool IsBreakable { get; set; }
            public bool IsBroken { get; set; }
            public Vector3 LocalBoundsMinCm { get; set; }
            public Vector3 LocalBoundsMaxCm { get; set; }
            public Vector3 Velocity { get; set; }
            public int BodyType { get; set; }
            public Vector3 AngularVelocity { get; set; }
            public float LinearDamping { get; set; }
            public float AngularDamping { get; set; }
            public float Friction { get; set; }
            public float Restitution { get; set; }
            public bool IsSleeping { get; set; }
            public int IslandId { get; set; }
            public float SleepThreshold { get; set; }
        }
    }
}