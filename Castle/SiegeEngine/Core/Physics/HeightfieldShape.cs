// Folder: SiegeEngine/Core/Physics
// File: HeightfieldShape.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// First-class heightfield collider. Generates ordinary ContactManifold points
    /// against capsules / OBBs so the heightfield participates in the same solver
    /// as TriangleMeshShape and ObbShape. No special-case position snapping.
    /// </summary>
    public sealed class HeightfieldShape : ColliderShape
    {
        private readonly IHeightProvider _provider;

        public HeightfieldShape(IHeightProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IHeightProvider Provider => _provider;

        public override void GetAabb(in Vector3 position, in Quaternion rotation, out Vector3 min, out Vector3 max)
        {
            // World-space bounds of the entire heightfield (rotation ignored — heightfields are axis-aligned).
            float w = _provider.Width * _provider.WorldScaleX;
            float h = _provider.Height * _provider.WorldScaleZ;
            // Conservative vertical range; real contacts use the sampled height.
            min = new Vector3(0f, 0f, -1000f);
            max = new Vector3(w, h, 1000f);
        }

        public override bool Raycast(in Vector3 position, in Quaternion rotation,
            in Vector3 origin, in Vector3 direction, float maxDistance,
            out float distance, out Vector3 normal)
        {
            // Simple discrete height sampling along the ray (Phase-1).
            distance = 0f;
            normal = Vector3.UnitZ;
            const int steps = 32;
            float step = maxDistance / steps;
            for (int i = 0; i <= steps; i++)
            {
                float t = i * step;
                Vector3 p = origin + direction * t;
                float ground = _provider.GetInterpolatedHeight(p.X, p.Y);
                if (p.Z <= ground)
                {
                    distance = t;
                    normal = SampleNormal(p.X, p.Y);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Finite-difference normal so slopes produce a tilted contact normal
        /// instead of always (0,0,1).
        /// </summary>
        public Vector3 SampleNormal(float worldX, float worldY)
        {
            const float eps = 0.5f;
            float hL = _provider.GetInterpolatedHeight(worldX - eps, worldY);
            float hR = _provider.GetInterpolatedHeight(worldX + eps, worldY);
            float hD = _provider.GetInterpolatedHeight(worldX, worldY - eps);
            float hU = _provider.GetInterpolatedHeight(worldX, worldY + eps);
            Vector3 n = new Vector3(hL - hR, hD - hU, eps * 2f);
            float len = n.Length();
            return len > 1e-6f ? n / len : Vector3.UnitZ;
        }

        public float SampleHeight(float worldX, float worldY)
        {
            return _provider.GetInterpolatedHeight(worldX, worldY);
        }
    }
}