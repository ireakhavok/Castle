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
        private readonly List<Vertex> _dynVerts = new List<Vertex>(2048);
        private readonly List<uint> _dynIndices = new List<uint>(4096);

        private AcousticGeometry _geometry;
        private AcousticRayTracer _tracer;
        private bool _geometryDirty = true;
        private int _lastEntityCount = -1;

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
            if (!Enabled) return;

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
                _geometry.Rebuild(entities, null);
                _geometryDirty = false;
            }

            Vector3 listener = _getListenerPos();
            var sources = _getSourcePositions();

            _tracer.KickDebugBidirectional(listener, sources);
            var segments = _tracer.GetDebugSegments();

            _dynVerts.Clear();
            _dynIndices.Clear();

            if (segments != null)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    var seg = segments[i];
                    Vector4 col = seg.Kind switch
                    {
                        AcousticRayTracer.DebugSegmentKind.Listener => ListenerColor,
                        AcousticRayTracer.DebugSegmentKind.Source => SourceColor,
                        _ => MeetingColor
                    };
                    if ((seg.Kind == AcousticRayTracer.DebugSegmentKind.Listener && !ShowListenerRays) ||
                        (seg.Kind == AcousticRayTracer.DebugSegmentKind.Source && !ShowSourceRays) ||
                        (seg.Kind == AcousticRayTracer.DebugSegmentKind.Meeting && !ShowMeetings))
                        continue;
                    AddLine(_dynVerts, _dynIndices, seg.A, seg.B, col);
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
    }
}