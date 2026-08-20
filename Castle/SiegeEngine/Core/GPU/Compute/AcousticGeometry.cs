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
        private readonly List<DrawVertex> _drawVerts = new List<DrawVertex>(24576);
        private readonly List<uint> _drawIndices = new List<uint>(24576);

        // Per-static-body proxy cache. Key = Entity.Id. Generation happens only once.
        private readonly Dictionary<int, List<GpuTriangle>> _proxyCache = new Dictionary<int, List<GpuTriangle>>();
        private readonly HashSet<int> _usedThisRebuild = new HashSet<int>();

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
            _usedThisRebuild.Clear();

            if (entities == null)
            {
                Upload();
                BuildDrawBuffer();
                return;
            }

            bool anyNew = false;

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity == null) continue;

                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics == null || !physics.CollisionEnabled) continue;
                if (physics.BodyType != BodyType.Static) continue;

                int id = entity.Id;
                _usedThisRebuild.Add(id);

                if (_proxyCache.TryGetValue(id, out var cached))
                {
                    _cpuTris.AddRange(cached);
                    continue;
                }

                var local = new List<GpuTriangle>(48);
                float density = 1.0f;
                float volume = physics.Size.X * physics.Size.Y * physics.Size.Z;
                if (volume > 1e-6f)
                    density = Math.Max(0.1f, physics.Mass / volume);

                // Correct centre + half-extents that respect mesh origin vs geometric centre.
                GetWorldObb(physics, out Vector3 worldCentre, out Vector3 halfExtents, out Quaternion rotation);
                EmitObbShell(worldCentre, rotation, halfExtents, density, local);

                _proxyCache[id] = local;
                _cpuTris.AddRange(local);
                anyNew = true;
            }

            // Prune cache entries that are no longer present.
            if (_proxyCache.Count > _usedThisRebuild.Count)
            {
                var toRemove = new List<int>();
                foreach (var kv in _proxyCache)
                    if (!_usedThisRebuild.Contains(kv.Key))
                        toRemove.Add(kv.Key);
                for (int r = 0; r < toRemove.Count; r++)
                    _proxyCache.Remove(toRemove[r]);
            }

            // Heightmap contribution (stepped, cheap).
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

            if (anyNew)
                _geometryVersion++;
        }

        /// <summary>
        /// Builds the correct world-space centre and half-extents for a static body.
        /// Respects ObbShape.CenterOffset and LocalBounds so the proxy sits on the
        /// visual geometry even when the FBX origin is at the feet / corner.
        /// </summary>
        private static void GetWorldObb(PhysicsComponent physics, out Vector3 worldCentre, out Vector3 halfExtents, out Quaternion rotation)
        {
            rotation = physics.Rotation;
            Vector3 centerOffset = Vector3.Zero;

            if (physics.Shape is ObbShape obb)
            {
                halfExtents = obb.HalfExtents;
                centerOffset = obb.CenterOffset;
            }
            else if (HasValidLocalBounds(physics))
            {
                Vector3 sizeM = physics.LocalBoundsMaxCm - physics.LocalBoundsMinCm;
                halfExtents = sizeM * 0.5f;
                centerOffset = (physics.LocalBoundsMinCm + physics.LocalBoundsMaxCm) * 0.5f;
            }
            else
            {
                halfExtents = physics.Size * 0.5f;
                centerOffset = Vector3.Zero;
            }

            // Position is the mesh origin; CenterOffset moves us to the geometric centre.
            worldCentre = physics.Position + Vector3.Transform(centerOffset, rotation);
        }

        private static bool HasValidLocalBounds(PhysicsComponent physics)
        {
            return physics.LocalBoundsMinCm.X <= physics.LocalBoundsMaxCm.X
                && physics.LocalBoundsMinCm.Y <= physics.LocalBoundsMaxCm.Y
                && physics.LocalBoundsMinCm.Z <= physics.LocalBoundsMaxCm.Z
                && !float.IsInfinity(physics.LocalBoundsMinCm.X) && !float.IsInfinity(physics.LocalBoundsMaxCm.X)
                && !float.IsNaN(physics.LocalBoundsMinCm.X) && !float.IsNaN(physics.LocalBoundsMaxCm.X);
        }

        /// <summary>
        /// Closed dual-shell OBB centred at the true geometric centre.
        /// Sides stay flat; triangle count stays tiny (24 per body).
        /// </summary>
        private void EmitObbShell(Vector3 worldCentre, Quaternion rotation, Vector3 halfExtents, float density, List<GpuTriangle> target)
        {
            // Tiny numerical expansion so the 96×96 ID depth buffer does not leak
            // on the exact surface. Does not change the overall flat silhouette.
            float eps = 0.12f;
            Vector3 he = halfExtents + new Vector3(eps);
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);

            Vector3[] c = new Vector3[8];
            c[0] = worldCentre + Vector3.Transform(new Vector3(-he.X, -he.Y, -he.Z), rot);
            c[1] = worldCentre + Vector3.Transform(new Vector3(he.X, -he.Y, -he.Z), rot);
            c[2] = worldCentre + Vector3.Transform(new Vector3(he.X, he.Y, -he.Z), rot);
            c[3] = worldCentre + Vector3.Transform(new Vector3(-he.X, he.Y, -he.Z), rot);
            c[4] = worldCentre + Vector3.Transform(new Vector3(-he.X, -he.Y, he.Z), rot);
            c[5] = worldCentre + Vector3.Transform(new Vector3(he.X, -he.Y, he.Z), rot);
            c[6] = worldCentre + Vector3.Transform(new Vector3(he.X, he.Y, he.Z), rot);
            c[7] = worldCentre + Vector3.Transform(new Vector3(-he.X, he.Y, he.Z), rot);

            // Outer shell – 12 triangles
            AddTriTo(target, c[0], c[1], c[2], density);
            AddTriTo(target, c[0], c[2], c[3], density);
            AddTriTo(target, c[4], c[6], c[5], density);
            AddTriTo(target, c[4], c[7], c[6], density);
            AddTriTo(target, c[0], c[4], c[5], density);
            AddTriTo(target, c[0], c[5], c[1], density);
            AddTriTo(target, c[3], c[2], c[6], density);
            AddTriTo(target, c[3], c[6], c[7], density);
            AddTriTo(target, c[0], c[3], c[7], density);
            AddTriTo(target, c[0], c[7], c[4], density);
            AddTriTo(target, c[1], c[5], c[6], density);
            AddTriTo(target, c[1], c[6], c[2], density);

            // Dual (slightly larger) shell for reliable occlusion on thin volumes
            float outer = eps * 1.5f;
            Vector3 he2 = halfExtents + new Vector3(outer);
            c[0] = worldCentre + Vector3.Transform(new Vector3(-he2.X, -he2.Y, -he2.Z), rot);
            c[1] = worldCentre + Vector3.Transform(new Vector3(he2.X, -he2.Y, -he2.Z), rot);
            c[2] = worldCentre + Vector3.Transform(new Vector3(he2.X, he2.Y, -he2.Z), rot);
            c[3] = worldCentre + Vector3.Transform(new Vector3(-he2.X, he2.Y, -he2.Z), rot);
            c[4] = worldCentre + Vector3.Transform(new Vector3(-he2.X, -he2.Y, he2.Z), rot);
            c[5] = worldCentre + Vector3.Transform(new Vector3(he2.X, -he2.Y, he2.Z), rot);
            c[6] = worldCentre + Vector3.Transform(new Vector3(he2.X, he2.Y, he2.Z), rot);
            c[7] = worldCentre + Vector3.Transform(new Vector3(-he2.X, he2.Y, he2.Z), rot);

            AddTriTo(target, c[0], c[1], c[2], density);
            AddTriTo(target, c[0], c[2], c[3], density);
            AddTriTo(target, c[4], c[6], c[5], density);
            AddTriTo(target, c[4], c[7], c[6], density);
            AddTriTo(target, c[0], c[4], c[5], density);
            AddTriTo(target, c[0], c[5], c[1], density);
            AddTriTo(target, c[3], c[2], c[6], density);
            AddTriTo(target, c[3], c[6], c[7], density);
            AddTriTo(target, c[0], c[3], c[7], density);
            AddTriTo(target, c[0], c[7], c[4], density);
            AddTriTo(target, c[1], c[5], c[6], density);
            AddTriTo(target, c[1], c[6], c[2], density);
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

        private static void AddTriTo(List<GpuTriangle> target, Vector3 a, Vector3 b, Vector3 c, float density)
        {
            target.Add(new GpuTriangle
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
                if (RayTriangle(origin, d, a, b, c, out float t, out Vector3 hitN))
                {
                    if (t > 0.001f && t < tHit)
                    {
                        tHit = t;
                        nHit = hitN;
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
                _proxyCache.Clear();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}