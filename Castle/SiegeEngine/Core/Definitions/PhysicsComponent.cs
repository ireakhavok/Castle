// Folder: SiegeEngine/Core/Definitions
// File: PhysicsComponent.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class PhysicsComponent : IComponent
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
        }

        // === FULL BACKWARD COMPATIBILITY LAYER (zero breaking changes) ===
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

        public void Break()
        {
            if (IsBreakable && _health <= 0)
            {
                IsBroken = true;
            }
        }

        /// <summary>
        /// Professional-grade OBB ray intersection test.
        /// Transforms the ray into the entity's local space using LocalToWorld inverse, then performs a robust slab AABB test on [-Size/2, +Size/2].
        /// This is the canonical, reusable hit detection for rotated entities (walls, FBX models, etc.).
        /// Used by EditorScene (right-click selection) and GameServer.RequestRayTrace (authoritative multiplayer).
        /// Strict travel-order first-hit semantics are handled by callers.
        /// </summary>
        public bool RayIntersects(Vector3 rayOrigin, Vector3 rayDir, out float distance, out Vector3 hitPoint)
        {
            distance = 0f;
            hitPoint = Vector3.Zero;
            if (rayDir.LengthSquared() < 1e-8f) return false;

            // Transform ray to local space (exact match to render transform)
            if (!Matrix4x4.Invert(LocalToWorld, out Matrix4x4 worldToLocal)) return false;
            Vector3 localOrigin = Vector3.Transform(rayOrigin, worldToLocal);
            Vector3 localDir = Vector3.TransformNormal(rayDir, worldToLocal);
            localDir = Vector3.Normalize(localDir);

            Vector3 halfSize = Size / 2f;
            Vector3 boxMin = -halfSize;
            Vector3 boxMax = halfSize;

            // Robust slab method for ray (tmin starts at 0, no negative t allowed)
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
    }
}