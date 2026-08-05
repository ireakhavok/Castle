// Folder: SiegeEngine/Core/Definitions
// File: PhysicsComponent.cs
using System;
using System.Numerics;
using System.Text.Json;
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
            // (including mid-air). Designer must explicitly set Dynamic/Kinematic to enable gravity.
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
            if (data == null) return;

            // In-memory path (no JSON crossing): typed PhysicsComponentData
            if (data is PhysicsComponentData p)
            {
                ApplyData(
                    p.Position, p.Rotation, p.Scale, p.Size,
                    p.Mass, p.Health, p.IsBreakable, p.IsBroken,
                    p.LocalBoundsMinCm, p.LocalBoundsMaxCm, p.Velocity,
                    p.BodyType, p.AngularVelocity,
                    p.LinearDamping, p.AngularDamping, p.Friction, p.Restitution,
                    p.IsSleeping, p.IslandId, p.SleepThreshold);
                return;
            }

            // JSON round-trip path: System.Text.Json deserializes object → JsonElement
            if (data is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                ApplyFromJsonElement(je);
                return;
            }

            // Fallback: some serializers leave a boxed JsonDocument or string
            if (data is string jsonStr)
            {
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    ApplyFromJsonElement(doc.RootElement);
            }
        }

        private void ApplyFromJsonElement(JsonElement je)
        {
            Vector3 position = ReadVector3(je, "Position", Position);
            Quaternion rotation = ReadQuaternion(je, "Rotation", Rotation);
            Vector3 scale = ReadVector3(je, "Scale", Scale);
            Vector3 size = ReadVector3(je, "Size", Size);
            float mass = ReadFloat(je, "Mass", Mass);
            float health = ReadFloat(je, "Health", Health);
            bool isBreakable = ReadBool(je, "IsBreakable", IsBreakable);
            bool isBroken = ReadBool(je, "IsBroken", IsBroken);
            Vector3 localMin = ReadVector3(je, "LocalBoundsMinCm", LocalBoundsMinCm);
            Vector3 localMax = ReadVector3(je, "LocalBoundsMaxCm", LocalBoundsMaxCm);
            Vector3 velocity = ReadVector3(je, "Velocity", Velocity);
            int bodyType = ReadInt(je, "BodyType", (int)BodyType);
            Vector3 angularVelocity = ReadVector3(je, "AngularVelocity", AngularVelocity);
            float linearDamping = ReadFloat(je, "LinearDamping", LinearDamping);
            float angularDamping = ReadFloat(je, "AngularDamping", AngularDamping);
            float friction = ReadFloat(je, "Friction", Friction);
            float restitution = ReadFloat(je, "Restitution", Restitution);
            bool isSleeping = ReadBool(je, "IsSleeping", IsSleeping);
            int islandId = ReadInt(je, "IslandId", IslandId);
            float sleepThreshold = ReadFloat(je, "SleepThreshold", SleepThreshold);

            ApplyData(
                position, rotation, scale, size,
                mass, health, isBreakable, isBroken,
                localMin, localMax, velocity,
                bodyType, angularVelocity,
                linearDamping, angularDamping, friction, restitution,
                isSleeping, islandId, sleepThreshold);
        }

        private void ApplyData(
            Vector3 position, Quaternion rotation, Vector3 scale, Vector3 size,
            float mass, float health, bool isBreakable, bool isBroken,
            Vector3 localMin, Vector3 localMax, Vector3 velocity,
            int bodyType, Vector3 angularVelocity,
            float linearDamping, float angularDamping, float friction, float restitution,
            bool isSleeping, int islandId, float sleepThreshold)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Size = size;
            // Mass/Health setters throw on invalid values — clamp defensively
            if (mass > 0f) Mass = mass;
            if (health >= 0f) Health = health;
            IsBreakable = isBreakable;
            if (isBroken) Health = 0f; // drives IsBroken via setter when breakable
            LocalBoundsMinCm = localMin;
            LocalBoundsMaxCm = localMax;
            Velocity = velocity;
            BodyType = (BodyType)bodyType;
            AngularVelocity = angularVelocity;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
            Friction = friction;
            Restitution = restitution;
            IsSleeping = isSleeping;
            IslandId = islandId;
            SleepThreshold = sleepThreshold;
        }

        private static Vector3 ReadVector3(JsonElement parent, string name, Vector3 fallback)
        {
            if (!parent.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.Object)
                return fallback;
            float x = el.TryGetProperty("X", out var xe) && xe.TryGetSingle(out float xv) ? xv : fallback.X;
            float y = el.TryGetProperty("Y", out var ye) && ye.TryGetSingle(out float yv) ? yv : fallback.Y;
            float z = el.TryGetProperty("Z", out var ze) && ze.TryGetSingle(out float zv) ? zv : fallback.Z;
            return new Vector3(x, y, z);
        }

        private static Quaternion ReadQuaternion(JsonElement parent, string name, Quaternion fallback)
        {
            if (!parent.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.Object)
                return fallback;
            float x = el.TryGetProperty("X", out var xe) && xe.TryGetSingle(out float xv) ? xv : fallback.X;
            float y = el.TryGetProperty("Y", out var ye) && ye.TryGetSingle(out float yv) ? yv : fallback.Y;
            float z = el.TryGetProperty("Z", out var ze) && ze.TryGetSingle(out float zv) ? zv : fallback.Z;
            float w = el.TryGetProperty("W", out var we) && we.TryGetSingle(out float wv) ? wv : fallback.W;
            return new Quaternion(x, y, z, w);
        }

        private static float ReadFloat(JsonElement parent, string name, float fallback)
        {
            if (!parent.TryGetProperty(name, out JsonElement el)) return fallback;
            if (el.TryGetSingle(out float v)) return v;
            if (el.TryGetDouble(out double d)) return (float)d;
            return fallback;
        }

        private static int ReadInt(JsonElement parent, string name, int fallback)
        {
            if (!parent.TryGetProperty(name, out JsonElement el)) return fallback;
            if (el.TryGetInt32(out int v)) return v;
            if (el.TryGetDouble(out double d)) return (int)d;
            return fallback;
        }

        private static bool ReadBool(JsonElement parent, string name, bool fallback)
        {
            if (!parent.TryGetProperty(name, out JsonElement el)) return fallback;
            return el.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => fallback
            };
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