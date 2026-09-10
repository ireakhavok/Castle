// Folder: SiegeEngine/Core/Physics
// File: TriangleMeshShape.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Static triangle mesh built from an FBXModel.
    /// Vertices are converted to metres using model.UnitToMeters (1.0 for Blender metres, 0.01 for classic cm).
    /// </summary>
    public sealed class TriangleMeshShape : ColliderShape
    {
        private readonly List<Vector3> _localVerticesM = new List<Vector3>();
        private readonly List<int> _indices = new List<int>();
        private Vector3 _localMin;
        private Vector3 _localMax;
        public Vector3 LocalCentreOfMass { get; private set; }
        public float BoundingRadius { get; private set; }
        private struct Node
        {
            public Vector3 Min, Max;
            public int Left;
            public int Right;
            public int TriStart;
            public int TriCount;
        }
        private Node[] _nodes;
        private int[] _triOrder;
        public TriangleMeshShape(FBXModel model)
            : this(model, null, null)
        {
        }

        public TriangleMeshShape(FBXModel model, IList<int> hiddenMeshIndices, IList<MeshMaterialOption> materialOptions)
        {
            // hiddenMeshIndices / materialOptions ignored — 62f5553 used every FBX triangle.
            if (model == null || model.Meshes == null) return;
            float toMeters = model.UnitToMeters;
            foreach (var mesh in model.Meshes)
            {
                if (mesh.Vertices == null || mesh.Indices == null) continue;
                int baseIndex = _localVerticesM.Count;
                foreach (var v in mesh.Vertices)
                    _localVerticesM.Add(v.Position * toMeters);
                foreach (uint idx in mesh.Indices)
                    _indices.Add(baseIndex + (int)idx);
            }
            if (_localVerticesM.Count == 0)
            {
                _localMin = Vector3.Zero;
                _localMax = Vector3.Zero;
                LocalCentreOfMass = Vector3.Zero;
                BoundingRadius = 0f;
                return;
            }
            _localMin = new Vector3(float.MaxValue);
            _localMax = new Vector3(float.MinValue);
            Vector3 sum = Vector3.Zero;
            foreach (var p in _localVerticesM)
            {
                _localMin = Vector3.Min(_localMin, p);
                _localMax = Vector3.Max(_localMax, p);
                sum += p;
            }
            LocalCentreOfMass = sum / _localVerticesM.Count;
            float maxR2 = 0f;
            foreach (var p in _localVerticesM)
            {
                float d2 = (p - LocalCentreOfMass).LengthSquared();
                if (d2 > maxR2) maxR2 = d2;
            }
            BoundingRadius = MathF.Sqrt(maxR2);
            BuildAabbTree();
        }
        public override void GetAabb(in Vector3 position, in Quaternion rotation, out Vector3 min, out Vector3 max)
        {
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
        public void QueryWorldTriangles(in Vector3 position, in Quaternion rotation,
            in Vector3 queryMin, in Vector3 queryMax,
            List<Vector3> outA, List<Vector3> outB, List<Vector3> outC)
        {
            if (_nodes == null || _nodes.Length == 0) return;
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Matrix4x4.Invert(rot, out Matrix4x4 invRot);
            Vector3[] qCorners = new Vector3[8];
            qCorners[0] = new Vector3(queryMin.X, queryMin.Y, queryMin.Z);
            qCorners[1] = new Vector3(queryMax.X, queryMin.Y, queryMin.Z);
            qCorners[2] = new Vector3(queryMin.X, queryMax.Y, queryMin.Z);
            qCorners[3] = new Vector3(queryMax.X, queryMax.Y, queryMin.Z);
            qCorners[4] = new Vector3(queryMin.X, queryMin.Y, queryMax.Z);
            qCorners[5] = new Vector3(queryMax.X, queryMin.Y, queryMax.Z);
            qCorners[6] = new Vector3(queryMin.X, queryMax.Y, queryMax.Z);
            qCorners[7] = new Vector3(queryMax.X, queryMax.Y, queryMax.Z);
            Vector3 localMin = new Vector3(float.MaxValue);
            Vector3 localMax = new Vector3(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 l = Vector3.Transform(qCorners[i] - position, invRot);
                localMin = Vector3.Min(localMin, l);
                localMax = Vector3.Max(localMax, l);
            }
            QueryNode(0, localMin, localMax, position, rot, outA, outB, outC);
        }
        public void GetWorldTriangles(in Vector3 position, in Quaternion rotation,
            List<Vector3> outA, List<Vector3> outB, List<Vector3> outC)
        {
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            for (int i = 0; i + 2 < _indices.Count; i += 3)
            {
                outA.Add(Vector3.Transform(_localVerticesM[_indices[i]], rot) + position);
                outB.Add(Vector3.Transform(_localVerticesM[_indices[i + 1]], rot) + position);
                outC.Add(Vector3.Transform(_localVerticesM[_indices[i + 2]], rot) + position);
            }
        }
        public void QueryClosestWorldTriangles(in Vector3 position, in Quaternion rotation,
            in Vector3 worldPoint, List<Vector3> outA, List<Vector3> outB, List<Vector3> outC, int count)
        {
            if (count <= 0 || _indices.Count < 3) return;
            if (_nodes == null || _nodes.Length == 0) return;
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Matrix4x4.Invert(rot, out Matrix4x4 invRot);
            Vector3 localPoint = Vector3.Transform(worldPoint - position, invRot);
            var bestDist = new float[count];
            var bestTri = new int[count];
            for (int i = 0; i < count; i++)
            {
                bestDist[i] = float.MaxValue;
                bestTri[i] = -1;
            }
            int found = 0;
            float worst = float.MaxValue;
            QueryClosestNode(0, localPoint, bestDist, bestTri, count, ref found, ref worst);
            for (int i = 0; i < found; i++)
            {
                int t = bestTri[i];
                if (t < 0) continue;
                outA.Add(Vector3.Transform(_localVerticesM[_indices[t * 3]], rot) + position);
                outB.Add(Vector3.Transform(_localVerticesM[_indices[t * 3 + 1]], rot) + position);
                outC.Add(Vector3.Transform(_localVerticesM[_indices[t * 3 + 2]], rot) + position);
            }
        }
        private void QueryClosestNode(int nodeIdx, Vector3 localPoint,
            float[] bestDist, int[] bestTri, int count, ref int found, ref float worst)
        {
            ref Node n = ref _nodes[nodeIdx];
            if (found >= count && DistanceSqPointAabb(localPoint, n.Min, n.Max) >= worst)
                return;
            if (n.Left < 0)
            {
                for (int i = 0; i < n.TriCount; i++)
                {
                    int t = _triOrder[n.TriStart + i];
                    Vector3 a = _localVerticesM[_indices[t * 3]];
                    Vector3 b = _localVerticesM[_indices[t * 3 + 1]];
                    Vector3 c = _localVerticesM[_indices[t * 3 + 2]];
                    Vector3 closest = ClosestPointOnTriangle(localPoint, a, b, c);
                    float d2 = (closest - localPoint).LengthSquared();
                    if (found < count)
                    {
                        bestDist[found] = d2;
                        bestTri[found] = t;
                        found++;
                        if (found == count)
                        {
                            worst = bestDist[0];
                            int worstIdx = 0;
                            for (int k = 1; k < count; k++)
                            {
                                if (bestDist[k] > worst)
                                {
                                    worst = bestDist[k];
                                    worstIdx = k;
                                }
                            }
                        }
                    }
                    else if (d2 < worst)
                    {
                        int worstIdx = 0;
                        worst = bestDist[0];
                        for (int k = 1; k < count; k++)
                        {
                            if (bestDist[k] > worst)
                            {
                                worst = bestDist[k];
                                worstIdx = k;
                            }
                        }
                        bestDist[worstIdx] = d2;
                        bestTri[worstIdx] = t;
                        worst = bestDist[0];
                        for (int k = 1; k < count; k++)
                        {
                            if (bestDist[k] > worst)
                                worst = bestDist[k];
                        }
                    }
                }
                return;
            }
            float dL = DistanceSqPointAabb(localPoint, _nodes[n.Left].Min, _nodes[n.Left].Max);
            float dR = DistanceSqPointAabb(localPoint, _nodes[n.Right].Min, _nodes[n.Right].Max);
            if (dL < dR)
            {
                QueryClosestNode(n.Left, localPoint, bestDist, bestTri, count, ref found, ref worst);
                QueryClosestNode(n.Right, localPoint, bestDist, bestTri, count, ref found, ref worst);
            }
            else
            {
                QueryClosestNode(n.Right, localPoint, bestDist, bestTri, count, ref found, ref worst);
                QueryClosestNode(n.Left, localPoint, bestDist, bestTri, count, ref found, ref worst);
            }
        }
        private static float DistanceSqPointAabb(Vector3 p, Vector3 min, Vector3 max)
        {
            float dx = p.X < min.X ? min.X - p.X : (p.X > max.X ? p.X - max.X : 0f);
            float dy = p.Y < min.Y ? min.Y - p.Y : (p.Y > max.Y ? p.Y - max.Y : 0f);
            float dz = p.Z < min.Z ? min.Z - p.Z : (p.Z > max.Z ? p.Z - max.Z : 0f);
            return dx * dx + dy * dy + dz * dz;
        }
        private void BuildAabbTree()
        {
            int triCount = _indices.Count / 3;
            if (triCount == 0) return;
            _triOrder = new int[triCount];
            for (int i = 0; i < triCount; i++) _triOrder[i] = i;
            var triMin = new Vector3[triCount];
            var triMax = new Vector3[triCount];
            var centroid = new Vector3[triCount];
            for (int t = 0; t < triCount; t++)
            {
                Vector3 a = _localVerticesM[_indices[t * 3]];
                Vector3 b = _localVerticesM[_indices[t * 3 + 1]];
                Vector3 c = _localVerticesM[_indices[t * 3 + 2]];
                triMin[t] = Vector3.Min(a, Vector3.Min(b, c));
                triMax[t] = Vector3.Max(a, Vector3.Max(b, c));
                centroid[t] = (a + b + c) * (1f / 3f);
            }
            var nodes = new List<Node>(triCount * 2);
            BuildRecursive(0, triCount, triMin, triMax, centroid, nodes);
            _nodes = nodes.ToArray();
        }
        private int BuildRecursive(int start, int count, Vector3[] triMin, Vector3[] triMax, Vector3[] centroid, List<Node> nodes)
        {
            int nodeIdx = nodes.Count;
            nodes.Add(default);
            Vector3 bMin = new Vector3(float.MaxValue);
            Vector3 bMax = new Vector3(float.MinValue);
            for (int i = 0; i < count; i++)
            {
                int t = _triOrder[start + i];
                bMin = Vector3.Min(bMin, triMin[t]);
                bMax = Vector3.Max(bMax, triMax[t]);
            }
            const int leafThreshold = 4;
            if (count <= leafThreshold)
            {
                nodes[nodeIdx] = new Node
                {
                    Min = bMin,
                    Max = bMax,
                    Left = -1,
                    Right = -1,
                    TriStart = start,
                    TriCount = count
                };
                return nodeIdx;
            }
            Vector3 cMin = new Vector3(float.MaxValue);
            Vector3 cMax = new Vector3(float.MinValue);
            for (int i = 0; i < count; i++)
            {
                Vector3 c = centroid[_triOrder[start + i]];
                cMin = Vector3.Min(cMin, c);
                cMax = Vector3.Max(cMax, c);
            }
            Vector3 extent = cMax - cMin;
            int axis = 0;
            if (extent.Y > extent.X) axis = 1;
            if (extent.Z > extent[axis]) axis = 2;
            float mid = (cMin[axis] + cMax[axis]) * 0.5f;
            int left = 0;
            for (int i = 0; i < count; i++)
            {
                int t = _triOrder[start + i];
                if (centroid[t][axis] < mid)
                {
                    int tmp = _triOrder[start + left];
                    _triOrder[start + left] = t;
                    _triOrder[start + i] = tmp;
                    left++;
                }
            }
            if (left == 0 || left == count) left = count / 2;
            int leftChild = BuildRecursive(start, left, triMin, triMax, centroid, nodes);
            int rightChild = BuildRecursive(start + left, count - left, triMin, triMax, centroid, nodes);
            nodes[nodeIdx] = new Node
            {
                Min = bMin,
                Max = bMax,
                Left = leftChild,
                Right = rightChild,
                TriStart = 0,
                TriCount = 0
            };
            return nodeIdx;
        }
        private void QueryNode(int nodeIdx, Vector3 localMin, Vector3 localMax,
            Vector3 position, Matrix4x4 rot,
            List<Vector3> outA, List<Vector3> outB, List<Vector3> outC)
        {
            ref Node n = ref _nodes[nodeIdx];
            if (localMax.X < n.Min.X || localMin.X > n.Max.X ||
                localMax.Y < n.Min.Y || localMin.Y > n.Max.Y ||
                localMax.Z < n.Min.Z || localMin.Z > n.Max.Z)
                return;
            if (n.Left < 0)
            {
                for (int i = 0; i < n.TriCount; i++)
                {
                    int t = _triOrder[n.TriStart + i];
                    Vector3 a = Vector3.Transform(_localVerticesM[_indices[t * 3]], rot) + position;
                    Vector3 b = Vector3.Transform(_localVerticesM[_indices[t * 3 + 1]], rot) + position;
                    Vector3 c = Vector3.Transform(_localVerticesM[_indices[t * 3 + 2]], rot) + position;
                    outA.Add(a);
                    outB.Add(b);
                    outC.Add(c);
                }
                return;
            }
            QueryNode(n.Left, localMin, localMax, position, rot, outA, outB, outC);
            QueryNode(n.Right, localMin, localMax, position, rot, outA, outB, outC);
        }
        private static bool RayTriangle(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, Vector3 c,
            out float t, out Vector3 normal)
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
        private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = p - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return a + ab * v;
            }
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return a + ac * w;
            }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return b + (c - b) * w;
            }
            float denom = 1f / (va + vb + vc);
            float v2 = vb * denom;
            float w2 = vc * denom;
            return a + ab * v2 + ac * w2;
        }
    }
}
