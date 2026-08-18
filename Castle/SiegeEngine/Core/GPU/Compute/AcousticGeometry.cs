// Folder: SiegeEngine/Core/Rendering/Compute
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
    /// <summary>
    /// Packs low-triangle acoustic proxies (stratified sampling of TriangleMeshShape ≤192 tris
    /// per body, OBB for non-mesh bodies, coarse heightmap) into a GPU SSBO for multipath ray tracing.
    /// Full TriangleMeshShape representation is left untouched for physics.
    /// </summary>
    public unsafe class AcousticGeometry : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct GpuTriangle
        {
            public Vector4 A;
            public Vector4 B;
            public Vector4 C; // .w = material density
        }

        private readonly IRenderContext _renderContext;
        private readonly ShaderStorageBuffer _ssbo;
        private readonly List<GpuTriangle> _cpuTris = new List<GpuTriangle>(2048);
        private readonly List<Vector3> _tmpA = new List<Vector3>(4096);
        private readonly List<Vector3> _tmpB = new List<Vector3>(4096);
        private readonly List<Vector3> _tmpC = new List<Vector3>(4096);
        private bool _disposed;
        private int _lastTriangleCount;
        private volatile uint _geometryVersion;

        public int TriangleCount => _lastTriangleCount;
        public ShaderStorageBuffer Buffer => _ssbo;
        public uint GeometryVersion => _geometryVersion;

        public AcousticGeometry(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _ssbo = new ShaderStorageBuffer(_renderContext);
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
                    TessellateMeshProxy(mesh, physics.Position, physics.Rotation, density);
                }
                else
                {
                    TessellateObb(physics.Position, physics.Rotation, physics.Size * 0.5f, density);
                }
            }

            if (heightProvider != null && heightProvider.Width > 1 && heightProvider.Height > 1)
            {
                const int maxCells = 48;
                int stepX = Math.Max(1, heightProvider.Width / maxCells);
                int stepY = Math.Max(1, heightProvider.Height / maxCells);
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
        }

        private void TessellateMeshProxy(TriangleMeshShape mesh, Vector3 position, Quaternion rotation, float density)
        {
            _tmpA.Clear();
            _tmpB.Clear();
            _tmpC.Clear();
            mesh.GetWorldTriangles(position, rotation, _tmpA, _tmpB, _tmpC);
            int triCount = _tmpA.Count;
            if (triCount == 0) return;

            const int maxTris = 192;
            if (triCount <= maxTris)
            {
                for (int t = 0; t < triCount; t++)
                    AddTri(_tmpA[t], _tmpB[t], _tmpC[t], density);
                return;
            }

            // Uniform stratified sample (step through the triangle list)
            float step = (float)triCount / maxTris;
            for (int i = 0; i < maxTris; i++)
            {
                int t = Math.Min((int)(i * step), triCount - 1);
                AddTri(_tmpA[t], _tmpB[t], _tmpC[t], density);
            }
        }

        private void TessellateObb(Vector3 position, Quaternion rotation, Vector3 halfExtents, float density)
        {
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Vector3 centre = position;
            Vector3 hx = Vector3.Transform(new Vector3(halfExtents.X, 0, 0), rot);
            Vector3 hy = Vector3.Transform(new Vector3(0, halfExtents.Y, 0), rot);
            Vector3 hz = Vector3.Transform(new Vector3(0, 0, halfExtents.Z), rot);
            Vector3[] c = new Vector3[8];
            c[0] = centre - hx - hy - hz;
            c[1] = centre + hx - hy - hz;
            c[2] = centre + hx + hy - hz;
            c[3] = centre - hx + hy - hz;
            c[4] = centre - hx - hy + hz;
            c[5] = centre + hx - hy + hz;
            c[6] = centre + hx + hy + hz;
            c[7] = centre - hx + hy + hz;
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