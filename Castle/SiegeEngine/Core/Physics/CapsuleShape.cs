// Folder: SiegeEngine/Core/Physics
// File: CapsuleShape.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Vertical capsule used for Kinematic character controllers.
    /// The bottom of the capsule sits exactly on the PhysicsComponent.Position
    /// (the feet / contact point). Height and radius are derived from Size for Phase 1.
    /// </summary>
    public sealed class CapsuleShape : ColliderShape
    {
        public float Radius { get; }
        public float Height { get; }   // total height including the two hemispheres

        public CapsuleShape(float radius, float height)
        {
            Radius = MathF.Max(0.01f, radius);
            Height = MathF.Max(Radius * 2f, height);
        }

        public override void GetAabb(in Vector3 position, in Quaternion rotation, out Vector3 min, out Vector3 max)
        {
            // Vertical capsule – ignore rotation for the Phase-1 character controller
            // (characters never tilt). Centre of the capsule is half-height above the feet.
            float halfH = Height * 0.5f;
            Vector3 centre = position + new Vector3(0f, 0f, halfH);
            Vector3 extents = new Vector3(Radius, Radius, halfH);
            min = centre - extents;
            max = centre + extents;
        }

        public override bool Raycast(in Vector3 position, in Quaternion rotation,
            in Vector3 origin, in Vector3 direction, float maxDistance,
            out float distance, out Vector3 normal)
        {
            // Simple sphere-at-feet + sphere-at-top approximation for Phase 1 raycasts.
            // Full capsule raycast can replace this later without changing callers.
            distance = 0f;
            normal = Vector3.Zero;

            float halfH = Height * 0.5f - Radius;
            if (halfH < 0f) halfH = 0f;

            Vector3 bottom = position + new Vector3(0f, 0f, Radius);
            Vector3 top = position + new Vector3(0f, 0f, Height - Radius);

            if (RaySphere(origin, direction, bottom, Radius, maxDistance, out float t0, out Vector3 n0) && t0 < maxDistance)
            {
                distance = t0;
                normal = n0;
                return true;
            }
            if (RaySphere(origin, direction, top, Radius, maxDistance, out float t1, out Vector3 n1) && t1 < maxDistance)
            {
                distance = t1;
                normal = n1;
                return true;
            }
            return false;
        }

        private static bool RaySphere(Vector3 origin, Vector3 dir, Vector3 centre, float radius,
            float maxDist, out float t, out Vector3 normal)
        {
            t = 0f;
            normal = Vector3.Zero;
            Vector3 m = origin - centre;
            float b = Vector3.Dot(m, dir);
            float c = Vector3.Dot(m, m) - radius * radius;
            if (c > 0f && b > 0f) return false;
            float discr = b * b - c;
            if (discr < 0f) return false;
            t = -b - MathF.Sqrt(discr);
            if (t < 0f) t = 0f;
            if (t > maxDist) return false;
            Vector3 hit = origin + dir * t;
            normal = Vector3.Normalize(hit - centre);
            return true;
        }
    }
}