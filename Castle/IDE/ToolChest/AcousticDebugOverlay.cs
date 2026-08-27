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
    public class AcousticDebugOverlay : ICustomOverlay, IWorldOverlay
    {
        private readonly IRenderContext _renderContext;
        private readonly Func<IReadOnlyList<Entity>> _getEntities;
        private readonly Func<Vector3> _getListenerPos;
        private readonly Func<IReadOnlyList<Vector3>> _getSourcePositions;
        private readonly Func<IHeightProvider> _getHeightProvider;
        private VertexBuffer _surfaceBuffer;
        private VertexBuffer _lineBuffer;
        private LineRenderer _lineRenderer;
        private ShaderProgram _surfaceShader;
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
        private AcousticRayTracer _sharedTracer;
        private AcousticGeometry _sharedGeometry;
        private float _lastTransformSig = float.NaN;
        private bool _hadHeightProvider;
        private bool _awaitingRaster;
        private uint _rasterVersionAtEnable = uint.MaxValue;
        private bool HasSharedProvider => _sharedTracer != null;
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
            _lineRenderer = new LineRenderer(_renderContext);
            _lineRenderer.Initialize();
        }
        public void SetSharedFreeSurface(AcousticRayTracer tracer, AcousticGeometry geometry)
        {
            _sharedTracer = tracer;
            _sharedGeometry = geometry;
            _lastPaintedVisibilityVersion = uint.MaxValue;
            _lastPerceivedVersion = uint.MaxValue;
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
                _awaitingRaster = true;
                _rasterVersionAtEnable = uint.MaxValue;
                _lastTransformSig = float.NaN;
                _hadHeightProvider = false;
            }
            EnsureResources();
            var entities = _getEntities();
            if (entities == null) return;
            IHeightProvider height = null;
            try { height = _getHeightProvider(); } catch { }
            if (height != null && !_hadHeightProvider)
            {
                _geometryDirty = true;
                _hadHeightProvider = true;
            }
            float transformSig = entities.Count;
            for (int i = 0; i < entities.Count; i++)
            {
                var physics = entities[i]?.GetComponent<PhysicsComponent>();
                if (physics == null) continue;
                transformSig += physics.Position.X + physics.Position.Y * 0.13f + physics.Position.Z * 0.37f
                    + physics.Rotation.X + physics.Rotation.Y * 0.17f + physics.Rotation.Z * 0.29f + physics.Rotation.W;
            }
            if (transformSig != _lastTransformSig)
            {
                _geometryDirty = true;
                _lastTransformSig = transformSig;
                _lastPerceivedVersion = uint.MaxValue;
            }
            if (entities.Count != _lastEntityCount)
            {
                _geometryDirty = true;
                _lastEntityCount = entities.Count;
            }
            if (_geometryDirty)
            {
                _geometry.Rebuild(entities, height);
                _geometryDirty = false;
                _lastPaintedVisibilityVersion = uint.MaxValue;
                _lastPerceivedVersion = uint.MaxValue;
                _awaitingRaster = true;
                _rasterVersionAtEnable = uint.MaxValue;
            }
            Vector3 listener = _getListenerPos();
            var sources = _getSourcePositions() ?? Array.Empty<Vector3>();
            AcousticRayTracer activeTracer = HasSharedProvider ? _sharedTracer : _tracer;
            AcousticGeometry activeGeom = HasSharedProvider && _sharedGeometry != null ? _sharedGeometry : _geometry;
            if (activeTracer == null || activeGeom == null || activeGeom.TriangleCount <= 0)
                return;
            if (!HasSharedProvider)
            {
                activeTracer.KickDebugBidirectional(listener, sources);
                activeTracer.TryCompletePendingRaster();
            }
            if (_rasterVersionAtEnable == uint.MaxValue)
                _rasterVersionAtEnable = activeTracer.VisibilityVersion;
            if (activeTracer.VisibilityVersion != _rasterVersionAtEnable)
                _awaitingRaster = false;
            if (activeTracer.VisibilityVersion != _lastPaintedVisibilityVersion)
            {
                RebuildSurfaceMesh(activeTracer, activeGeom);
                _lastPaintedVisibilityVersion = activeTracer.VisibilityVersion;
            }
            bool needPerceived =
                _awaitingRaster ||
                activeTracer.VisibilityVersion != _lastPerceivedVersion ||
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
                ComputePerceived(listener, sources, activeTracer, activeGeom);
                _lastPerceivedVersion = activeTracer.VisibilityVersion;
                _lastPerceivedListener = listener;
                _lastPerceivedSources.Clear();
                for (int i = 0; i < sources.Count; i++)
                    _lastPerceivedSources.Add(sources[i]);
            }
            RebuildLineMesh(listener, sources);

            // Surface triangles (keep minimal local state – not lines)
            if (_surfaceBuffer != null && _surfaceIndices.Count > 0)
            {
                _renderContext.Disable(_renderContext.Enums.DepthTest);
                _renderContext.Enable(_renderContext.Enums.Blend);
                _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
                if (_surfaceShader == null)
                    _surfaceShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
                _surfaceShader.Use();
                _surfaceShader.SetMatrix4("uView", view);
                _surfaceShader.SetMatrix4("uProjection", projection);
                _surfaceShader.SetMatrix4("uModel", Matrix4x4.Identity);
                _surfaceBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, (uint)_surfaceIndices.Count, _renderContext.Enums.UnsignedInt, null);
                _renderContext.Disable(_renderContext.Enums.Blend);
                _renderContext.Enable(_renderContext.Enums.DepthTest);
            }

            // All lines through the generic LineRenderer
            if (_lineBuffer != null && _lineIndices.Count > 0)
                _lineRenderer.DrawLines(_lineBuffer, view, projection, 1f);
        }
        private void RebuildSurfaceMesh(AcousticRayTracer tracer, AcousticGeometry geom)
        {
            _surfaceVerts.Clear();
            _surfaceIndices.Clear();
            if (ShowMeetings)
            {
                foreach (int tri in tracer.GetJoinedMutualFree())
                {
                    if (!geom.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                    AddFilledTriangle(_surfaceVerts, _surfaceIndices, a, b, c, DiffractedColor);
                }
            }
            if (_surfaceBuffer == null)
                _surfaceBuffer = new VertexBuffer(_renderContext);
            if (_surfaceVerts.Count > 0)
                _surfaceBuffer.UpdateCustom(_surfaceVerts, _surfaceIndices);
        }
        private void ComputePerceived(Vector3 listener, IReadOnlyList<Vector3> sources, AcousticRayTracer tracer, AcousticGeometry geom)
        {
            _cachedPerceived.Clear();
            if (geom.TriangleCount <= 0 || sources.Count == 0)
                return;
            for (int s = 0; s < sources.Count; s++)
            {
                Vector3 source = sources[s];
                Vector3 toSource = source - listener;
                float dist = toSource.Length();
                bool losClear = false;
                if (dist > 1e-4f)
                {
                    Vector3 dir = toSource / dist;
                    if (geom.TryClosestHit(listener, dir, out float tHit, out _, out _))
                    {
                        if (tHit >= dist * 0.98f) losClear = true;
                    }
                    else losClear = true;
                }
                var free = tracer.ComputeFreeSurfacePerceived(listener, source);
                Vector3 perceivedDir = free.ApparentDirection;
                float intensity = free.Intensity;
                float pathLen = free.Delay > 0f ? free.Delay * SpeedOfSound : dist;
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
            if (_geometry == null)
                _geometry = new AcousticGeometry(_renderContext);
            if (_tracer == null && !HasSharedProvider)
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