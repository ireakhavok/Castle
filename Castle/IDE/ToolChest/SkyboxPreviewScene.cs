// Folder: ToolChest
// File: SkyboxPreviewScene.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace ToolChest
{
    /// <summary>
    /// IDE-only scene owned by SkyboxRotatePanel.
    /// Matches ModelViewerScene pure-content ownership.
    /// Depth clear is performed only when the panel size actually changes
    /// so continuous docked resize does not pay a scissored Clear every frame.
    /// Lines go through the shared LineRenderer.
    /// </summary>
    public unsafe class SkyboxPreviewScene : Scene
    {
        private uint _cubemapTex;
        private VertexBuffer _previewCube;
        private VertexBuffer _axisBuffer;
        private VertexBuffer _ringBuffer;
        private VertexBuffer _faceOutlineBuffer;
        private ShaderProgram _previewShader;
        private LineRenderer _lineRenderer;
        public float PreviewYaw = 0.6f;
        public float PreviewPitch = 0.35f;
        public float PreviewDist = 2.8f;
        private Quaternion _orientation = Quaternion.Identity;
        private int _selectedFace = -1;
        private int _lastClearedW = -1;
        private int _lastClearedH = -1;
        public SkyboxPreviewScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
        }
        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _lineRenderer = new LineRenderer(_renderContext);
            _lineRenderer.Initialize();
            _previewCube = new VertexBuffer(_renderContext);
            BuildPreviewCube();
            _axisBuffer = new VertexBuffer(_renderContext);
            _ringBuffer = new VertexBuffer(_renderContext);
            _faceOutlineBuffer = new VertexBuffer(_renderContext);
            RebuildAxesAndRings();
            BuildFaceOutline(-1);
            string vs = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
uniform mat4 uMVP;
out vec3 vDir;
void main() {
    vDir = aPosition;
    gl_Position = uMVP * vec4(aPosition, 1.0);
}";
            string fs = @"
#version 330 core
in vec3 vDir;
uniform samplerCube uSkybox;
out vec4 FragColor;
void main() {
    FragColor = texture(uSkybox, normalize(vDir));
}";
            _previewShader = new ShaderProgram(_renderContext, vs, fs);
            _lastClearedW = width;
            _lastClearedH = height;
        }
        public override void Resize(int width, int height)
        {
            _width = width;
            _height = height;
            _aspectRatio = width > 0 && height > 0 ? (float)width / height : 16f / 9f;
            // No Viewport – LayeredUIRenderer owns the panel absolute viewport + scissor.
        }
        public void SetOrientation(Quaternion orient)
        {
            _orientation = orient;
        }
        public void SetSelectedFace(int face)
        {
            if (_selectedFace == face) return;
            _selectedFace = face;
            BuildFaceOutline(face);
        }
        public void SetCubemapTexture(uint tex)
        {
            if (_cubemapTex != 0 && _cubemapTex != tex)
            {
                _renderContext.DeleteTexture(_cubemapTex);
            }
            _cubemapTex = tex;
        }
        public uint CubemapTexture => _cubemapTex;
        public override void Render(IReadOnlyList<Entity> entities)
        {
            if (_cubemapTex == 0 || _previewCube == null || _previewShader == null)
                return;
            float aspect = AspectRatio;
            if (aspect <= 0f) aspect = 1f;
            Vector3 camPos = new Vector3(
                MathF.Sin(PreviewYaw) * MathF.Cos(PreviewPitch) * PreviewDist,
               -MathF.Cos(PreviewYaw) * MathF.Cos(PreviewPitch) * PreviewDist,
                MathF.Sin(PreviewPitch) * PreviewDist
            );
            Matrix4x4 view = Matrix4x4.CreateLookAt(camPos, Vector3.Zero, Vector3.UnitZ);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, aspect, 0.1f, 50f);
            Matrix4x4 orient = Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(_orientation));
            Matrix4x4 mvp = orient * view * proj;
            // Minimal state required for a closed cube under the depth buffer left dirty by LayeredUIRenderer.
            // Clear is performed only when the panel size has actually changed so continuous docked resize
            // does not pay a scissored Clear every frame (matches the pure-content cost model of ModelViewerScene).
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            if (_width != _lastClearedW || _height != _lastClearedH)
            {
                _renderContext.Clear(_renderContext.Enums.DepthBufferBit);
                _lastClearedW = _width;
                _lastClearedH = _height;
            }
            _previewShader.Use();
            _previewShader.SetMatrix4("uMVP", mvp);
            _renderContext.ActiveTexture(0);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTex);
            _previewCube.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Triangles, _previewCube.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            // Lines through the shared LineRenderer (owns its own Depth/LineWidth state)
            Matrix4x4 lineModel = orient;
            if (_axisBuffer != null)
                _lineRenderer.DrawLines(_axisBuffer, lineModel, view, proj, 1f);
            if (_ringBuffer != null)
                _lineRenderer.DrawLines(_ringBuffer, lineModel, view, proj, 2.5f);
            if (_selectedFace >= 0 && _faceOutlineBuffer != null && _faceOutlineBuffer.GetIndexCount() > 0)
                _lineRenderer.DrawLines(_faceOutlineBuffer, lineModel, view, proj, 1f);
        }
        private void BuildPreviewCube()
        {
            float s = 0.7f;
            var vertices = new List<float>();
            var indices = new List<uint>();
            vertices.AddRange(new float[] { -s, -s, -s, 1, 1, 1, 1, 0, 0 });
            vertices.AddRange(new float[] { s, -s, -s, 1, 1, 1, 1, 1, 0 });
            vertices.AddRange(new float[] { s, s, -s, 1, 1, 1, 1, 1, 1 });
            vertices.AddRange(new float[] { -s, s, -s, 1, 1, 1, 1, 0, 1 });
            vertices.AddRange(new float[] { -s, -s, s, 1, 1, 1, 1, 0, 0 });
            vertices.AddRange(new float[] { s, -s, s, 1, 1, 1, 1, 1, 0 });
            vertices.AddRange(new float[] { s, s, s, 1, 1, 1, 1, 1, 1 });
            vertices.AddRange(new float[] { -s, s, s, 1, 1, 1, 1, 0, 1 });
            indices.AddRange(new uint[] { 0, 1, 2, 2, 3, 0 });
            indices.AddRange(new uint[] { 4, 5, 6, 6, 7, 4 });
            indices.AddRange(new uint[] { 0, 4, 7, 7, 3, 0 });
            indices.AddRange(new uint[] { 1, 5, 6, 6, 2, 1 });
            indices.AddRange(new uint[] { 3, 2, 6, 6, 7, 3 });
            indices.AddRange(new uint[] { 0, 1, 5, 5, 4, 0 });
            _previewCube.UpdateCustomWithUV(vertices, indices);
        }
        private void RebuildAxesAndRings()
        {
            var aVerts = new List<Vertex>();
            var aIdx = new List<uint>();
            float len = 1.1f;
            aVerts.Add(new Vertex(0, 0, 0, 1, 0.15f, 0.15f, 1));
            aVerts.Add(new Vertex(len, 0, 0, 1, 0.15f, 0.15f, 1));
            aIdx.Add(0); aIdx.Add(1);
            aVerts.Add(new Vertex(0, 0, 0, 0.15f, 1, 0.15f, 1));
            aVerts.Add(new Vertex(0, len, 0, 0.15f, 1, 0.15f, 1));
            aIdx.Add(2); aIdx.Add(3);
            aVerts.Add(new Vertex(0, 0, 0, 0.2f, 0.4f, 1, 1));
            aVerts.Add(new Vertex(0, 0, len, 0.2f, 0.4f, 1, 1));
            aIdx.Add(4); aIdx.Add(5);
            _axisBuffer.UpdateCustom(aVerts, aIdx);
            var rVerts = new List<Vertex>();
            var rIdx = new List<uint>();
            AddRing(rVerts, rIdx, Vector3.UnitX, new Vector4(1f, 0.2f, 0.2f, 1f));
            AddRing(rVerts, rIdx, Vector3.UnitY, new Vector4(0.2f, 1f, 0.2f, 1f));
            AddRing(rVerts, rIdx, Vector3.UnitZ, new Vector4(0.2f, 0.4f, 1f, 1f));
            _ringBuffer.UpdateCustom(rVerts, rIdx);
        }
        private void AddRing(List<Vertex> vertices, List<uint> indices, Vector3 axis, Vector4 color)
        {
            uint baseIndex = (uint)vertices.Count;
            int segments = 48;
            float radius = 1.05f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * MathF.PI * 2f / segments;
                float x = MathF.Cos(angle) * radius;
                float y = MathF.Sin(angle) * radius;
                Vector3 point;
                if (axis == Vector3.UnitX) point = new Vector3(0, x, y);
                else if (axis == Vector3.UnitY) point = new Vector3(x, 0, y);
                else point = new Vector3(x, y, 0);
                vertices.Add(new Vertex(point.X, point.Y, point.Z, color.X, color.Y, color.Z, color.W));
            }
            for (int i = 0; i < segments; i++)
            {
                uint current = baseIndex + (uint)i;
                uint next = baseIndex + (uint)((i + 1) % segments);
                indices.Add(current);
                indices.Add(next);
            }
        }
        private void BuildFaceOutline(int face)
        {
            float s = 0.72f;
            var verts = new List<Vertex>();
            var idx = new List<uint>();
            Vector4 c = new Vector4(1f, 0.85f, 0.1f, 1f);
            Vector3[] corners = face switch
            {
                0 => new[] { new Vector3(s, -s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(s, -s, s) },
                1 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(-s, s, s), new Vector3(-s, s, -s) },
                2 => new[] { new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(-s, s, s) },
                3 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, -s, -s) },
                4 => new[] { new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(-s, s, s) },
                5 => new[] { new Vector3(-s, -s, -s), new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, -s, -s) },
                _ => Array.Empty<Vector3>()
            };
            if (corners.Length == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    verts.Add(new Vertex(corners[i].X, corners[i].Y, corners[i].Z, c.X, c.Y, c.Z, c.W));
                    idx.Add((uint)i);
                    idx.Add((uint)((i + 1) % 4));
                }
            }
            _faceOutlineBuffer.UpdateCustom(verts, idx);
        }
        public (Vector3 origin, Vector3 dir, bool ok) GetPreviewRay(Vector2 relMouse, float contentW, float contentH, float header)
        {
            float aspect = contentW / Math.Max(contentH, 1f);
            Vector3 camPos = new Vector3(
                MathF.Sin(PreviewYaw) * MathF.Cos(PreviewPitch) * PreviewDist,
               -MathF.Cos(PreviewYaw) * MathF.Cos(PreviewPitch) * PreviewDist,
                MathF.Sin(PreviewPitch) * PreviewDist
            );
            Matrix4x4 view = Matrix4x4.CreateLookAt(camPos, Vector3.Zero, Vector3.UnitZ);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, aspect, 0.1f, 50f);
            if (!Matrix4x4.Invert(proj, out Matrix4x4 invProj)) return (Vector3.Zero, Vector3.Zero, false);
            if (!Matrix4x4.Invert(view, out Matrix4x4 invView)) return (Vector3.Zero, Vector3.Zero, false);
            float ndcX = (relMouse.X / contentW) * 2f - 1f;
            float ndcY = 1f - ((relMouse.Y - header) / contentH) * 2f;
            Vector4 nearH = Vector4.Transform(new Vector4(ndcX, ndcY, -1f, 1f), invProj);
            Vector4 farH = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), invProj);
            Vector3 near = new Vector3(nearH.X / nearH.W, nearH.Y / nearH.W, nearH.Z / nearH.W);
            Vector3 far = new Vector3(farH.X / farH.W, farH.Y / farH.W, farH.Z / farH.W);
            Vector3 origin = Vector3.Transform(near, invView);
            Vector3 dir = Vector3.Normalize(Vector3.Transform(far, invView) - origin);
            return (origin, dir, true);
        }
        public int PickRing(Vector2 relMouse, float contentW, float contentH, float header, float tolerance)
        {
            Matrix4x4 orient = Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(_orientation));
            float best = float.MaxValue;
            int bestRing = -1;
            for (int ring = 0; ring < 3; ring++)
            {
                int segs = 48;
                float radius = 1.05f;
                Vector3 prev = Vector3.Zero;
                for (int i = 0; i <= segs; i++)
                {
                    float ang = i * MathF.PI * 2f / segs;
                    float x = MathF.Cos(ang) * radius;
                    float y = MathF.Sin(ang) * radius;
                    Vector3 local;
                    if (ring == 0) local = new Vector3(0, x, y);
                    else if (ring == 1) local = new Vector3(x, 0, y);
                    else local = new Vector3(x, y, 0);
                    Vector3 world = Vector3.Transform(local, orient);
                    if (i > 0)
                    {
                        float d = DistanceToSegment2D(relMouse, prev, world, contentW, contentH, header);
                        if (d < best) { best = d; bestRing = ring; }
                    }
                    prev = world;
                }
            }
            return best < tolerance ? bestRing : -1;
        }
        private float DistanceToSegment2D(Vector2 p, Vector3 a3, Vector3 b3, float vw, float vh, float header)
        {
            Vector2 a = WorldToScreen(a3, vw, vh, header);
            Vector2 b = WorldToScreen(b3, vw, vh, header);
            Vector2 ab = b - a;
            float len2 = ab.LengthSquared();
            if (len2 < 1e-8f) return Vector2.Distance(p, a);
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }
        private Vector2 WorldToScreen(Vector3 world, float vw, float vh, float header)
        {
            Vector3 camPos = new Vector3(
                MathF.Sin(PreviewYaw) * MathF.Cos(PreviewPitch) * PreviewDist,
               -MathF.Cos(PreviewYaw) * MathF.Cos(PreviewPitch) * PreviewDist,
                MathF.Sin(PreviewPitch) * PreviewDist
            );
            Matrix4x4 view = Matrix4x4.CreateLookAt(camPos, Vector3.Zero, Vector3.UnitZ);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, vw / Math.Max(vh, 1f), 0.1f, 50f);
            Vector4 clip = Vector4.Transform(new Vector4(world, 1f), view * proj);
            if (Math.Abs(clip.W) < 1e-6f) return new Vector2(vw * 0.5f, vh * 0.5f + header);
            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            return new Vector2((ndcX * 0.5f + 0.5f) * vw, (1f - (ndcY * 0.5f + 0.5f)) * vh + header);
        }
        public override void Dispose()
        {
            if (_cubemapTex != 0)
            {
                _renderContext.DeleteTexture(_cubemapTex);
                _cubemapTex = 0;
            }
            _previewCube?.Dispose();
            _axisBuffer?.Dispose();
            _ringBuffer?.Dispose();
            _faceOutlineBuffer?.Dispose();
            _previewShader?.Dispose();
            _lineRenderer?.Dispose();
            base.Dispose();
        }
    }
}