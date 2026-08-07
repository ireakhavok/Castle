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
            LinearDamping = 0.05f;
            AngularDamping = 0.05f;
            Friction = 0.5f;
            Restitution = 0.0f;
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
                {
                    IsBroken = true;
                }
            }
        }
        public Vector3 LocalBoundsMinCm { get; set; } = new Vector3(float.MaxValue);
        public Vector3 LocalBoundsMaxCm { get; set; } = new Vector3(float.MinValue);
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
        public bool CollisionEnabled { get; set; } = true;
        /// <summary>
        /// True when a supporting contact (heightfield or static) with normal within SlopeLimitDegrees was present last physics step.
        /// Used by kinematic Integrate to suppress gravity and residual downward velocity.
        /// </summary>
        public bool IsGrounded { get; set; } = false;
        /// <summary>
        /// Maximum surface angle (degrees from vertical) that still counts as ground for IsGrounded and character response.
        /// </summary>
        public float SlopeLimitDegrees { get; set; } = 50f;
        /// <summary>
        /// Maximum vertical step the kinematic capsule can climb without explicit step-up logic (Phase-1 foundation).
        /// </summary>
        public float StepHeight { get; set; } = 0.35f;
        /// <summary>
        /// Visual-only sample that accounts for fixed-timestep residual time.
        /// Authoritative Position remains discrete; RenderPosition is written by PhysicsWorld after Step.
        /// </summary>
        public Vector3 RenderPosition { get; set; }
        /// <summary>
        /// Local-space centre of mass relative to Position (metres).
        /// </summary>
        public Vector3 LocalCentreOfMass { get; set; } = Vector3.Zero;
        /// <summary>
        /// Inverse mass. Zero for Static (and pure kinematic scenery that should not respond to forces).
        /// </summary>
        public float InvMass { get; private set; }
        /// <summary>
        /// Diagonal inverse inertia tensor in local space (principal axes).
        /// </summary>
        public Vector3 InvInertiaLocal { get; private set; } = Vector3.Zero;
        public ColliderShape Shape { get; private set; }
        /// <summary>
        /// Clears the cached collider so the next RebuildShape / physics step
        /// rebuilds from the current Size / LocalBounds / BodyType.
        /// </summary>
        public void InvalidateShape()
        {
            Shape = null;
        }
        public void RebuildShape(FBXModel model = null)
        {
            switch (BodyType)
            {
                case BodyType.Kinematic:
                    {
                        // Player path calls RebuildShape(null) with no authored bounds → fixed capsule.
                        // Kinematic props (walls, platforms) have LocalBounds from the FBX → OBB of actual size.
                        if (model != null || HasValidLocalBounds())
                        {
                            BuildObbFromActualBounds();
                        }
                        else
                        {
                            Shape = new CapsuleShape(0.4f, 1.8f);
                        }
                        break;
                    }
                case BodyType.Dynamic:
                    {
                        BuildObbFromActualBounds();
                        break;
                    }
                case BodyType.Static:
                default:
                    {
                        if (model != null && model.Meshes != null && model.Meshes.Count > 0)
                        {
                            Shape = new TriangleMeshShape(model);
                        }
                        else
                        {
                            BuildObbFromActualBounds();
                        }
                        break;
                    }
            }
            RecomputeMassProperties();
        }
        private bool HasValidLocalBounds()
        {
            return LocalBoundsMinCm.X <= LocalBoundsMaxCm.X
                && LocalBoundsMinCm.Y <= LocalBoundsMaxCm.Y
                && LocalBoundsMinCm.Z <= LocalBoundsMaxCm.Z;
        }
        private void BuildObbFromActualBounds()
        {
            Vector3 half;
            Vector3 centerOffset = Vector3.Zero;
            if (HasValidLocalBounds())
            {
                // LocalBounds are centimetres. Convert to metres.
                Vector3 sizeM = (LocalBoundsMaxCm - LocalBoundsMinCm) * 0.01f;
                half = sizeM * 0.5f;
                // Geometric centre of the authored AABB relative to the mesh origin (metres).
                centerOffset = (LocalBoundsMinCm + LocalBoundsMaxCm) * 0.005f;
            }
            else
            {
                half = Size * 0.5f;
            }
            Shape = new ObbShape(half, centerOffset);
        }
        /// <summary>
        /// Recomputes InvMass, LocalCentreOfMass and InvInertiaLocal from current BodyType, Mass and Shape.
        /// Safe to call after any change to those fields.
        /// </summary>
        public void RecomputeMassProperties()
        {
            if (BodyType == BodyType.Static)
            {
                InvMass = 0f;
                InvInertiaLocal = Vector3.Zero;
                LocalCentreOfMass = Vector3.Zero;
                return;
            }
            InvMass = 1f / MathF.Max(0.001f, _mass);
            if (Shape is CapsuleShape cap)
            {
                // CoM at geometric centre of the vertical capsule (feet remain at Position).
                LocalCentreOfMass = new Vector3(0f, 0f, cap.Height * 0.5f);
                // Phase-1 character controller: no torque response for the vertical capsule.
                // Props that use Capsule later can be given inertia in a future pass.
                if (BodyType == BodyType.Kinematic)
                {
                    InvInertiaLocal = Vector3.Zero;
                }
                else
                {
                    // Dynamic capsule – treat as bounding box for inertia.
                    float hx = cap.Radius * 2f;
                    float hy = cap.Radius * 2f;
                    float hz = cap.Height;
                    InvInertiaLocal = ComputeBoxInvInertia(_mass, hx, hy, hz);
                }
            }
            else if (Shape is ObbShape obb)
            {
                LocalCentreOfMass = obb.CenterOffset;
                float hx = obb.HalfExtents.X * 2f;
                float hy = obb.HalfExtents.Y * 2f;
                float hz = obb.HalfExtents.Z * 2f;
                InvInertiaLocal = ComputeBoxInvInertia(_mass, hx, hy, hz);
            }
            else
            {
                // TriangleMesh or unknown – treat as static-like for inertia.
                LocalCentreOfMass = Vector3.Zero;
                InvInertiaLocal = Vector3.Zero;
            }
        }
        private static Vector3 ComputeBoxInvInertia(float mass, float hx, float hy, float hz)
        {
            // Ixx = (1/12) m (hy² + hz²), etc.
            float ixx = mass * (hy * hy + hz * hz) / 12f;
            float iyy = mass * (hx * hx + hz * hz) / 12f;
            float izz = mass * (hx * hx + hy * hy) / 12f;
            return new Vector3(
                ixx > 1e-8f ? 1f / ixx : 0f,
                iyy > 1e-8f ? 1f / iyy : 0f,
                izz > 1e-8f ? 1f / izz : 0f);
        }
        /// <summary>
        /// World-space centre of mass.
        /// </summary>
        public Vector3 WorldCentreOfMass
        {
            get
            {
                if (LocalCentreOfMass == Vector3.Zero)
                    return Position;
                return Position + Vector3.Transform(LocalCentreOfMass, Rotation);
            }
        }
        /// <summary>
        /// Applies the local diagonal inverse inertia to a world-space torque vector.
        /// </summary>
        public Vector3 ApplyInvInertiaWorld(Vector3 worldTorque)
        {
            if (InvInertiaLocal == Vector3.Zero)
                return Vector3.Zero;
            // Rotate into local, scale, rotate back.
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
            {
                IsBroken = true;
            }
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
            if (HasValidLocalBounds())
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
            IsSleeping = isSleeping;
            IslandId = islandId;
            SleepThreshold = sleepThreshold;
            CollisionEnabled = collisionEnabled;
            IsGrounded = isGrounded;
            SlopeLimitDegrees = slopeLimitDegrees;
            StepHeight = stepHeight;
            LocalCentreOfMass = localCentreOfMass;
            // Recompute after shape is known; values from payload are used as seed if shape not yet rebuilt.
            InvMass = invMass;
            InvInertiaLocal = invInertiaLocal;
            Shape = null;
            RenderPosition = position;
            // Ensure consistent mass properties once shape is rebuilt later.
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