// Folder: SiegeEngine/Core/GPU/Compute
// File: AcousticGeometry.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Compute
{
    public unsafe class AcousticGeometry : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct GpuTriangle
        {
            public Vector4 A;
            public Vector4 B;
            public Vector4 C; // .w = material density
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DrawVertex
        {
            public Vector3 Position;
            public int TriangleIndex;
        }

        private readonly IRenderContext _renderContext;
        private readonly ShaderStorageBuffer _ssbo;
        private readonly List<GpuTriangle> _cpuTris = new List<GpuTriangle>(8192);
        private readonly List<Vector3> _tmpA = new List<Vector3>(8192);
        private readonly List<Vector3> _tmpB = new List<Vector3>(8192);
        private readonly List<Vector3> _tmpC = new List<Vector3>(8192);
        private readonly List<Vector3> _hullVerts = new List<Vector3>(512);
        private readonly List<int> _hullFaces = new List<int>(1024);
        private readonly List<DrawVertex> _drawVerts = new List<DrawVertex>(24576);
        private readonly List<uint> _drawIndices = new List<uint>(24576);
        private uint _drawVao;
        private uint _drawVbo;
        private uint _drawIbo;
        private int _drawIndexCount;
        private bool _disposed;
        private int _lastTriangleCount;
        private volatile uint _geometryVersion;

        public int TriangleCount => _lastTriangleCount;
        public ShaderStorageBuffer Buffer => _ssbo;
        public uint GeometryVersion => _geometryVersion;
        public int DrawIndexCount => _drawIndexCount;

        public AcousticGeometry(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _ssbo = new ShaderStorageBuffer(_renderContext);
            _drawVao = _renderContext.GenVertexArray();
            _drawVbo = _renderContext.GenBuffer();
            _drawIbo = _renderContext.GenBuffer();
        }

        public void Rebuild(IReadOnlyList<Entity> entities, IHeightProvider heightProvider = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AcousticGeometry));
            _cpuTris.Clear();
            if (entities == null) return;

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity == null) continue;
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics == null || !physics.CollisionEnabled) continue;
                if (physics.BodyType != BodyType.Static) continue;

                float density = 1.0f;
                float volume = physics.Size.X * physics.Size.Y * physics.Size.Z;
                if (volume > 1e-6f)
                    density = Math.Max(0.1f, physics.Mass / volume);

                if (physics.Shape is TriangleMeshShape mesh)
                {
                    // Convex hull of the real mesh vertices – closed, tight, multi-sided, fewer tris
                    TessellateConvexHullFromMesh(mesh, physics.Position, physics.Rotation, density);
                }
                else
                {
                    Vector3 halfExtents = physics.Size * 0.5f;
                    if (physics.Shape is ObbShape obb)
                        halfExtents = obb.HalfExtents;
                    // Analytic shapes → convex hull of the oriented box corners (exact OBB volume)
                    TessellateConvexHullFromObb(physics.Position, physics.Rotation, halfExtents, density);
                }
            }

            if (heightProvider != null && heightProvider.Width > 1 && heightProvider.Height > 1)
            {
                int stepX = 1;
                int stepY = 1;
                if (heightProvider.Width > 128 || heightProvider.Height > 128)
                {
                    stepX = Math.Max(1, heightProvider.Width / 96);
                    stepY = Math.Max(1, heightProvider.Height / 96);
                }

                float sx = 1f, sy = 1f;
                if (heightProvider is HeightmapAdapter ha)
                {
                    sx = ha.WorldScaleX;
                    sy = ha.WorldScaleZ;
                }

                const float groundDensity = 1.8f;
                for (int ix = 0; ix < heightProvider.Width - stepX; ix += stepX)
                {
                    for (int iy = 0; iy < heightProvider.Height - stepY; iy += stepY)
                    {
                        float x0 = ix * sx;
                        float y0 = iy * sy;
                        float x1 = (ix + stepX) * sx;
                        float y1 = (iy + stepY) * sy;
                        float h00 = heightProvider.GetInterpolatedHeight(x0, y0);
                        float h10 = heightProvider.GetInterpolatedHeight(x1, y0);
                        float h01 = heightProvider.GetInterpolatedHeight(x0, y1);
                        float h11 = heightProvider.GetInterpolatedHeight(x1, y1);
                        Vector3 p00 = new Vector3(x0, y0, h00);
                        Vector3 p10 = new Vector3(x1, y0, h10);
                        Vector3 p01 = new Vector3(x0, y1, h01);
                        Vector3 p11 = new Vector3(x1, y1, h11);
                        AddTri(p00, p10, p11, groundDensity);
                        AddTri(p00, p11, p01, groundDensity);
                    }
                }
            }

            Upload();
            BuildDrawBuffer();
        }

        private void TessellateConvexHullFromMesh(TriangleMeshShape mesh, Vector3 position, Quaternion rotation, float density)
        {
            _tmpA.Clear();
            _tmpB.Clear();
            _tmpC.Clear();
            mesh.GetWorldTriangles(position, rotation, _tmpA, _tmpB, _tmpC);

            _hullVerts.Clear();
            var seen = new HashSet<long>();
            void AddUnique(Vector3 v)
            {
                long key = ((long)(v.X * 1000f)) ^ (((long)(v.Y * 1000f)) << 21) ^ (((long)(v.Z * 1000f)) << 42);
                if (seen.Add(key))
                    _hullVerts.Add(v);
            }

            for (int i = 0; i < _tmpA.Count; i++)
            {
                AddUnique(_tmpA[i]);
                AddUnique(_tmpB[i]);
                AddUnique(_tmpC[i]);
            }

            if (_hullVerts.Count < 4)
            {
                // Degenerate – fall back to dual-sided mesh
                float eps = 0.35f;
                for (int t = 0; t < _tmpA.Count && t < 512; t++)
                {
                    Vector3 a = _tmpA[t], b = _tmpB[t], c = _tmpC[t];
                    Vector3 n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                    AddTri(a + n * eps, b + n * eps, c + n * eps, density);
                    AddTri(a - n * eps, c - n * eps, b - n * eps, density);
                }
                return;
            }

            BuildAndEmitConvexHull(density);
        }

        private void TessellateConvexHullFromObb(Vector3 position, Quaternion rotation, Vector3 halfExtents, float density)
        {
            float eps = 0.30f;
            Vector3 he = halfExtents + new Vector3(eps);
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);

            _hullVerts.Clear();
            _hullVerts.Add(position + Vector3.Transform(new Vector3(-he.X, -he.Y, -he.Z), rot));
            _hullVerts.Add(position + Vector3.Transform(new Vector3(he.X, -he.Y, -he.Z), rot));
            _hullVerts.Add(position + Vector3.Transform(new Vector3(he.X, he.Y, -he.Z), rot));
            _hullVerts.Add(position + Vector3.Transform(new Vector3(-he.X, he.Y, -he.Z), rot));
            _hullVerts.Add(position + Vector3.Transform(new Vector3(-he.X, -he.Y, he.Z), rot));
            _hullVerts.Add(position + Vector3.Transform(new Vector3(he.X, -he.Y, he.Z), rot));
            _hullVerts.Add(position + Vector3.Transform(new Vector3(he.X, he.Y, he.Z), rot));
            _hullVerts.Add(position + Vector3.Transform(new Vector3(-he.X, he.Y, he.Z), rot));

            BuildAndEmitConvexHull(density);
        }

        private void BuildAndEmitConvexHull(float density)
        {
            _hullFaces.Clear();
            if (!ComputeConvexHull(_hullVerts, _hullFaces))
                return;

            // Emit hull faces with a small outward offset so the continuous ID depth is solid
            float eps = Math.Max(0.25f, 0.08f * Math.Clamp(density, 0.5f, 4f));
            for (int f = 0; f < _hullFaces.Count; f += 3)
            {
                Vector3 a = _hullVerts[_hullFaces[f]];
                Vector3 b = _hullVerts[_hullFaces[f + 1]];
                Vector3 c = _hullVerts[_hullFaces[f + 2]];
                Vector3 n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                AddTri(a + n * eps, b + n * eps, c + n * eps, density);
            }

            // Dual shell (outer) for reliable occlusion on thin shapes
            float outerEps = eps * 1.8f;
            for (int f = 0; f < _hullFaces.Count; f += 3)
            {
                Vector3 a = _hullVerts[_hullFaces[f]];
                Vector3 b = _hullVerts[_hullFaces[f + 1]];
                Vector3 c = _hullVerts[_hullFaces[f + 2]];
                Vector3 n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                AddTri(a + n * outerEps, b + n * outerEps, c + n * outerEps, density);
            }
        }

        /// <summary>
        /// Lightweight incremental 3D convex hull.
        /// Produces a closed solid that fully contains the input points.
        /// </summary>
        private static bool ComputeConvexHull(List<Vector3> points, List<int> outFaces)
        {
            int n = points.Count;
            if (n < 4) return false;

            // Find initial tetrahedron (four non-coplanar points)
            int i0 = 0, i1 = -1, i2 = -1, i3 = -1;
            float best = 0f;
            for (int i = 1; i < n; i++)
            {
                float d = Vector3.DistanceSquared(points[i0], points[i]);
                if (d > best) { best = d; i1 = i; }
            }
            if (i1 < 0) return false;

            best = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i == i0 || i == i1) continue;
                Vector3 ab = points[i1] - points[i0];
                Vector3 ac = points[i] - points[i0];
                float area = Vector3.Cross(ab, ac).LengthSquared();
                if (area > best) { best = area; i2 = i; }
            }
            if (i2 < 0) return false;

            best = 0f;
            Vector3 nrm = Vector3.Normalize(Vector3.Cross(points[i1] - points[i0], points[i2] - points[i0]));
            for (int i = 0; i < n; i++)
            {
                if (i == i0 || i == i1 || i == i2) continue;
                float d = Math.Abs(Vector3.Dot(points[i] - points[i0], nrm));
                if (d > best) { best = d; i3 = i; }
            }
            if (i3 < 0 || best < 1e-8f) return false;

            // Orient tetrahedron so faces have outward normals
            Vector3 centroid = (points[i0] + points[i1] + points[i2] + points[i3]) * 0.25f;
            void AddFace(int a, int b, int c)
            {
                Vector3 n = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                if (Vector3.Dot(n, centroid - points[a]) > 0f)
                    outFaces.AddRange(new[] { a, c, b });
                else
                    outFaces.AddRange(new[] { a, b, c });
            }

            AddFace(i0, i1, i2);
            AddFace(i0, i2, i3);
            AddFace(i0, i3, i1);
            AddFace(i1, i3, i2);

            // Incremental insertion of remaining points
            var visible = new List<int>(32);
            var horizon = new List<(int, int)>(32);
            var edgeCount = new Dictionary<(int, int), int>(64);

            for (int p = 0; p < n; p++)
            {
                if (p == i0 || p == i1 || p == i2 || p == i3) continue;

                visible.Clear();
                for (int f = 0; f < outFaces.Count; f += 3)
                {
                    int a = outFaces[f], b = outFaces[f + 1], c = outFaces[f + 2];
                    Vector3 n = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                    if (Vector3.Dot(n, points[p] - points[a]) > 1e-6f)
                        visible.Add(f);
                }
                if (visible.Count == 0) continue;

                // Build horizon edges
                edgeCount.Clear();
                horizon.Clear();
                foreach (int f in visible)
                {
                    void Edge(int u, int v)
                    {
                        var e = u < v ? (u, v) : (v, u);
                        if (!edgeCount.ContainsKey(e)) edgeCount[e] = 0;
                        edgeCount[e]++;
                    }
                    Edge(outFaces[f], outFaces[f + 1]);
                    Edge(outFaces[f + 1], outFaces[f + 2]);
                    Edge(outFaces[f + 2], outFaces[f]);
                }
                foreach (var kv in edgeCount)
                    if (kv.Value == 1)
                        horizon.Add(kv.Key);

                // Remove visible faces (from back so indices stay valid)
                visible.Sort();
                for (int vi = visible.Count - 1; vi >= 0; vi--)
                {
                    int f = visible[vi];
                    outFaces.RemoveRange(f, 3);
                }

                // Stitch new faces from horizon to the new point
                foreach (var (u, v) in horizon)
                {
                    Vector3 n = Vector3.Cross(points[v] - points[u], points[p] - points[u]);
                    if (Vector3.Dot(n, centroid - points[u]) > 0f)
                        outFaces.AddRange(new[] { u, p, v });
                    else
                        outFaces.AddRange(new[] { u, v, p });
                }

                // Update centroid roughly
                centroid = (centroid * (outFaces.Count / 3f) + points[p]) / (outFaces.Count / 3f + 1f);
            }

            return outFaces.Count >= 12;
        }

        private void AddTri(Vector3 a, Vector3 b, Vector3 c, float density)
        {
            _cpuTris.Add(new GpuTriangle
            {
                A = new Vector4(a, 0f),
                B = new Vector4(b, 0f),
                C = new Vector4(c, density)
            });
        }

        private void Upload()
        {
            _lastTriangleCount = _cpuTris.Count;
            if (_lastTriangleCount == 0)
            {
                _cpuTris.Add(new GpuTriangle());
                _lastTriangleCount = 0;
            }
            int byteSize = _cpuTris.Count * sizeof(GpuTriangle);
            fixed (GpuTriangle* ptr = _cpuTris.ToArray())
            {
                _ssbo.SetData((uint)byteSize, ptr, _renderContext.Enums.DynamicDraw);
            }
            _geometryVersion++;
        }

        private void BuildDrawBuffer()
        {
            _drawVerts.Clear();
            _drawIndices.Clear();
            for (int t = 0; t < _lastTriangleCount; t++)
            {
                if (!GetTriangle(t, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                uint baseIdx = (uint)_drawVerts.Count;
                _drawVerts.Add(new DrawVertex { Position = a, TriangleIndex = t });
                _drawVerts.Add(new DrawVertex { Position = b, TriangleIndex = t });
                _drawVerts.Add(new DrawVertex { Position = c, TriangleIndex = t });
                _drawIndices.Add(baseIdx);
                _drawIndices.Add(baseIdx + 1);
                _drawIndices.Add(baseIdx + 2);
            }
            _drawIndexCount = _drawIndices.Count;
            if (_drawIndexCount == 0) return;

            _renderContext.BindVertexArray(_drawVao);
            fixed (DrawVertex* vptr = _drawVerts.ToArray())
            {
                _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _drawVbo);
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer,
                    (uint)(_drawVerts.Count * sizeof(DrawVertex)), vptr, _renderContext.Enums.DynamicDraw);
            }
            fixed (uint* iptr = _drawIndices.ToArray())
            {
                _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _drawIbo);
                _renderContext.BufferData(_renderContext.Enums.ElementArrayBuffer,
                    (uint)(_drawIndices.Count * sizeof(uint)), iptr, _renderContext.Enums.DynamicDraw);
            }

            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false,
                (uint)sizeof(DrawVertex), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribIPointer(1, 1, _renderContext.Enums.Int,
                (uint)sizeof(DrawVertex), (void*)(3 * sizeof(float)));
            _renderContext.BindVertexArray(0);
        }

        public void Draw()
        {
            if (_drawIndexCount == 0) return;
            _renderContext.BindVertexArray(_drawVao);
            _renderContext.DrawElements(_renderContext.Enums.Triangles, (uint)_drawIndexCount,
                _renderContext.Enums.UnsignedInt, null);
            _renderContext.BindVertexArray(0);
        }

        public bool TryClosestHit(Vector3 origin, Vector3 dir, out float tHit, out Vector3 nHit, out float dens)
        {
            tHit = float.MaxValue;
            nHit = Vector3.Zero;
            dens = 1.0f;
            if (_cpuTris.Count == 0) return false;
            float dirLen = dir.Length();
            if (dirLen < 1e-6f) return false;
            Vector3 d = dir / dirLen;
            bool hit = false;
            for (int i = 0; i < _cpuTris.Count; i++)
            {
                var tri = _cpuTris[i];
                Vector3 a = new Vector3(tri.A.X, tri.A.Y, tri.A.Z);
                Vector3 b = new Vector3(tri.B.X, tri.B.Y, tri.B.Z);
                Vector3 c = new Vector3(tri.C.X, tri.C.Y, tri.C.Z);
                if (RayTriangle(origin, d, a, b, c, out float t, out Vector3 n))
                {
                    if (t > 0.001f && t < tHit)
                    {
                        tHit = t;
                        nHit = n;
                        dens = Math.Max(0.1f, tri.C.W);
                        hit = true;
                    }
                }
            }
            return hit;
        }

        public bool GetTriangle(int index, out Vector3 a, out Vector3 b, out Vector3 c)
        {
            a = b = c = Vector3.Zero;
            if (index < 0 || index >= _cpuTris.Count) return false;
            var tri = _cpuTris[index];
            a = new Vector3(tri.A.X, tri.A.Y, tri.A.Z);
            b = new Vector3(tri.B.X, tri.B.Y, tri.B.Z);
            c = new Vector3(tri.C.X, tri.C.Y, tri.C.Z);
            return true;
        }

        private static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t, out Vector3 n)
        {
            t = 0f;
            n = Vector3.Zero;
            Vector3 e1 = b - a;
            Vector3 e2 = c - a;
            Vector3 p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (Math.Abs(det) < 1e-8f) return false;
            float inv = 1.0f / det;
            Vector3 tv = o - a;
            float u = Vector3.Dot(tv, p) * inv;
            if (u < 0.0f || u > 1.0f) return false;
            Vector3 q = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, q) * inv;
            if (v < 0.0f || u + v > 1.0f) return false;
            t = Vector3.Dot(e2, q) * inv;
            if (t < 0.0f) return false;
            n = Vector3.Normalize(Vector3.Cross(e1, e2));
            if (Vector3.Dot(n, d) > 0.0f) n = -n;
            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _ssbo?.Dispose();
                if (_drawVao != 0) _renderContext.DeleteVertexArray(_drawVao);
                if (_drawVbo != 0) _renderContext.DeleteBuffer(_drawVbo);
                if (_drawIbo != 0) _renderContext.DeleteBuffer(_drawIbo);
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}