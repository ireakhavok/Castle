// Folder: SiegeEngine/Core/Definitions
// File: PhysicsComponent.cs
using System;
using System.Numerics;
using System.Text.Json;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Physics;
namespace SiegeEngine.Core.Definitions
{
    public class PhysicsComponent : IComponent, IComponentData
    {
        private float _mass = 1.0f;
        private float _health = 100f;
        private readonly TransformComponent _transform;
        private Vector3 _forceAccum;
        private Vector3 _torqueAccum;
        public PhysicsComponent()
        {
            _transform = new TransformComponent();
            Size = new Vector3(1f, 1f, 1f);
            IsBreakable = false;
            IsBroken = false;
            Mass = 1.0f;
            Health = 100f;
            IsVisible = true;
            BodyType = BodyType.Static;
            AngularVelocity = Vector3.Zero;
            LinearDamping = 0.4f;
            AngularDamping = 0.4f;
            Friction = 1.8f;
            Restitution = 0.0f;
            KineticFriction = 0.60f;
            StaticFriction = 0.85f;
            RollingResistance = 0.20f;
            IsSleeping = false;
            IslandId = -1;
            SleepThreshold = 0.05f;
            SleepTimer = 0f;
            CollisionEnabled = true;
            IsGrounded = false;
            SlopeLimitDegrees = 50f;
            StepHeight = 0.35f;
            LocalCentreOfMass = Vector3.Zero;
            InvMass = 0f;
            InvInertiaLocal = Vector3.Zero;
            RenderPosition = Position;
            _forceAccum = Vector3.Zero;
            _torqueAccum = Vector3.Zero;
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
                RecomputeMassProperties();
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
                    IsBroken = true;
            }
        }
        public Vector3 LocalBoundsMinCm { get; set; } = new Vector3(float.MaxValue);
        public Vector3 LocalBoundsMaxCm { get; set; } = new Vector3(float.MinValue);
        public BodyType BodyType { get; set; } = BodyType.Static;
        public Vector3 AngularVelocity { get; set; } = Vector3.Zero;
        public float LinearDamping { get; set; } = 0.4f;
        public float AngularDamping { get; set; } = 0.4f;
        public float Friction { get; set; } = 1.8f;
        public float Restitution { get; set; } = 0.0f;
        /// <summary>Kinetic (sliding) friction coefficient used on heightfield / terrain.</summary>
        public float KineticFriction { get; set; } = 0.60f;
        /// <summary>Static friction coefficient used when nearly at rest on heightfield / terrain.</summary>
        public float StaticFriction { get; set; } = 0.85f;
        /// <summary>Rolling-resistance coefficient. Higher values make the body lose energy faster while rolling.</summary>
        public float RollingResistance { get; set; } = 0.20f;
        public bool IsSleeping { get; set; } = false;
        public int IslandId { get; set; } = -1;
        public float SleepThreshold { get; set; } = 0.05f;
        public float SleepTimer { get; set; } = 0f;
        public bool CollisionEnabled { get; set; } = true;
        public bool IsGrounded { get; set; } = false;
        public float SlopeLimitDegrees { get; set; } = 50f;
        public float StepHeight { get; set; } = 0.35f;
        public Vector3 RenderPosition { get; set; }
        public Vector3 LocalCentreOfMass { get; set; } = Vector3.Zero;
        public float InvMass { get; private set; }
        public Vector3 InvInertiaLocal { get; private set; } = Vector3.Zero;
        public ColliderShape Shape { get; private set; }
        public void ApplyForce(Vector3 force)
        {
            if (BodyType != BodyType.Dynamic || IsSleeping) return;
            _forceAccum += force;
        }
        public void ApplyForceAtPoint(Vector3 force, Vector3 worldPoint)
        {
            if (BodyType != BodyType.Dynamic || IsSleeping) return;
            _forceAccum += force;
            Vector3 r = worldPoint - WorldCentreOfMass;
            _torqueAccum += Vector3.Cross(r, force);
        }
        public void ApplyTorque(Vector3 torque)
        {
            if (BodyType != BodyType.Dynamic || IsSleeping) return;
            _torqueAccum += torque;
        }
        public void ClearForces()
        {
            _forceAccum = Vector3.Zero;
            _torqueAccum = Vector3.Zero;
        }
        public void Integrate(float dt)
        {
            if (BodyType == BodyType.Static || IsSleeping) return;
            if (BodyType == BodyType.Kinematic)
            {
                Position += Velocity * dt;
                return;
            }
            if (InvMass > 0f)
            {
                Vector3 dampingForce = -Velocity * (LinearDamping * Mass);
                _forceAccum += dampingForce;
                Vector3 acceleration = _forceAccum * InvMass;
                Velocity += acceleration * dt;
                Position += Velocity * dt;
            }
            if (InvInertiaLocal != Vector3.Zero)
            {
                Vector3 dampingTorque = -AngularVelocity * AngularDamping;
                _torqueAccum += dampingTorque;
                Vector3 angularAccel = ApplyInvInertiaWorld(_torqueAccum);
                AngularVelocity += angularAccel * dt;
                if (AngularVelocity.LengthSquared() > 1e-12f)
                {
                    Quaternion omegaQ = new Quaternion(AngularVelocity.X, AngularVelocity.Y, AngularVelocity.Z, 0f);
                    Quaternion dq = Quaternion.Multiply(omegaQ, Rotation) * 0.5f;
                    Rotation = Quaternion.Normalize(Rotation + dq * dt);
                }
            }
            ClearForces();
        }
        public void InvalidateShape()
        {
            Shape = null;
        }
        public void RebuildShape(FBXModel model = null)
        {
            if (BodyType == BodyType.Kinematic)
            {
                Shape = new CapsuleShape(0.4f, 1.8f);
            }
            else if (model != null && model.Meshes != null && model.Meshes.Count > 0)
            {
                if (BodyType == BodyType.Dynamic)
                {
                    var tempMesh = new TriangleMeshShape(model);
                    Vector3 com = tempMesh.LocalCentreOfMass;
                    float radius = tempMesh.BoundingRadius;
                    if (radius < 0.001f) radius = 0.5f;
                    Shape = new SphereShape(radius, com);
                }
                else
                {
                    Shape = new TriangleMeshShape(model);
                }
            }
            else
            {
                BuildObbFromActualBounds();
            }
            RecomputeMassProperties();
        }
        private bool HasValidLocalBounds()
        {
            return LocalBoundsMinCm.X <= LocalBoundsMaxCm.X
                && LocalBoundsMinCm.Y <= LocalBoundsMaxCm.Y
                && LocalBoundsMinCm.Z <= LocalBoundsMaxCm.Z
                && !float.IsInfinity(LocalBoundsMinCm.X) && !float.IsInfinity(LocalBoundsMaxCm.X);
        }
        private void BuildObbFromActualBounds()
        {
            Vector3 half;
            Vector3 centerOffset = Vector3.Zero;
            if (HasValidLocalBounds())
            {
                Vector3 sizeM = LocalBoundsMaxCm - LocalBoundsMinCm;
                half = sizeM * 0.5f;
                centerOffset = (LocalBoundsMinCm + LocalBoundsMaxCm) * 0.5f;
            }
            else
            {
                half = Size * 0.5f;
            }
            Shape = new ObbShape(half, centerOffset);
        }
        public void RecomputeMassProperties()
        {
            if (BodyType == BodyType.Static || BodyType == BodyType.Kinematic)
            {
                InvMass = 0f;
                InvInertiaLocal = Vector3.Zero;
                if (BodyType == BodyType.Static)
                    LocalCentreOfMass = Vector3.Zero;
                else if (Shape is CapsuleShape cap)
                    LocalCentreOfMass = new Vector3(0f, 0f, cap.Height * 0.5f);
                return;
            }
            InvMass = 1f / MathF.Max(0.001f, _mass);
            if (Shape is CapsuleShape capDyn)
            {
                LocalCentreOfMass = new Vector3(0f, 0f, capDyn.Height * 0.5f);
                float hx = capDyn.Radius * 2f;
                float hy = capDyn.Radius * 2f;
                float hz = capDyn.Height;
                InvInertiaLocal = ComputeBoxInvInertia(_mass, hx, hy, hz);
            }
            else if (Shape is SphereShape sphere)
            {
                LocalCentreOfMass = sphere.CenterOffset;
                float i = 0.4f * _mass * sphere.Radius * sphere.Radius;
                float inv = i > 1e-8f ? 1f / i : 0f;
                InvInertiaLocal = new Vector3(inv, inv, inv);
            }
            else if (Shape is ObbShape obb)
            {
                LocalCentreOfMass = obb.CenterOffset;
                float hx = obb.HalfExtents.X * 2f;
                float hy = obb.HalfExtents.Y * 2f;
                float hz = obb.HalfExtents.Z * 2f;
                InvInertiaLocal = ComputeBoxInvInertia(_mass, hx, hy, hz);
            }
            else if (Shape is TriangleMeshShape mesh)
            {
                LocalCentreOfMass = mesh.LocalCentreOfMass;
                if (HasValidLocalBounds())
                {
                    Vector3 size = LocalBoundsMaxCm - LocalBoundsMinCm;
                    InvInertiaLocal = ComputeBoxInvInertia(_mass, size.X, size.Y, size.Z);
                }
                else
                {
                    InvInertiaLocal = ComputeBoxInvInertia(_mass, Size.X, Size.Y, Size.Z);
                }
            }
            else
            {
                LocalCentreOfMass = Vector3.Zero;
                InvInertiaLocal = Vector3.Zero;
            }
        }
        private static Vector3 ComputeBoxInvInertia(float mass, float hx, float hy, float hz)
        {
            float ixx = mass * (hy * hy + hz * hz) / 12f;
            float iyy = mass * (hx * hx + hz * hz) / 12f;
            float izz = mass * (hx * hx + hy * hy) / 12f;
            return new Vector3(
                ixx > 1e-8f ? 1f / ixx : 0f,
                iyy > 1e-8f ? 1f / iyy : 0f,
                izz > 1e-8f ? 1f / izz : 0f);
        }
        public Vector3 WorldCentreOfMass
        {
            get
            {
                if (LocalCentreOfMass == Vector3.Zero)
                    return Position;
                return Position + Vector3.Transform(LocalCentreOfMass, Rotation);
            }
        }
        public Vector3 ApplyInvInertiaWorld(Vector3 worldTorque)
        {
            if (InvInertiaLocal == Vector3.Zero)
                return Vector3.Zero;
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4.Invert(rot, out Matrix4x4 invRot);
            Vector3 local = Vector3.TransformNormal(worldTorque, invRot);
            local = new Vector3(
                local.X * InvInertiaLocal.X,
                local.Y * InvInertiaLocal.Y,
                local.Z * InvInertiaLocal.Z);
            return Vector3.TransformNormal(local, rot);
        }
        public void Break()
        {
            if (IsBreakable && _health <= 0)
                IsBroken = true;
        }
        public bool RayIntersects(Vector3 rayOrigin, Vector3 rayDir, out float distance, out Vector3 hitPoint)
        {
            distance = 0f;
            hitPoint = Vector3.Zero;
            if (rayDir.LengthSquared() < 1e-8f) return false;
            if (Shape != null)
            {
                if (Shape.Raycast(Position, Rotation, rayOrigin, rayDir, float.MaxValue, out distance, out _))
                {
                    hitPoint = rayOrigin + rayDir * distance;
                    return true;
                }
                return false;
            }
            Matrix4x4 scaleMat = Matrix4x4.CreateScale(Scale);
            Matrix4x4 rotMat = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4 transMat = Matrix4x4.CreateTranslation(Position);
            Matrix4x4 modelMat = scaleMat * rotMat * transMat;
            if (!Matrix4x4.Invert(modelMat, out Matrix4x4 worldToLocal)) return false;
            Vector3 localOrigin = Vector3.Transform(rayOrigin, worldToLocal);
            Vector3 localDir = Vector3.TransformNormal(rayDir, worldToLocal);
            localDir = Vector3.Normalize(localDir);
            Vector3 boxMin;
            Vector3 boxMax;
            if (HasValidLocalBounds())
            {
                boxMin = LocalBoundsMinCm;
                boxMax = LocalBoundsMaxCm;
            }
            else
            {
                Vector3 localHalfExtents = Size * 0.5f;
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
            distance = tmin;
            hitPoint = rayOrigin + rayDir * distance;
            return true;
        }
        public object ToSerializableData()
        {
            Vector3 safeMin = HasValidLocalBounds() ? LocalBoundsMinCm : Vector3.Zero;
            Vector3 safeMax = HasValidLocalBounds() ? LocalBoundsMaxCm : Vector3.Zero;
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
                LocalBoundsMinCm = safeMin,
                LocalBoundsMaxCm = safeMax,
                Velocity = Velocity,
                BodyType = (int)BodyType,
                AngularVelocity = AngularVelocity,
                LinearDamping = LinearDamping,
                AngularDamping = AngularDamping,
                Friction = Friction,
                Restitution = Restitution,
                KineticFriction = KineticFriction,
                StaticFriction = StaticFriction,
                RollingResistance = RollingResistance,
                IsSleeping = IsSleeping,
                IslandId = IslandId,
                SleepThreshold = SleepThreshold,
                CollisionEnabled = CollisionEnabled,
                IsGrounded = IsGrounded,
                SlopeLimitDegrees = SlopeLimitDegrees,
                StepHeight = StepHeight,
                LocalCentreOfMass = LocalCentreOfMass,
                InvMass = InvMass,
                InvInertiaLocal = InvInertiaLocal
            };
        }
        public void FromSerializableData(object data)
        {
            if (data == null) return;
            if (data is PhysicsComponentData p)
            {
                ApplyData(
                    p.Position, p.Rotation, p.Scale, p.Size,
                    p.Mass, p.Health, p.IsBreakable, p.IsBroken,
                    p.LocalBoundsMinCm, p.LocalBoundsMaxCm, p.Velocity,
                    p.BodyType, p.AngularVelocity,
                    p.LinearDamping, p.AngularDamping, p.Friction, p.Restitution,
                    p.KineticFriction, p.StaticFriction, p.RollingResistance,
                    p.IsSleeping, p.IslandId, p.SleepThreshold, p.CollisionEnabled,
                    p.IsGrounded, p.SlopeLimitDegrees, p.StepHeight,
                    p.LocalCentreOfMass, p.InvMass, p.InvInertiaLocal);
                return;
            }
            if (data is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                ApplyFromJsonElement(je);
                return;
            }
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
            float kineticFriction = ReadFloat(je, "KineticFriction", KineticFriction);
            float staticFriction = ReadFloat(je, "StaticFriction", StaticFriction);
            float rollingResistance = ReadFloat(je, "RollingResistance", RollingResistance);
            bool isSleeping = ReadBool(je, "IsSleeping", IsSleeping);
            int islandId = ReadInt(je, "IslandId", IslandId);
            float sleepThreshold = ReadFloat(je, "SleepThreshold", SleepThreshold);
            bool collisionEnabled = ReadBool(je, "CollisionEnabled", CollisionEnabled);
            bool isGrounded = ReadBool(je, "IsGrounded", IsGrounded);
            float slopeLimitDegrees = ReadFloat(je, "SlopeLimitDegrees", SlopeLimitDegrees);
            float stepHeight = ReadFloat(je, "StepHeight", StepHeight);
            Vector3 localCentreOfMass = ReadVector3(je, "LocalCentreOfMass", LocalCentreOfMass);
            float invMass = ReadFloat(je, "InvMass", InvMass);
            Vector3 invInertiaLocal = ReadVector3(je, "InvInertiaLocal", InvInertiaLocal);
            ApplyData(
                position, rotation, scale, size,
                mass, health, isBreakable, isBroken,
                localMin, localMax, velocity,
                bodyType, angularVelocity,
                linearDamping, angularDamping, friction, restitution,
                kineticFriction, staticFriction, rollingResistance,
                isSleeping, islandId, sleepThreshold, collisionEnabled,
                isGrounded, slopeLimitDegrees, stepHeight,
                localCentreOfMass, invMass, invInertiaLocal);
        }
        private void ApplyData(
            Vector3 position, Quaternion rotation, Vector3 scale, Vector3 size,
            float mass, float health, bool isBreakable, bool isBroken,
            Vector3 localMin, Vector3 localMax, Vector3 velocity,
            int bodyType, Vector3 angularVelocity,
            float linearDamping, float angularDamping, float friction, float restitution,
            float kineticFriction, float staticFriction, float rollingResistance,
            bool isSleeping, int islandId, float sleepThreshold, bool collisionEnabled,
            bool isGrounded, float slopeLimitDegrees, float stepHeight,
            Vector3 localCentreOfMass, float invMass, Vector3 invInertiaLocal)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Size = size;
            if (mass > 0f) _mass = mass;
            if (health >= 0f) Health = health;
            IsBreakable = isBreakable;
            if (isBroken) Health = 0f;
            LocalBoundsMinCm = localMin;
            LocalBoundsMaxCm = localMax;
            Velocity = velocity;
            BodyType = (BodyType)bodyType;
            AngularVelocity = angularVelocity;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
            Friction = friction;
            Restitution = restitution;
            KineticFriction = kineticFriction;
            StaticFriction = staticFriction;
            RollingResistance = rollingResistance;
            IsSleeping = isSleeping;
            IslandId = islandId;
            SleepThreshold = sleepThreshold;
            CollisionEnabled = collisionEnabled;
            IsGrounded = isGrounded;
            SlopeLimitDegrees = slopeLimitDegrees;
            StepHeight = stepHeight;
            LocalCentreOfMass = localCentreOfMass;
            InvMass = invMass;
            InvInertiaLocal = invInertiaLocal;
            Shape = null;
            RenderPosition = position;
            ClearForces();
            if (Shape != null)
                RecomputeMassProperties();
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
            public float KineticFriction { get; set; }
            public float StaticFriction { get; set; }
            public float RollingResistance { get; set; }
            public bool IsSleeping { get; set; }
            public int IslandId { get; set; }
            public float SleepThreshold { get; set; }
            public bool CollisionEnabled { get; set; }
            public bool IsGrounded { get; set; }
            public float SlopeLimitDegrees { get; set; }
            public float StepHeight { get; set; }
            public Vector3 LocalCentreOfMass { get; set; }
            public float InvMass { get; set; }
            public Vector3 InvInertiaLocal { get; set; }
        }
    }
}