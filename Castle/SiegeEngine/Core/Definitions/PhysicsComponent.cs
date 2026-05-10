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

        // NEW: exact local AABB in FBX cm units (populated from FBXModel at entity creation)
        // Eliminates centering assumption for walls/prefabs with non-zero pivot.
        // Falls back to symmetric box for legacy/centered models.
        public Vector3 LocalBoundsMinCm { get; set; } = new Vector3(float.MaxValue);
        public Vector3 LocalBoundsMaxCm { get; set; } = new Vector3(float.MinValue);

        public void Break()
        {
            if (IsBreakable && _health <= 0)
            {
                IsBroken = true;
            }
        }

        /// <summary>
        /// Professional-grade OBB ray intersection test.
        /// Transforms the ray into the entity's local space using EXACT render model matrix (scale 0.01f * rot * trans)
        /// then performs a robust slab AABB test in FBX cm space.
        /// This is the canonical, reusable hit detection for rotated entities (walls, FBX models, etc.).
        /// Used by EditorScene (right-click selection) and GameServer.RequestRayTrace (authoritative multiplayer).
        /// Strict travel-order first-hit semantics are handled by callers.
        /// </summary>
        public bool RayIntersects(Vector3 rayOrigin, Vector3 rayDir, out float distance, out Vector3 hitPoint)
        {
            distance = 0f;
            hitPoint = Vector3.Zero;
            if (rayDir.LengthSquared() < 1e-8f) return false;

            // EXACT render model matrix as in SceneEditorPanel.RenderInnerContent
            // (scaleMat 0.01f * rotation * translation) to match FBX cm vertices exactly
            Matrix4x4 scaleMat = Matrix4x4.CreateScale(0.01f);
            Matrix4x4 rotMat = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4 transMat = Matrix4x4.CreateTranslation(Position);
            Matrix4x4 modelMat = scaleMat * rotMat * transMat;

            if (!Matrix4x4.Invert(modelMat, out Matrix4x4 worldToLocal)) return false;

            Vector3 localOrigin = Vector3.Transform(rayOrigin, worldToLocal);
            Vector3 localDir = Vector3.TransformNormal(rayDir, worldToLocal);
            localDir = Vector3.Normalize(localDir);  // normalized for slab t-parameter consistency

            // FBX local space is in CENTIMETERS.
            // Use actual stored local AABB (handles non-centered models like walls) or fallback to symmetric
            Vector3 boxMin;
            Vector3 boxMax;
            if (LocalBoundsMinCm.X < float.MaxValue / 2)
            {
                boxMin = LocalBoundsMinCm;
                boxMax = LocalBoundsMaxCm;
            }
            else
            {
                // Legacy centered fallback (Size already in meters)
                Vector3 localHalfExtents = Size * 50f;
                boxMin = -localHalfExtents;
                boxMax = localHalfExtents;
            }

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

            // Convert local-space tmin (cm scale) back to world distance (meters)
            distance = tmin * 0.01f;
            hitPoint = rayOrigin + rayDir * distance;
            return true;
        }
    }
}