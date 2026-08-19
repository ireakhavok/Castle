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
    /// <summary>
    /// Packs low-triangle acoustic proxies into a GPU SSBO.
    /// - TriangleMeshShape: stratified sample of original faces (≤192) with normal expansion.
    /// - Analytic / non-mesh static bodies: oriented 20-face icosahedron (d20)
    ///   scaled to the body's half-extents + conservative expansion (sealed volume).
    ///   Extreme thin bodies receive a dual-shell so linear ray-triangle cannot tunnel.
    /// Dynamic bodies are ignored (physics remains authoritative).
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
        // Pre-computed unit icosahedron (regular, 12 vertices, 20 faces)
        private static readonly Vector3[] IcoVerts;
        private static readonly int[,] IcoFaces;
        static AcousticGeometry()
        {
            const float t = 1.6180339887f; // golden ratio
            IcoVerts = new Vector3[]
            {
                new Vector3(-1,  t,  0), new Vector3( 1,  t,  0),
                new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t),
                new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1),
                new Vector3(-t,  0, -1), new Vector3(-t,  0,  1)
            };
            // Normalise to unit radius
            for (int i = 0; i < IcoVerts.Length; i++)
                IcoVerts[i] = Vector3.Normalize(IcoVerts[i]);
            IcoFaces = new int[20, 3]
            {
                {0,11,5}, {0,5,1}, {0,1,7}, {0,7,10}, {0,10,11},
                {1,5,9}, {5,11,4}, {11,10,2}, {10,7,6}, {7,1,8},
                {3,9,4}, {3,4,2}, {3,2,6}, {3,6,8}, {3,8,9},
                {4,9,5}, {2,4,11}, {6,2,10}, {8,6,7}, {9,8,1}
            };
        }
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
                    // Oriented 20-face icosahedron scaled to half-extents (OBB in the shape of an icosahedron)
                    TessellateIcosahedron(physics.Position, physics.Rotation, physics.Size * 0.5f, density);
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
            // Conservative expansion so stratified samples seal
            float eps = Math.Max(0.18f, 0.08f * Math.Clamp(density, 0.5f, 3f));
            const int maxTris = 192;
            if (triCount <= maxTris)
            {
                for (int t = 0; t < triCount; t++)
                {
                    Vector3 a = _tmpA[t], b = _tmpB[t], c = _tmpC[t];
                    Vector3 n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                    AddTri(a + n * eps, b + n * eps, c + n * eps, density);
                }
                return;
            }
            // Uniform stratified sample + expansion
            float step = (float)triCount / maxTris;
            for (int i = 0; i < maxTris; i++)
            {
                int t = Math.Min((int)(i * step), triCount - 1);
                Vector3 a = _tmpA[t], b = _tmpB[t], c = _tmpC[t];
                Vector3 n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                AddTri(a + n * eps, b + n * eps, c + n * eps, density);
            }
        }
        /// <summary>
        /// Oriented 20-face regular icosahedron (d20) scaled by half-extents.
        /// Larger conservative expansion + dual-shell for extreme thin walls
        /// so linear closestHit cannot tunnel through high-density geometry.
        /// </summary>
        private void TessellateIcosahedron(Vector3 position, Quaternion rotation, Vector3 halfExtents, float density)
        {
            // Density-aware expansion so high-density walls form a solid volume
            float eps = Math.Max(0.22f, 0.10f * Math.Clamp(density, 0.5f, 4f));
            Vector3 he = halfExtents + new Vector3(eps);
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);

            // Detect extreme thin bodies (walls)
            float minAxis = Math.Min(halfExtents.X, Math.Min(halfExtents.Y, halfExtents.Z));
            float maxAxis = Math.Max(halfExtents.X, Math.Max(halfExtents.Y, halfExtents.Z));
            bool thin = (maxAxis > 1e-4f) && (minAxis / maxAxis < 0.30f);

            // Transform the 12 unit vertices into the oriented box
            Vector3[] world = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                Vector3 local = new Vector3(
                    IcoVerts[i].X * he.X,
                    IcoVerts[i].Y * he.Y,
                    IcoVerts[i].Z * he.Z);
                world[i] = position + Vector3.Transform(local, rot);
            }
            for (int f = 0; f < 20; f++)
            {
                int i0 = IcoFaces[f, 0];
                int i1 = IcoFaces[f, 1];
                int i2 = IcoFaces[f, 2];
                AddTri(world[i0], world[i1], world[i2], density);
            }

            // Dual-shell for thin walls: outer shell at 1.5*eps closes the volume
            if (thin)
            {
                Vector3 heOuter = halfExtents + new Vector3(eps * 1.5f);
                for (int i = 0; i < 12; i++)
                {
                    Vector3 local = new Vector3(
                        IcoVerts[i].X * heOuter.X,
                        IcoVerts[i].Y * heOuter.Y,
                        IcoVerts[i].Z * heOuter.Z);
                    world[i] = position + Vector3.Transform(local, rot);
                }
                for (int f = 0; f < 20; f++)
                {
                    int i0 = IcoFaces[f, 0];
                    int i1 = IcoFaces[f, 1];
                    int i2 = IcoFaces[f, 2];
                    AddTri(world[i0], world[i1], world[i2], density);
                }
            }
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