// Folder: SiegeEngine/Core/Physics
// File: SphereShape.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.Physics
{
    public sealed class SphereShape : ColliderShape
    {
        public float Radius { get; }
        public Vector3 CenterOffset { get; }

        public SphereShape(float radius, Vector3 centerOffset = default)
        {
            Radius = MathF.Max(0.001f, radius);
            CenterOffset = centerOffset;
        }

        public override void GetAabb(in Vector3 position, in Quaternion rotation, out Vector3 min, out Vector3 max)
        {
            Vector3 centre = position + Vector3.Transform(CenterOffset, rotation);
            Vector3 extent = new Vector3(Radius);
            min = centre - extent;
            max = centre + extent;
        }

        public override bool Raycast(in Vector3 position, in Quaternion rotation,
            in Vector3 origin, in Vector3 direction, float maxDistance,
            out float distance, out Vector3 normal)
        {
            distance = 0f;
            normal = Vector3.Zero;
            Vector3 centre = position + Vector3.Transform(CenterOffset, rotation);
            Vector3 oc = origin - centre;
            float a = Vector3.Dot(direction, direction);
            float b = 2f * Vector3.Dot(oc, direction);
            float c = Vector3.Dot(oc, oc) - Radius * Radius;
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float sqrt = MathF.Sqrt(disc);
            float t = (-b - sqrt) / (2f * a);
            if (t < 0f || t > maxDistance) return false;
            distance = t;
            Vector3 hit = origin + direction * t;
            normal = Vector3.Normalize(hit - centre);
            return true;
        }
    }
}