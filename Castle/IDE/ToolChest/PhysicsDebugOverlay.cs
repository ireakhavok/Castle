// Folder: ToolChest
// File: PhysicsDebugOverlay.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering.Shaders;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ToolChest
{
    public class PhysicsDebugOverlay : ICustomOverlay
    {
        private readonly IRenderContext _renderContext;
        private readonly Func<IReadOnlyList<Entity>> _getEntities;
        private readonly Func<IReadOnlyList<ContactManifold>> _getManifolds;
        private readonly Func<IReadOnlyList<int>> _getSelectedIds;

        // Local-space geometry caches (uploaded once, GPU transforms thereafter)
        private readonly Dictionary<object, VertexBuffer> _shapeCaches = new Dictionary<object, VertexBuffer>();
        private readonly Dictionary<object, bool> _cacheWasSleeping = new Dictionary<object, bool>();

        // Small dynamic buffer for contacts + velocities + angular
        private VertexBuffer _dynamicBuffer;
        private ShaderProgram _shader;

        private readonly List<Vertex> _dynVerts = new List<Vertex>(512);
        private readonly List<uint> _dynIndices = new List<uint>(1024);
        private readonly List<Vector3> _triA = new List<Vector3>(256);
        private readonly List<Vector3> _triB = new List<Vector3>(256);
        private readonly List<Vector3> _triC = new List<Vector3>(256);

        private readonly List<(Vector3 pos, Vector3 normal, float pen)> _cachedContacts = new List<(Vector3, Vector3, float)>(64);

        private bool _dynamicDirty = true;
        private int _lastAwakeCount = -1;
        private int _lastContactCount = -1;
        private bool _wasEnabled = false;

        public bool Enabled { get; set; } = false;
        public bool ShowShapes { get; set; } = true;
        public bool ShowContacts { get; set; } = true;
        public bool ShowVelocities { get; set; } = true;
        public bool SelectedOnly { get; set; } = false;

        public PhysicsDebugOverlay(
            IRenderContext renderContext,
            Func<IReadOnlyList<Entity>> getEntities,
            Func<IReadOnlyList<ContactManifold>> getManifolds,
            Func<IReadOnlyList<int>> getSelectedIds)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _getEntities = getEntities ?? throw new ArgumentNullException(nameof(getEntities));
            _getManifolds = getManifolds;
            _getSelectedIds = getSelectedIds ?? (() => Array.Empty<int>());
        }

        public void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight) { }

        public unsafe void RenderWorld(Matrix4x4 view, Matrix4x4 projection)
        {
            if (!Enabled)
            {
                _wasEnabled = false;
                return;
            }

            if (!_wasEnabled)
            {
                _dynamicDirty = true;
                _wasEnabled = true;
            }

            if (_shader == null)
                _shader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);

            var entities = _getEntities();
            if (entities == null || entities.Count == 0) return;

            var selected = _getSelectedIds();
            bool filterSelected = SelectedOnly && selected != null && selected.Count > 0;

            int awakeCount = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                var p = e.GetComponent<PhysicsComponent>();
                if (p != null && !p.IsSleeping && p.BodyType == BodyType.Dynamic)
                    awakeCount++;
            }

            // Always keep dynamic buffer live while anything is moving or contacts may change
            if (awakeCount != _lastAwakeCount || awakeCount > 0)
            {
                _dynamicDirty = true;
                _lastAwakeCount = awakeCount;
            }

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _shader.Use();
            _shader.SetMatrix4("uView", view);
            _shader.SetMatrix4("uProjection", projection);
            _shader.SetUniform("uPointSize", 6f);

            // ── Static / local-space shape geometry (GPU transformed) ──────────
            if (ShowShapes)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    if (entity == null) continue;
                    if (filterSelected && (selected == null || !selected.Contains(entity.Id))) continue;

                    var physics = entity.GetComponent<PhysicsComponent>();
                    if (physics == null || physics.Shape == null) continue;

                    object cacheKey = GetCacheKey(physics);

                    // Invalidate cache if sleeping state changed so colour updates
                    if (_cacheWasSleeping.TryGetValue(cacheKey, out bool wasSleeping) && wasSleeping != physics.IsSleeping)
                    {
                        if (_shapeCaches.TryGetValue(cacheKey, out var oldBuf))
                        {
                            oldBuf.Dispose();
                            _shapeCaches.Remove(cacheKey);
                        }
                        _cacheWasSleeping.Remove(cacheKey);
                    }

                    if (!_shapeCaches.TryGetValue(cacheKey, out VertexBuffer buf))
                    {
                        buf = BuildLocalShapeBuffer(physics);
                        if (buf == null) continue;
                        _shapeCaches[cacheKey] = buf;
                        _cacheWasSleeping[cacheKey] = physics.IsSleeping;
                    }

                    Matrix4x4 model = BuildModelMatrix(physics);
                    _shader.SetMatrix4("uModel", model);
                    buf.Bind();
                    _renderContext.DrawElements(_renderContext.Enums.Lines, buf.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                }
            }

            // ── Dynamic contacts + linear + angular velocities ─────────────────
            if (ShowContacts || ShowVelocities)
            {
                if (_dynamicDirty)
                {
                    RebuildDynamicBuffer(entities, selected, filterSelected);
                    _dynamicDirty = false;
                }

                if (_dynamicBuffer != null && _dynamicBuffer.GetIndexCount() > 0)
                {
                    _shader.SetMatrix4("uModel", Matrix4x4.Identity);
                    _dynamicBuffer.Bind();
                    _renderContext.DrawElements(_renderContext.Enums.Lines, _dynamicBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                }
            }

            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        private static object GetCacheKey(PhysicsComponent physics)
        {
            return (physics.Shape, physics.BodyType, physics.IsSleeping);
        }

        private static Matrix4x4 BuildModelMatrix(PhysicsComponent physics)
        {
            if (physics.BodyType == BodyType.Kinematic && physics.Shape is CapsuleShape)
                return Matrix4x4.CreateTranslation(physics.Position);

            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(physics.Rotation);
            rot.Translation = physics.Position;
            return rot;
        }

        private VertexBuffer BuildLocalShapeBuffer(PhysicsComponent physics)
        {
            var verts = new List<Vertex>(512);
            var indices = new List<uint>(1024);
            Vector4 color = BodyColor(physics.BodyType, physics.IsSleeping);

            var shape = physics.Shape;
            if (shape is SphereShape sphere)
            {
                AddLocalSphere(verts, indices, sphere.Radius, color);
            }
            else if (shape is CapsuleShape cap)
            {
                AddLocalCapsule(verts, indices, cap.Radius, cap.Height, color);
            }
            else if (shape is ObbShape obb)
            {
                AddLocalObb(verts, indices, obb.HalfExtents, obb.CenterOffset, color);
            }
            else if (shape is TriangleMeshShape mesh)
            {
                _triA.Clear();
                _triB.Clear();
                _triC.Clear();
                mesh.GetWorldTriangles(Vector3.Zero, Quaternion.Identity, _triA, _triB, _triC);
                int count = Math.Min(_triA.Count, 2048);
                for (int t = 0; t < count; t++)
                {
                    AddLocalLine(verts, indices, _triA[t], _triB[t], color);
                    AddLocalLine(verts, indices, _triB[t], _triC[t], color);
                    AddLocalLine(verts, indices, _triC[t], _triA[t], color);
                }
            }
            else return null;

            if (verts.Count == 0) return null;

            var buf = new VertexBuffer(_renderContext);
            buf.UpdateCustom(verts, indices);
            return buf;
        }

        private void RebuildDynamicBuffer(IReadOnlyList<Entity> entities, IReadOnlyList<int> selected, bool filterSelected)
        {
            _dynVerts.Clear();
            _dynIndices.Clear();

            // Linear + Angular velocity arrows at centre of mass
            if (ShowVelocities)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    if (entity == null) continue;
                    if (filterSelected && (selected == null || !selected.Contains(entity.Id))) continue;

                    var physics = entity.GetComponent<PhysicsComponent>();
                    if (physics == null || physics.BodyType != BodyType.Dynamic) continue;

                    Vector3 com = physics.WorldCentreOfMass;

                    // Linear velocity (green)
                    Vector3 vel = physics.Velocity;
                    float vLen = vel.Length();
                    if (vLen > 0.01f)
                    {
                        float scale = Math.Clamp(vLen * 0.25f, 0.2f, 2.0f);
                        Vector3 end = com + Vector3.Normalize(vel) * scale;
                        AddLocalLine(_dynVerts, _dynIndices, com, end, new Vector4(0.2f, 1.0f, 0.3f, 1.0f));
                    }

                    // Angular velocity (magenta) – direction of spin axis, length = spin rate
                    Vector3 ang = physics.AngularVelocity;
                    float aLen = ang.Length();
                    if (aLen > 0.05f)
                    {
                        float scale = Math.Clamp(aLen * 0.15f, 0.15f, 1.5f);
                        Vector3 end = com + Vector3.Normalize(ang) * scale;
                        AddLocalLine(_dynVerts, _dynIndices, com, end, new Vector4(1.0f, 0.2f, 1.0f, 1.0f));
                    }
                }
            }

            // Contact normals / normal forces (bright orange-red)
            if (ShowContacts)
            {
                if (_getManifolds != null)
                {
                    var manifolds = _getManifolds();
                    if (manifolds != null && manifolds.Count > 0)
                    {
                        _cachedContacts.Clear();
                        for (int m = 0; m < manifolds.Count; m++)
                        {
                            var man = manifolds[m];
                            if (man == null) continue;
                            for (int p = 0; p < man.PointCount; p++)
                            {
                                var cp = man.Points[p];
                                _cachedContacts.Add((cp.Position, cp.Normal, cp.Penetration));
                            }
                        }
                        _lastContactCount = _cachedContacts.Count;
                    }
                }

                // Always draw the last known contacts so resting contacts stay visible
                for (int c = 0; c < _cachedContacts.Count; c++)
                {
                    var (pos, n, pen) = _cachedContacts[c];
                    // Longer, brighter lines so they are obvious
                    float len = Math.Clamp(0.35f + pen * 4.0f, 0.4f, 2.0f);
                    float intensity = Math.Clamp(0.6f + pen * 3.0f, 0.6f, 1.0f);
                    Vector4 color = new Vector4(1.0f, 0.35f, 0.05f, intensity);
                    AddLocalLine(_dynVerts, _dynIndices, pos, pos + n * len, color);
                }
            }

            if (_dynVerts.Count == 0) return;

            if (_dynamicBuffer == null)
                _dynamicBuffer = new VertexBuffer(_renderContext);

            _dynamicBuffer.UpdateCustom(_dynVerts, _dynIndices);
        }

        private static Vector4 BodyColor(BodyType t, bool sleeping)
        {
            Vector4 c = t switch
            {
                BodyType.Dynamic => new Vector4(0.2f, 0.85f, 1.0f, 1.0f),
                BodyType.Kinematic => new Vector4(1.0f, 0.85f, 0.2f, 1.0f),
                _ => new Vector4(0.55f, 0.55f, 0.55f, 0.85f)
            };
            if (sleeping)
            {
                c.X *= 0.45f;
                c.Y *= 0.45f;
                c.Z *= 0.45f;
                c.W = 0.55f;
            }
            return c;
        }

        // ── Local-space geometry helpers ──────────────────────────────────────

        private static void AddLocalLine(List<Vertex> verts, List<uint> indices, Vector3 a, Vector3 b, Vector4 color)
        {
            uint i0 = (uint)verts.Count;
            verts.Add(new Vertex(a.X, a.Y, a.Z, color.X, color.Y, color.Z, color.W));
            verts.Add(new Vertex(b.X, b.Y, b.Z, color.X, color.Y, color.Z, color.W));
            indices.Add(i0);
            indices.Add(i0 + 1);
        }

        private static void AddLocalSphere(List<Vertex> verts, List<uint> indices, float radius, Vector4 color)
        {
            const int segs = 24;
            for (int axis = 0; axis < 3; axis++)
            {
                uint baseIdx = (uint)verts.Count;
                for (int i = 0; i < segs; i++)
                {
                    float a = i * MathF.PI * 2f / segs;
                    float c = MathF.Cos(a) * radius;
                    float s = MathF.Sin(a) * radius;
                    Vector3 p = axis switch
                    {
                        0 => new Vector3(0f, c, s),
                        1 => new Vector3(c, 0f, s),
                        _ => new Vector3(c, s, 0f)
                    };
                    verts.Add(new Vertex(p.X, p.Y, p.Z, color.X, color.Y, color.Z, color.W));
                }
                for (int i = 0; i < segs; i++)
                {
                    indices.Add(baseIdx + (uint)i);
                    indices.Add(baseIdx + (uint)((i + 1) % segs));
                }
            }
        }

        private static void AddLocalCapsule(List<Vertex> verts, List<uint> indices, float radius, float height, Vector4 color)
        {
            Vector3 bottom = new Vector3(0f, 0f, radius);
            Vector3 top = new Vector3(0f, 0f, height - radius);
            const int segs = 16;

            uint baseBottom = (uint)verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float a = i * MathF.PI * 2f / segs;
                Vector3 p = bottom + new Vector3(MathF.Cos(a) * radius, MathF.Sin(a) * radius, 0f);
                verts.Add(new Vertex(p.X, p.Y, p.Z, color.X, color.Y, color.Z, color.W));
            }
            for (int i = 0; i < segs; i++)
            {
                indices.Add(baseBottom + (uint)i);
                indices.Add(baseBottom + (uint)((i + 1) % segs));
            }

            uint baseTop = (uint)verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float a = i * MathF.PI * 2f / segs;
                Vector3 p = top + new Vector3(MathF.Cos(a) * radius, MathF.Sin(a) * radius, 0f);
                verts.Add(new Vertex(p.X, p.Y, p.Z, color.X, color.Y, color.Z, color.W));
            }
            for (int i = 0; i < segs; i++)
            {
                indices.Add(baseTop + (uint)i);
                indices.Add(baseTop + (uint)((i + 1) % segs));
            }

            for (int i = 0; i < 4; i++)
            {
                float a = i * MathF.PI * 0.5f;
                Vector3 offset = new Vector3(MathF.Cos(a) * radius, MathF.Sin(a) * radius, 0f);
                AddLocalLine(verts, indices, bottom + offset, top + offset, color);
            }
        }

        private static void AddLocalObb(List<Vertex> verts, List<uint> indices, Vector3 halfExtents, Vector3 centerOffset, Vector4 color)
        {
            Vector3 centre = centerOffset;
            Vector3 hx = new Vector3(halfExtents.X, 0f, 0f);
            Vector3 hy = new Vector3(0f, halfExtents.Y, 0f);
            Vector3 hz = new Vector3(0f, 0f, halfExtents.Z);

            Vector3[] corners = new Vector3[8];
            corners[0] = centre + hx + hy + hz;
            corners[1] = centre + hx + hy - hz;
            corners[2] = centre + hx - hy + hz;
            corners[3] = centre + hx - hy - hz;
            corners[4] = centre - hx + hy + hz;
            corners[5] = centre - hx + hy - hz;
            corners[6] = centre - hx - hy + hz;
            corners[7] = centre - hx - hy - hz;

            int[,] edges = {
                {0,1},{0,2},{0,4},
                {1,3},{1,5},
                {2,3},{2,6},
                {3,7},
                {4,5},{4,6},
                {5,7},
                {6,7}
            };
            for (int e = 0; e < 12; e++)
                AddLocalLine(verts, indices, corners[edges[e, 0]], corners[edges[e, 1]], color);
        }
    }
}