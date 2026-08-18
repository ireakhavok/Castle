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

        private VertexBuffer _dynamicBuffer;
        private ShaderProgram _shader;
        private readonly List<Vertex> _dynVerts = new List<Vertex>(4096);
        private readonly List<uint> _dynIndices = new List<uint>(8192);

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

        private static readonly Vector4 ListenerColor = new Vector4(0.20f, 0.55f, 1.00f, 1.0f);
        private static readonly Vector4 SourceColor = new Vector4(1.00f, 0.25f, 0.20f, 1.0f);
        private static readonly Vector4 MeetingColor = new Vector4(0.85f, 0.20f, 1.00f, 1.0f);

        public AcousticDebugOverlay(
            IRenderContext renderContext,
            Func<IReadOnlyList<Entity>> getEntities,
            Func<Vector3> getListenerPos,
            Func<IReadOnlyList<Vector3>> getSourcePositions)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _getEntities = getEntities ?? throw new ArgumentNullException(nameof(getEntities));
            _getListenerPos = getListenerPos ?? (() => Vector3.Zero);
            _getSourcePositions = getSourcePositions ?? (() => Array.Empty<Vector3>());
        }

        public void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight) { }

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
                try
                {
                    foreach (var e in entities)
                    {
                        var p = e.GetComponent<PhysicsComponent>();
                        if (p?.Shape is HeightfieldShape) { }
                    }
                }
                catch { }
                _geometry.Rebuild(entities, height);
                _geometryDirty = false;
            }

            Vector3 listener = _getListenerPos();
            var sources = _getSourcePositions() ?? Array.Empty<Vector3>();

            // Always draw visible markers so the toggle is never silent
            _dynVerts.Clear();
            _dynIndices.Clear();
            AddCross(_dynVerts, _dynIndices, listener, 1.5f, ListenerColor);
            for (int s = 0; s < sources.Count; s++)
                AddCross(_dynVerts, _dynIndices, sources[s], 1.2f, SourceColor);

            // GPU bidirectional free-segment + meeting results
            if (_geometry.TriangleCount > 0)
            {
                _tracer.KickDebugBidirectional(listener, sources, !_loggedThisEnable);
                var segments = _tracer.GetDebugSegments();

                if (!_loggedThisEnable)
                {
                    Console.WriteLine($"[AcousticDebug] Listener resolved = ({listener.X:F3},{listener.Y:F3},{listener.Z:F3})  (Camera-first then PreferredSpawnPointIds → Physics.Position; zero = unresolved)");
                    if (sources.Count == 0)
                        Console.WriteLine("[AcousticDebug] Sources: none");
                    else
                    {
                        for (int s = 0; s < sources.Count; s++)
                            Console.WriteLine($"[AcousticDebug] Source[{s}] = ({sources[s].X:F3},{sources[s].Y:F3},{sources[s].Z:F3})");
                    }
                    Console.WriteLine($"[AcousticDebug] Geometry after Rebuild: TriangleCount={_geometry.TriangleCount} GeometryVersion={_geometry.GeometryVersion}");

                    int count = segments != null ? segments.Count : 0;
                    Console.WriteLine($"[AcousticDebug] GetDebugSegments returned {count} segments");

                    int listenerCnt = 0, sourceCnt = 0, meetingCnt = 0;
                    if (segments != null)
                    {
                        for (int i = 0; i < segments.Count; i++)
                        {
                            if (segments[i].Kind == AcousticRayTracer.DebugSegmentKind.Listener) listenerCnt++;
                            else if (segments[i].Kind == AcousticRayTracer.DebugSegmentKind.Source) sourceCnt++;
                            else meetingCnt++;
                        }
                        Console.WriteLine($"[AcousticDebug] Kind histogram: Listener={listenerCnt} Source={sourceCnt} Meeting={meetingCnt}");

                        int sample = Math.Min(12, segments.Count);
                        for (int i = 0; i < sample; i++)
                        {
                            var seg = segments[i];
                            Console.WriteLine($"[AcousticDebug]   seg[{i}] Kind={seg.Kind} A=({seg.A.X:F2},{seg.A.Y:F2},{seg.A.Z:F2}) B=({seg.B.X:F2},{seg.B.Y:F2},{seg.B.Z:F2})");
                        }
                    }
                    _loggedThisEnable = true;
                }

                // Force-draw every accepted segment on the diagnostic frame and all subsequent frames
                // (ignore Show* toggles and length cull so the full set of rays is visible)
                if (segments != null)
                {
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var seg = segments[i];
                        Vector4 col;
                        if (seg.Kind == AcousticRayTracer.DebugSegmentKind.Listener)
                            col = ListenerColor;
                        else if (seg.Kind == AcousticRayTracer.DebugSegmentKind.Source)
                            col = SourceColor;
                        else
                            col = MeetingColor;
                        AddLine(_dynVerts, _dynIndices, seg.A, seg.B, col);
                    }
                }
            }
            else
            {
                if (!_loggedThisEnable)
                {
                    Console.WriteLine("[AcousticDebug] TriangleCount==0 → synthetic free-space fallback path");
                    _loggedThisEnable = true;
                }
                const int fallbackRays = 32;
                for (int i = 0; i < fallbackRays; i++)
                {
                    float a = i * MathF.PI * 2f / fallbackRays;
                    float elev = ((i % 4) - 1.5f) * 0.35f;
                    Vector3 dir = Vector3.Normalize(new Vector3(MathF.Cos(a), MathF.Sin(a), elev));
                    Vector3 end = listener + dir * 25f;
                    AddLine(_dynVerts, _dynIndices, listener, end, ListenerColor);
                }
                for (int s = 0; s < sources.Count; s++)
                {
                    Vector3 src = sources[s];
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * MathF.PI * 2f / 12f;
                        Vector3 dir = Vector3.Normalize(new Vector3(MathF.Cos(a), MathF.Sin(a), 0.1f));
                        Vector3 end = src + dir * 18f;
                        AddLine(_dynVerts, _dynIndices, src, end, SourceColor);
                    }
                    Vector3 mid = (listener + src) * 0.5f;
                    AddCross(_dynVerts, _dynIndices, mid, 0.8f, MeetingColor);
                }
            }

            if (_dynVerts.Count == 0) return;

            if (_dynamicBuffer == null)
                _dynamicBuffer = new VertexBuffer(_renderContext);
            _dynamicBuffer.UpdateCustom(_dynVerts, _dynIndices);

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _shader.Use();
            _shader.SetMatrix4("uView", view);
            _shader.SetMatrix4("uProjection", projection);
            _shader.SetMatrix4("uModel", Matrix4x4.Identity);
            _shader.SetUniform("uPointSize", 4f);
            _dynamicBuffer.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Lines, _dynamicBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
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

        private static void AddCross(List<Vertex> verts, List<uint> indices, Vector3 center, float size, Vector4 color)
        {
            AddLine(verts, indices, center - new Vector3(size, 0, 0), center + new Vector3(size, 0, 0), color);
            AddLine(verts, indices, center - new Vector3(0, size, 0), center + new Vector3(0, size, 0), color);
            AddLine(verts, indices, center - new Vector3(0, 0, size), center + new Vector3(0, 0, size), color);
        }
    }
}