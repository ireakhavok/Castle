// Folder: SiegeEngine/Core/Physics
// File: ObbShape.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Oriented bounding box. Half-extents are in local space (metres).
    /// Used for Dynamic bodies and as the fallback for Static bodies that have no mesh.
    /// </summary>
    public sealed class ObbShape : ColliderShape
    {
        public Vector3 HalfExtents { get; }

        public ObbShape(Vector3 halfExtents)
        {
            HalfExtents = new Vector3(
                MathF.Max(0.001f, halfExtents.X),
                MathF.Max(0.001f, halfExtents.Y),
                MathF.Max(0.001f, halfExtents.Z));
        }

        public override void GetAabb(in Vector3 position, in Quaternion rotation, out Vector3 min, out Vector3 max)
        {
            // Transform the eight corners and take the axis-aligned bounds.
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Vector3 ex = new Vector3(HalfExtents.X, 0, 0);
            Vector3 ey = new Vector3(0, HalfExtents.Y, 0);
            Vector3 ez = new Vector3(0, 0, HalfExtents.Z);

            Vector3 wx = Vector3.TransformNormal(ex, rot);
            Vector3 wy = Vector3.TransformNormal(ey, rot);
            Vector3 wz = Vector3.TransformNormal(ez, rot);

            Vector3 abs = new Vector3(
                MathF.Abs(wx.X) + MathF.Abs(wy.X) + MathF.Abs(wz.X),
                MathF.Abs(wx.Y) + MathF.Abs(wy.Y) + MathF.Abs(wz.Y),
                MathF.Abs(wx.Z) + MathF.Abs(wy.Z) + MathF.Abs(wz.Z));

            min = position - abs;
            max = position + abs;
        }

        public override bool Raycast(in Vector3 position, in Quaternion rotation,
            in Vector3 origin, in Vector3 direction, float maxDistance,
            out float distance, out Vector3 normal)
        {
            // Transform ray into local OBB space and perform AABB slab test.
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Matrix4x4.Invert(rot, out Matrix4x4 invRot);

            Vector3 localOrigin = Vector3.Transform(origin - position, invRot);
            Vector3 localDir = Vector3.TransformNormal(direction, invRot);

            Vector3 boxMin = -HalfExtents;
            Vector3 boxMax = HalfExtents;

            float tmin = 0f;
            float tmax = maxDistance;
            normal = Vector3.Zero;

            for (int i = 0; i < 3; i++)
            {
                if (MathF.Abs(localDir[i]) < 1e-8f)
                {
                    if (localOrigin[i] < boxMin[i] || localOrigin[i] > boxMax[i])
                    {
                        distance = 0f;
                        return false;
                    }
                }
                else
                {
                    float ood = 1f / localDir[i];
                    float t1 = (boxMin[i] - localOrigin[i]) * ood;
                    float t2 = (boxMax[i] - localOrigin[i]) * ood;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    if (t1 > tmin) { tmin = t1; normal = GetFaceNormal(i, localDir[i] < 0f); }
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax)
                    {
                        distance = 0f;
                        return false;
                    }
                }
            }

            distance = tmin;
            if (normal != Vector3.Zero)
                normal = Vector3.TransformNormal(normal, rot);
            return true;
        }

        private static Vector3 GetFaceNormal(int axis, bool negative)
        {
            return axis switch
            {
                0 => negative ? -Vector3.UnitX : Vector3.UnitX,
                1 => negative ? -Vector3.UnitY : Vector3.UnitY,
                _ => negative ? -Vector3.UnitZ : Vector3.UnitZ
            };
        }
    }
}