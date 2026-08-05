// Folder: SiegeEngine/Core/Physics
// File: TriangleMeshShape.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.AssetParsing.Model;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Static triangle mesh built from an FBXModel.
    /// Vertices are stored in local centimetres and converted to metres on the fly
    /// so the same mesh can be shared by many instances without re-baking.
    /// </summary>
    public sealed class TriangleMeshShape : ColliderShape
    {
        private readonly List<Vector3> _localVerticesM = new List<Vector3>(); // already in metres
        private readonly List<int> _indices = new List<int>();
        private Vector3 _localMin;
        private Vector3 _localMax;

        public TriangleMeshShape(FBXModel model)
        {
            if (model == null || model.Meshes == null) return;

            foreach (var mesh in model.Meshes)
            {
                if (mesh.Vertices == null || mesh.Indices == null) continue;

                int baseIndex = _localVerticesM.Count;
                foreach (var v in mesh.Vertices)
                {
                    // FBX is centimetres → metres
                    _localVerticesM.Add(v.Position * 0.01f);
                }
                foreach (uint idx in mesh.Indices)
                {
                    _indices.Add(baseIndex + (int)idx);
                }
            }

            if (_localVerticesM.Count == 0)
            {
                _localMin = Vector3.Zero;
                _localMax = Vector3.Zero;
                return;
            }

            _localMin = new Vector3(float.MaxValue);
            _localMax = new Vector3(float.MinValue);
            foreach (var p in _localVerticesM)
            {
                _localMin = Vector3.Min(_localMin, p);
                _localMax = Vector3.Max(_localMax, p);
            }
        }

        public override void GetAabb(in Vector3 position, in Quaternion rotation, out Vector3 min, out Vector3 max)
        {
            // Transform local AABB corners
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(_localMin.X, _localMin.Y, _localMin.Z);
            corners[1] = new Vector3(_localMax.X, _localMin.Y, _localMin.Z);
            corners[2] = new Vector3(_localMin.X, _localMax.Y, _localMin.Z);
            corners[3] = new Vector3(_localMax.X, _localMax.Y, _localMin.Z);
            corners[4] = new Vector3(_localMin.X, _localMin.Y, _localMax.Z);
            corners[5] = new Vector3(_localMax.X, _localMin.Y, _localMax.Z);
            corners[6] = new Vector3(_localMin.X, _localMax.Y, _localMax.Z);
            corners[7] = new Vector3(_localMax.X, _localMax.Y, _localMax.Z);

            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 w = Vector3.Transform(corners[i], rot) + position;
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
        }

        public override bool Raycast(in Vector3 position, in Quaternion rotation,
            in Vector3 origin, in Vector3 direction, float maxDistance,
            out float distance, out Vector3 normal)
        {
            distance = maxDistance;
            normal = Vector3.Zero;
            bool hit = false;

            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Matrix4x4.Invert(rot, out Matrix4x4 invRot);
            Vector3 localOrigin = Vector3.Transform(origin - position, invRot);
            Vector3 localDir = Vector3.TransformNormal(direction, invRot);

            for (int i = 0; i + 2 < _indices.Count; i += 3)
            {
                Vector3 a = _localVerticesM[_indices[i]];
                Vector3 b = _localVerticesM[_indices[i + 1]];
                Vector3 c = _localVerticesM[_indices[i + 2]];

                if (RayTriangle(localOrigin, localDir, a, b, c, out float t, out Vector3 n) && t < distance)
                {
                    distance = t;
                    normal = Vector3.TransformNormal(n, rot);
                    hit = true;
                }
            }
            return hit;
        }

        /// <summary>
        /// Returns the world-space triangles for narrow-phase contact generation.
        /// Caller supplies the body transform.
        /// </summary>
        public void GetWorldTriangles(in Vector3 position, in Quaternion rotation,
            List<Vector3> outA, List<Vector3> outB, List<Vector3> outC)
        {
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            for (int i = 0; i + 2 < _indices.Count; i += 3)
            {
                Vector3 a = Vector3.Transform(_localVerticesM[_indices[i]], rot) + position;
                Vector3 b = Vector3.Transform(_localVerticesM[_indices[i + 1]], rot) + position;
                Vector3 c = Vector3.Transform(_localVerticesM[_indices[i + 2]], rot) + position;
                outA.Add(a);
                outB.Add(b);
                outC.Add(c);
            }
        }

        private static bool RayTriangle(Vector3 origin, Vector3 dir,
            Vector3 a, Vector3 b, Vector3 c, out float t, out Vector3 normal)
        {
            t = 0f;
            normal = Vector3.Zero;
            Vector3 e1 = b - a;
            Vector3 e2 = c - a;
            Vector3 p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (MathF.Abs(det) < 1e-8f) return false;
            float invDet = 1f / det;
            Vector3 tvec = origin - a;
            float u = Vector3.Dot(tvec, p) * invDet;
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(tvec, e1);
            float v = Vector3.Dot(dir, q) * invDet;
            if (v < 0f || u + v > 1f) return false;
            t = Vector3.Dot(e2, q) * invDet;
            if (t < 0f) return false;
            normal = Vector3.Normalize(Vector3.Cross(e1, e2));
            if (Vector3.Dot(normal, dir) > 0f) normal = -normal;
            return true;
        }
    }
}