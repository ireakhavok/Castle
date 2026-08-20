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
        private VertexBuffer _dynamicBuffer;
        private ShaderProgram _shader;
        private readonly List<Vertex> _dynVerts = new List<Vertex>(8192);
        private readonly List<uint> _dynIndices = new List<uint>(16384);
        private AcousticGeometry _geometry;
        private AcousticRayTracer _tracer;
        private bool _geometryDirty = true;
        private int _lastEntityCount = -1;
        private bool _wasEnabled;
        private bool _loggedThisEnable;
        public bool Enabled { get; set; } = false;
        public bool ShowListenerRays { get; set; } = true;
        public bool ShowSourceRays { get; set; } = true;
        public bool ShowMeetings { get; set; } = true;
        private static readonly Vector4 ListenerFreeColor = new Vector4(0.20f, 0.55f, 1.00f, 0.55f);
        private static readonly Vector4 SourceFreeColor = new Vector4(1.00f, 0.25f, 0.20f, 0.55f);
        private static readonly Vector4 BounceColor = new Vector4(0.20f, 0.90f, 0.40f, 0.90f);
        private static readonly Vector4 DiffractedColor = new Vector4(0.10f, 0.95f, 1.00f, 0.70f);

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
                _loggedThisEnable = false;
                return;
            }
            if (!_wasEnabled)
            {
                _geometryDirty = true;
                _wasEnabled = true;
                _loggedThisEnable = false;
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
            }
            Vector3 listener = _getListenerPos();
            var sources = _getSourcePositions() ?? Array.Empty<Vector3>();
            _dynVerts.Clear();
            _dynIndices.Clear();
            AddCross(_dynVerts, _dynIndices, listener, 1.5f, ListenerFreeColor);
            for (int s = 0; s < sources.Count; s++)
                AddCross(_dynVerts, _dynIndices, sources[s], 1.2f, SourceFreeColor);

            if (_geometry.TriangleCount > 0)
            {
                bool doLog = !_loggedThisEnable;
                _tracer.KickDebugBidirectional(listener, sources, doLog);
                var segments = _tracer.GetDebugSegments();
                if (doLog)
                {
                    Console.WriteLine($"[AcousticDebug] Overlay received {segments.Count} segments");
                    _loggedThisEnable = true;
                }
                if (segments != null)
                {
                    // Continuous surface coloring: group by triangle and emit filled tris
                    var drawnTris = new HashSet<int>();
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var seg = segments[i];
                        if (seg.TriangleIndex >= 0 && !drawnTris.Contains(seg.TriangleIndex))
                        {
                            drawnTris.Add(seg.TriangleIndex);
                            if (!_geometry.GetTriangle(seg.TriangleIndex, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                            Vector4 col = seg.Kind switch
                            {
                                AcousticRayTracer.DebugSegmentKind.Diffracted => DiffractedColor,
                                AcousticRayTracer.DebugSegmentKind.FreeLeg => ListenerFreeColor,
                                AcousticRayTracer.DebugSegmentKind.SourceFree => SourceFreeColor,
                                _ => BounceColor
                            };
                            if ((seg.Kind == AcousticRayTracer.DebugSegmentKind.FreeLeg && !ShowListenerRays) ||
                                (seg.Kind == AcousticRayTracer.DebugSegmentKind.SourceFree && !ShowSourceRays) ||
                                (seg.Kind == AcousticRayTracer.DebugSegmentKind.Diffracted && !ShowMeetings))
                                continue;
                            AddFilledTriangle(_dynVerts, _dynIndices, a, b, c, col);
                        }
                        else if (seg.TriangleIndex < 0)
                        {
                            // Direct LOS line
                            if (seg.Kind == AcousticRayTracer.DebugSegmentKind.Diffracted)
                                AddLine(_dynVerts, _dynIndices, seg.A, seg.B, DiffractedColor);
                        }
                    }
                }
            }
            if (_dynVerts.Count == 0) return;
            if (_dynamicBuffer == null)
                _dynamicBuffer = new VertexBuffer(_renderContext);
            _dynamicBuffer.UpdateCustom(_dynVerts, _dynIndices);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            _shader.Use();
            _shader.SetMatrix4("uView", view);
            _shader.SetMatrix4("uProjection", projection);
            _shader.SetMatrix4("uModel", Matrix4x4.Identity);
            _shader.SetUniform("uPointSize", 4f);
            _dynamicBuffer.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Triangles, _dynamicBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
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