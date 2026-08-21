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
        private uint _lastPerceivedVersion = uint.MaxValue;
        private Vector3 _lastPerceivedListener;
        private readonly List<Vector3> _lastPerceivedSources = new List<Vector3>();
        private readonly List<(bool losClear, Vector3 perceivedDir, float intensity, float pathLength)> _cachedPerceived = new List<(bool, Vector3, float, float)>();
        private const float PerceivedMoveThreshold = 0.75f;
        private const float SpeedOfSound = 34300f;
        public bool Enabled { get; set; } = false;
        public bool ShowListenerRays { get; set; } = true;
        public bool ShowSourceRays { get; set; } = true;
        public bool ShowMeetings { get; set; } = true;
        private static readonly Vector4 ListenerFreeColor = new Vector4(0.20f, 0.55f, 1.00f, 0.55f);
        private static readonly Vector4 SourceFreeColor = new Vector4(1.00f, 0.25f, 0.20f, 0.55f);
        private static readonly Vector4 DiffractedColor = new Vector4(0.10f, 0.95f, 1.00f, 0.70f);
        private static readonly Vector4 LosColor = new Vector4(1.00f, 0.95f, 0.20f, 0.95f);
        private static readonly Vector4 PerceivedColor = new Vector4(1.00f, 0.55f, 0.10f, 0.95f);
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
                _lastPerceivedVersion = uint.MaxValue;
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
                _lastPerceivedVersion = uint.MaxValue;
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
            bool needPerceived =
                _tracer.VisibilityVersion != _lastPerceivedVersion ||
                Vector3.DistanceSquared(listener, _lastPerceivedListener) > PerceivedMoveThreshold * PerceivedMoveThreshold ||
                sources.Count != _lastPerceivedSources.Count;
            if (!needPerceived)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    if (i >= _lastPerceivedSources.Count ||
                        Vector3.DistanceSquared(sources[i], _lastPerceivedSources[i]) > PerceivedMoveThreshold * PerceivedMoveThreshold)
                    {
                        needPerceived = true;
                        break;
                    }
                }
            }
            if (needPerceived)
            {
                ComputePerceived(listener, sources);
                _lastPerceivedVersion = _tracer.VisibilityVersion;
                _lastPerceivedListener = listener;
                _lastPerceivedSources.Clear();
                for (int i = 0; i < sources.Count; i++)
                    _lastPerceivedSources.Add(sources[i]);
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
            // Only mutual (teal) filled surfaces — blue and red removed per request.
            if (ShowMeetings)
            {
                foreach (int tri in _tracer.GetMutualFree())
                {
                    if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                    AddFilledTriangle(_surfaceVerts, _surfaceIndices, a, b, c, DiffractedColor);
                }
            }
            if (_surfaceBuffer == null)
                _surfaceBuffer = new VertexBuffer(_renderContext);
            if (_surfaceVerts.Count > 0)
                _surfaceBuffer.UpdateCustom(_surfaceVerts, _surfaceIndices);
        }
        private void ComputePerceived(Vector3 listener, IReadOnlyList<Vector3> sources)
        {
            _cachedPerceived.Clear();
            if (_geometry.TriangleCount <= 0 || sources.Count == 0)
                return;
            var mutual = _tracer.GetMutualFree();
            for (int s = 0; s < sources.Count; s++)
            {
                Vector3 source = sources[s];
                Vector3 toSource = source - listener;
                float dist = toSource.Length();
                bool losClear = false;
                if (dist > 1e-4f)
                {
                    Vector3 dir = toSource / dist;
                    if (_geometry.TryClosestHit(listener, dir, out float tHit, out _, out _))
                    {
                        if (tHit >= dist * 0.98f) losClear = true;
                    }
                    else losClear = true;
                }
                // Physics: free-surface path intensity = inverse-square on total path length.
                // energy = sum of contributions from every mutual free triangle.
                // perceived direction = energy-weighted average of arrival directions
                // (listener ← free surface). Strongest path length is retained for delay.
                float energy = 0f;
                Vector3 weightedArrival = Vector3.Zero;
                float maxContrib = 0f;
                Vector3 strongestDir = Vector3.Zero;
                float strongestPath = dist;
                foreach (int tri in mutual)
                {
                    if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                    Vector3 centroid = (a + b + c) * (1f / 3f);
                    float rL = Vector3.Distance(centroid, listener);
                    float rS = Vector3.Distance(centroid, source);
                    if (rL < 0.05f || rS < 0.05f) continue;
                    float pathLength = rL + rS;
                    float contrib = 1.0f / (pathLength * pathLength);
                    energy += contrib;
                    Vector3 arrival = centroid - listener;
                    float arrivalLen = arrival.Length();
                    if (arrivalLen < 1e-5f) continue;
                    Vector3 arrivalDir = arrival / arrivalLen;
                    weightedArrival += arrivalDir * contrib;
                    if (contrib > maxContrib)
                    {
                        maxContrib = contrib;
                        strongestDir = arrivalDir;
                        strongestPath = pathLength;
                    }
                }
                Vector3 perceivedDir = Vector3.Zero;
                float intensity = 0f;
                float pathLen = dist;
                if (mutual.Count > 0 && energy > 0f)
                {
                    // Always produce a visible ray when mutual free surfaces exist.
                    perceivedDir = weightedArrival.LengthSquared() > 1e-12f
                        ? Vector3.Normalize(weightedArrival)
                        : strongestDir;
                    // Intensity relative to free-field at the same path length. Pure physics, no artificial floor.
                    float freeField = 1.0f / Math.Max(pathLen * pathLen, 1e-8f);
                    intensity = energy / freeField;
                    pathLen = strongestPath;
                }
                _cachedPerceived.Add((losClear, perceivedDir, intensity, pathLen));
            }
        }
        private void RebuildLineMesh(Vector3 listener, IReadOnlyList<Vector3> sources)
        {
            _lineVerts.Clear();
            _lineIndices.Clear();
            AddCross(_lineVerts, _lineIndices, listener, 1.5f, ListenerFreeColor);
            for (int s = 0; s < sources.Count; s++)
                AddCross(_lineVerts, _lineIndices, sources[s], 1.2f, SourceFreeColor);
            for (int s = 0; s < sources.Count && s < _cachedPerceived.Count; s++)
            {
                Vector3 source = sources[s];
                var (losClear, perceivedDir, intensity, pathLen) = _cachedPerceived[s];
                float dist = Vector3.Distance(listener, source);
                if (losClear)
                {
                    AddLine(_lineVerts, _lineIndices, listener, source, LosColor);
                }
                if (perceivedDir.LengthSquared() > 1e-6f && intensity > 0.01f)
                {
                    float rayLen = Math.Min(Math.Max(pathLen * 0.6f, dist * 0.4f), 30.0f) * (0.4f + 0.6f * Math.Clamp(intensity, 0f, 1f));
                    Vector3 end = listener + perceivedDir * rayLen;
                    Vector4 col = new Vector4(PerceivedColor.X, PerceivedColor.Y, PerceivedColor.Z, PerceivedColor.W * Math.Clamp(intensity, 0.35f, 1.0f));
                    AddLine(_lineVerts, _lineIndices, listener, end, col);
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