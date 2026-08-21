// Folder: ToolChest
// File: AcousticDebugOverlay.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.GPU.Compute;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace ToolChest
{
    public class AcousticDebugOverlay : ICustomOverlay
    {
        private readonly IRenderContext _renderContext;
        private readonly Func<IReadOnlyList<Entity>> _getEntities;
        private readonly Func<Vector3> _getListenerPos;
        private readonly Func<IReadOnlyList<Vector3>> _getSourcePositions;
        private readonly Func<IHeightProvider> _getHeightProvider;
        private VertexBuffer _surfaceBuffer;
        private VertexBuffer _lineBuffer;
        private ShaderProgram _shader;
        private readonly List<Vertex> _surfaceVerts = new List<Vertex>(8192);
        private readonly List<uint> _surfaceIndices = new List<uint>(16384);
        private readonly List<Vertex> _lineVerts = new List<Vertex>(256);
        private readonly List<uint> _lineIndices = new List<uint>(512);
        private AcousticGeometry _geometry;
        private AcousticRayTracer _tracer;
        private bool _geometryDirty = true;
        private int _lastEntityCount = -1;
        private bool _wasEnabled;
        private uint _lastPaintedVisibilityVersion = uint.MaxValue;
        public bool Enabled { get; set; } = false;
        public bool ShowListenerRays { get; set; } = true;
        public bool ShowSourceRays { get; set; } = true;
        public bool ShowMeetings { get; set; } = true;
        private static readonly Vector4 ListenerFreeColor = new Vector4(0.20f, 0.55f, 1.00f, 0.55f);
        private static readonly Vector4 SourceFreeColor = new Vector4(1.00f, 0.25f, 0.20f, 0.55f);
        private static readonly Vector4 BounceColor = new Vector4(0.20f, 0.90f, 0.40f, 0.90f);
        private static readonly Vector4 DiffractedColor = new Vector4(0.10f, 0.95f, 1.00f, 0.70f);
        private static readonly Vector4 LosColor = new Vector4(1.00f, 0.95f, 0.20f, 0.95f);
        private static readonly Vector4 PerceivedColor = new Vector4(1.00f, 0.55f, 0.10f, 0.90f);
        public AcousticDebugOverlay(
            IRenderContext renderContext,
            Func<IReadOnlyList<Entity>> getEntities,
            Func<Vector3> getListenerPos,
            Func<IReadOnlyList<Vector3>> getSourcePositions,
            Func<IHeightProvider> getHeightProvider = null)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _getEntities = getEntities ?? throw new ArgumentNullException(nameof(getEntities));
            _getListenerPos = getListenerPos ?? (() => Vector3.Zero);
            _getSourcePositions = getSourcePositions ?? (() => Array.Empty<Vector3>());
            _getHeightProvider = getHeightProvider ?? (() => null);
        }
        public void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight)
        {
        }
        public unsafe void RenderWorld(Matrix4x4 view, Matrix4x4 projection)
        {
            if (!Enabled)
            {
                _wasEnabled = false;
                return;
            }
            if (!_wasEnabled)
            {
                _geometryDirty = true;
                _wasEnabled = true;
                _lastPaintedVisibilityVersion = uint.MaxValue;
            }
            EnsureResources();
            var entities = _getEntities();
            if (entities == null) return;
            if (entities.Count != _lastEntityCount)
            {
                _geometryDirty = true;
                _lastEntityCount = entities.Count;
            }
            if (_geometryDirty)
            {
                IHeightProvider height = null;
                try { height = _getHeightProvider(); } catch { }
                _geometry.Rebuild(entities, height);
                _geometryDirty = false;
                _lastPaintedVisibilityVersion = uint.MaxValue;
            }
            Vector3 listener = _getListenerPos();
            var sources = _getSourcePositions() ?? Array.Empty<Vector3>();
            if (_geometry.TriangleCount > 0)
            {
                _tracer.KickDebugBidirectional(listener, sources);
                if (_tracer.VisibilityVersion != _lastPaintedVisibilityVersion)
                {
                    RebuildSurfaceMesh();
                    _lastPaintedVisibilityVersion = _tracer.VisibilityVersion;
                }
            }
            RebuildLineMesh(listener, sources);
            _shader.Use();
            _shader.SetMatrix4("uView", view);
            _shader.SetMatrix4("uProjection", projection);
            _shader.SetMatrix4("uModel", Matrix4x4.Identity);
            _shader.SetUniform("uPointSize", 4f);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            if (_surfaceBuffer != null && _surfaceIndices.Count > 0)
            {
                _surfaceBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, (uint)_surfaceIndices.Count, _renderContext.Enums.UnsignedInt, null);
            }
            if (_lineBuffer != null && _lineIndices.Count > 0)
            {
                _lineBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Lines, (uint)_lineIndices.Count, _renderContext.Enums.UnsignedInt, null);
            }
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }
        private void RebuildSurfaceMesh()
        {
            _surfaceVerts.Clear();
            _surfaceIndices.Clear();
            if (ShowMeetings)
            {
                foreach (int tri in _tracer.GetMutualFree())
                {
                    if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                    AddFilledTriangle(_surfaceVerts, _surfaceIndices, a, b, c, DiffractedColor);
                }
            }
            if (ShowListenerRays)
            {
                foreach (int tri in _tracer.GetListenerFree())
                {
                    if (_tracer.GetMutualFree().Contains(tri)) continue;
                    if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                    AddFilledTriangle(_surfaceVerts, _surfaceIndices, a, b, c, ListenerFreeColor);
                }
            }
            if (ShowSourceRays)
            {
                foreach (int tri in _tracer.GetSourceFree())
                {
                    if (_tracer.GetMutualFree().Contains(tri)) continue;
                    if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                    AddFilledTriangle(_surfaceVerts, _surfaceIndices, a, b, c, SourceFreeColor);
                }
            }
            if (_surfaceBuffer == null)
                _surfaceBuffer = new VertexBuffer(_renderContext);
            if (_surfaceVerts.Count > 0)
                _surfaceBuffer.UpdateCustom(_surfaceVerts, _surfaceIndices);
        }
        private void RebuildLineMesh(Vector3 listener, IReadOnlyList<Vector3> sources)
        {
            _lineVerts.Clear();
            _lineIndices.Clear();
            AddCross(_lineVerts, _lineIndices, listener, 1.5f, ListenerFreeColor);
            for (int s = 0; s < sources.Count; s++)
                AddCross(_lineVerts, _lineIndices, sources[s], 1.2f, SourceFreeColor);
            if (_geometry.TriangleCount <= 0 || sources.Count == 0)
            {
                if (_lineBuffer == null)
                    _lineBuffer = new VertexBuffer(_renderContext);
                if (_lineVerts.Count > 0)
                    _lineBuffer.UpdateCustom(_lineVerts, _lineIndices);
                return;
            }
            var mutual = _tracer.GetMutualFree();
            for (int s = 0; s < sources.Count; s++)
            {
                Vector3 source = sources[s];
                Vector3 toSource = source - listener;
                float dist = toSource.Length();
                if (dist < 1e-4f) continue;
                Vector3 dir = toSource / dist;
                bool losClear = false;
                if (_geometry.TryClosestHit(listener, dir, out float tHit, out _, out _))
                {
                    if (tHit >= dist * 0.98f) losClear = true;
                }
                else losClear = true;
                if (losClear)
                {
                    AddLine(_lineVerts, _lineIndices, listener, source, LosColor);
                }
                float energy = 0f;
                Vector3 weighted = Vector3.Zero;
                float maxContrib = 0f;
                Vector3 strongestDir = Vector3.Zero;
                foreach (int tri in mutual)
                {
                    if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                    Vector3 centroid = (a + b + c) * (1f / 3f);
                    float rL = Vector3.Distance(centroid, listener);
                    float rS = Vector3.Distance(centroid, source);
                    if (rL < 0.05f || rS < 0.05f) continue;
                    float contrib = 1.0f / (rL * rL * rS * rS);
                    energy += contrib;
                    Vector3 toCent = centroid - listener;
                    float len = toCent.Length();
                    if (len < 1e-5f) continue;
                    Vector3 d = toCent / len;
                    weighted += d * contrib;
                    if (contrib > maxContrib)
                    {
                        maxContrib = contrib;
                        strongestDir = d;
                    }
                }
                if (energy > 1e-12f)
                {
                    Vector3 perceived = weighted.LengthSquared() > 1e-12f ? Vector3.Normalize(weighted) : strongestDir;
                    float logIntensity = MathF.Log(1.0f + energy * 50.0f);
                    float intensity = Math.Clamp(logIntensity / MathF.Log(1.0f + 50.0f), 0.05f, 1.0f);
                    float rayLen = Math.Min(dist * 0.85f, 25.0f) * (0.35f + 0.65f * intensity);
                    Vector3 end = listener + perceived * rayLen;
                    Vector4 col = new Vector4(PerceivedColor.X, PerceivedColor.Y, PerceivedColor.Z, PerceivedColor.W * intensity);
                    AddLine(_lineVerts, _lineIndices, listener, end, col);
                }
                else if (!losClear)
                {
                    // fallback residual for visual completeness when no mutual free surfaces
                    _tracer.KickContinuousTrace(source, listener);
                    var residual = _tracer.ReadCompletedResult();
                    if (residual.ApparentDirection.LengthSquared() > 1e-6f && residual.Intensity > 0.01f)
                    {
                        Vector3 pdir = Vector3.Normalize(residual.ApparentDirection);
                        float rayLen = Math.Min(dist * 0.7f, 20.0f) * (0.3f + 0.7f * residual.Intensity);
                        Vector3 end = listener + pdir * rayLen;
                        Vector4 col = new Vector4(PerceivedColor.X, PerceivedColor.Y, PerceivedColor.Z, PerceivedColor.W * residual.Intensity);
                        AddLine(_lineVerts, _lineIndices, listener, end, col);
                    }
                }
            }
            if (_lineBuffer == null)
                _lineBuffer = new VertexBuffer(_renderContext);
            if (_lineVerts.Count > 0)
                _lineBuffer.UpdateCustom(_lineVerts, _lineIndices);
        }
        private void EnsureResources()
        {
            if (_shader == null)
                _shader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            if (_geometry == null)
                _geometry = new AcousticGeometry(_renderContext);
            if (_tracer == null)
                _tracer = new AcousticRayTracer(_renderContext, _geometry);
        }
        private static void AddLine(List<Vertex> verts, List<uint> indices, Vector3 a, Vector3 b, Vector4 color)
        {
            uint i0 = (uint)verts.Count;
            verts.Add(new Vertex(a.X, a.Y, a.Z, color.X, color.Y, color.Z, color.W));
            verts.Add(new Vertex(b.X, b.Y, b.Z, color.X, color.Y, color.Z, color.W));
            indices.Add(i0);
            indices.Add(i0 + 1);
        }
        private static void AddFilledTriangle(List<Vertex> verts, List<uint> indices, Vector3 a, Vector3 b, Vector3 c, Vector4 color)
        {
            uint i0 = (uint)verts.Count;
            verts.Add(new Vertex(a.X, a.Y, a.Z, color.X, color.Y, color.Z, color.W));
            verts.Add(new Vertex(b.X, b.Y, b.Z, color.X, color.Y, color.Z, color.W));
            verts.Add(new Vertex(c.X, c.Y, c.Z, color.X, color.Y, color.Z, color.W));
            indices.Add(i0);
            indices.Add(i0 + 1);
            indices.Add(i0 + 2);
        }
        private static void AddCross(List<Vertex> verts, List<uint> indices, Vector3 center, float size, Vector4 color)
        {
            AddLine(verts, indices, center - new Vector3(size, 0, 0), center + new Vector3(size, 0, 0), color);
            AddLine(verts, indices, center - new Vector3(0, size, 0), center + new Vector3(0, size, 0), color);
            AddLine(verts, indices, center - new Vector3(0, 0, size), center + new Vector3(0, 0, size), color);
        }
    }
}