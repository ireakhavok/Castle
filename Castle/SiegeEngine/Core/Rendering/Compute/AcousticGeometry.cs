// Folder: SiegeEngine/Core/Rendering/Compute
// File: AcousticGeometry.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.Rendering.ContextManagement;

namespace SiegeEngine.Core.Rendering.Compute
{
    /// <summary>
    /// Packs world-space triangles from PhysicsComponents into a GPU SSBO
    /// for acoustic multi-bounce ray tracing.
    /// </summary>
    public unsafe class AcousticGeometry : IDisposable
    {
        // GPU layout: 3 × vec4 per triangle (A.xyz + pad, B.xyz + pad, C.xyz + density)
        // 48 bytes per triangle, 16-byte aligned.
        [StructLayout(LayoutKind.Sequential)]
        public struct GpuTriangle
        {
            public Vector4 A;      // xyz = position, w = unused
            public Vector4 B;      // xyz = position, w = unused
            public Vector4 C;      // xyz = position, w = material density
        }

        private readonly IRenderContext _renderContext;
        private readonly ShaderStorageBuffer _ssbo;
        private readonly List<GpuTriangle> _cpuTris = new List<GpuTriangle>(4096);
        private readonly List<Vector3> _tmpA = new List<Vector3>(512);
        private readonly List<Vector3> _tmpB = new List<Vector3>(512);
        private readonly List<Vector3> _tmpC = new List<Vector3>(512);
        private bool _disposed;
        private int _lastTriangleCount;

        public int TriangleCount => _lastTriangleCount;
        public ShaderStorageBuffer Buffer => _ssbo;

        public AcousticGeometry(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _ssbo = new ShaderStorageBuffer(_renderContext);
        }

        /// <summary>
        /// Rebuild the triangle list from the given entities and upload to the GPU.
        /// Call whenever static geometry changes or at level load.
        /// </summary>
        public void Rebuild(IReadOnlyList<Entity> entities)
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
                if (physics.Shape == null) continue;

                // Skip pure kinematic character capsules for acoustic purposes
                if (physics.BodyType == BodyType.Kinematic && physics.Shape is CapsuleShape)
                    continue;

                float density = 1.0f;
                float volume = physics.Size.X * physics.Size.Y * physics.Size.Z;
                if (volume > 1e-6f)
                    density = Math.Max(0.1f, physics.Mass / volume);

                ExtractTriangles(physics, density);
            }

            Upload();
        }

        private void ExtractTriangles(PhysicsComponent physics, float density)
        {
            var shape = physics.Shape;
            Vector3 pos = physics.Position;
            Quaternion rot = physics.Rotation;

            if (shape is TriangleMeshShape mesh)
            {
                _tmpA.Clear();
                _tmpB.Clear();
                _tmpC.Clear();
                mesh.GetWorldTriangles(pos, rot, _tmpA, _tmpB, _tmpC);
                for (int t = 0; t < _tmpA.Count; t++)
                {
                    _cpuTris.Add(new GpuTriangle
                    {
                        A = new Vector4(_tmpA[t], 0f),
                        B = new Vector4(_tmpB[t], 0f),
                        C = new Vector4(_tmpC[t], density)
                    });
                }
            }
            else if (shape is ObbShape obb)
            {
                // Tessellate OBB into 12 triangles (2 per face)
                TessellateObb(pos, rot, obb, density);
            }
            else if (shape is SphereShape sphere)
            {
                // Low-poly icosphere approximation (optional; can be expanded later)
                TessellateSphere(pos, rot, sphere, density, 1);
            }
            // CapsuleShape intentionally skipped for acoustic geometry in the first version
        }

        private void TessellateObb(Vector3 position, Quaternion rotation, ObbShape obb, float density)
        {
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Vector3 centre = position + Vector3.Transform(obb.CenterOffset, rot);
            Vector3 hx = Vector3.Transform(new Vector3(obb.HalfExtents.X, 0, 0), rot);
            Vector3 hy = Vector3.Transform(new Vector3(0, obb.HalfExtents.Y, 0), rot);
            Vector3 hz = Vector3.Transform(new Vector3(0, 0, obb.HalfExtents.Z), rot);

            // 8 corners
            Vector3[] c = new Vector3[8];
            c[0] = centre - hx - hy - hz;
            c[1] = centre + hx - hy - hz;
            c[2] = centre + hx + hy - hz;
            c[3] = centre - hx + hy - hz;
            c[4] = centre - hx - hy + hz;
            c[5] = centre + hx - hy + hz;
            c[6] = centre + hx + hy + hz;
            c[7] = centre - hx + hy + hz;

            // 12 triangles (2 per face)
            AddTri(c[0], c[1], c[2], density);
            AddTri(c[0], c[2], c[3], density);
            AddTri(c[5], c[4], c[7], density);
            AddTri(c[5], c[7], c[6], density);
            AddTri(c[4], c[0], c[3], density);
            AddTri(c[4], c[3], c[7], density);
            AddTri(c[1], c[5], c[6], density);
            AddTri(c[1], c[6], c[2], density);
            AddTri(c[3], c[2], c[6], density);
            AddTri(c[3], c[6], c[7], density);
            AddTri(c[4], c[5], c[1], density);
            AddTri(c[4], c[1], c[0], density);
        }

        private void TessellateSphere(Vector3 position, Quaternion rotation, SphereShape sphere, float density, int subdivisions)
        {
            // Simple octahedron base, one level of subdivision for a usable approximation
            Vector3 centre = position + Vector3.Transform(sphere.CenterOffset, rotation);
            float r = sphere.Radius;

            Vector3[] v =
            {
                centre + new Vector3(0, 0, r),
                centre + new Vector3(0, 0, -r),
                centre + new Vector3(r, 0, 0),
                centre + new Vector3(-r, 0, 0),
                centre + new Vector3(0, r, 0),
                centre + new Vector3(0, -r, 0)
            };

            // 8 faces of octahedron
            AddTri(v[0], v[2], v[4], density);
            AddTri(v[0], v[4], v[3], density);
            AddTri(v[0], v[3], v[5], density);
            AddTri(v[0], v[5], v[2], density);
            AddTri(v[1], v[4], v[2], density);
            AddTri(v[1], v[3], v[4], density);
            AddTri(v[1], v[5], v[3], density);
            AddTri(v[1], v[2], v[5], density);
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
                // Keep a 1-triangle dummy so the SSBO is never empty
                _cpuTris.Add(new GpuTriangle());
                _lastTriangleCount = 0;
            }

            int byteSize = _cpuTris.Count * sizeof(GpuTriangle);
            fixed (GpuTriangle* ptr = _cpuTris.ToArray())
            {
                _ssbo.SetData((uint)byteSize, ptr, _renderContext.Enums.DynamicDraw);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _ssbo?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}