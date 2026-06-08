// Folder: ToolChest
// File: TransformGizmoOverlay.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ToolChest
{
    public class TransformGizmoOverlay : ICustomOverlay
    {
        private readonly IRenderContext _renderContext;
        private readonly EventBus _eventBus;

        private int _selectedEntityId = -1;
        private Vector3 _gizmoPosition = Vector3.Zero;
        private Quaternion _gizmoRotation = Quaternion.Identity;

        // Gizmo geometry buffers
        private VertexBuffer _arrowBuffer;
        private VertexBuffer _ringBuffer;

        // Interaction state
        private bool _isDragging = false;
        private int _activeAxis = -1; // 0=X trans, 1=Y trans, 2=Z trans, 3=RX, 4=RY, 5=RZ
        private bool _isRotation = false;
        private Vector3 _dragStartWorld;
        private Vector3 _dragStartMouseRay;

        // Hover highlight
        private int _hoveredAxis = -1;

        // Delegates provided by editor (avoids any reference to CastleBuilder types)
        private readonly Func<Vector2, float, float, (Vector3 origin, Vector3 dir, bool success)> _getMouseRay;
        private readonly Func<int, Entity> _getEntityById;

        public TransformGizmoOverlay(IRenderContext renderContext, EventBus eventBus,
            Func<Vector2, float, float, (Vector3 origin, Vector3 dir, bool success)> getMouseRay,
            Func<int, Entity> getEntityById)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _getMouseRay = getMouseRay ?? throw new ArgumentNullException(nameof(getMouseRay));
            _getEntityById = getEntityById ?? throw new ArgumentNullException(nameof(getEntityById));
        }

        public void OnEntitySelected(int entityId, Vector3 position, Quaternion rotation)
        {
            _selectedEntityId = entityId;
            _gizmoPosition = position;
            _gizmoRotation = rotation;
            _isDragging = false;
            _activeAxis = -1;
            _hoveredAxis = -1;
            RebuildGizmoGeometry();
        }

        public void ClearSelection()
        {
            _selectedEntityId = -1;
            _isDragging = false;
            _activeAxis = -1;
            _hoveredAxis = -1;
        }

        private unsafe void RebuildGizmoGeometry()
        {
            if (_arrowBuffer == null)
                _arrowBuffer = new VertexBuffer(_renderContext);
            if (_ringBuffer == null)
                _ringBuffer = new VertexBuffer(_renderContext);

            BuildArrowGeometry();
            BuildRingGeometry();
        }

        private unsafe void BuildArrowGeometry()
        {
            var vertices = new List<Vertex>();
            var indices = new List<uint>();

            // X axis (Red)
            AddArrow(vertices, indices, Vector3.UnitX, new Vector4(1f, 0.2f, 0.2f, 1f));
            // Y axis (Green)
            AddArrow(vertices, indices, Vector3.UnitY, new Vector4(0.2f, 1f, 0.2f, 1f));
            // Z axis (Blue)
            AddArrow(vertices, indices, Vector3.UnitZ, new Vector4(0.2f, 0.2f, 1f, 1f));

            _arrowBuffer.UpdateCustom(vertices, indices);
        }

        private void AddArrow(List<Vertex> vertices, List<uint> indices, Vector3 direction, Vector4 color)
        {
            uint baseIndex = (uint)vertices.Count;
            float scale = 0.5f; // half the width/size as requested

            // Shaft
            vertices.Add(new Vertex(0, 0, 0, color.X, color.Y, color.Z, color.W));
            vertices.Add(new Vertex(direction.X * 1.5f * scale, direction.Y * 1.5f * scale, direction.Z * 1.5f * scale, color.X, color.Y, color.Z, color.W));

            // Arrowhead
            float headLength = 0.4f * scale;
            float headRadius = 0.15f * scale;
            int segments = 12;
            uint shaftEnd = baseIndex + 1;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * MathF.PI * 2f / segments;
                float x = MathF.Cos(angle) * headRadius;
                float y = MathF.Sin(angle) * headRadius;

                Vector3 offset;
                if (direction == Vector3.UnitX)
                    offset = new Vector3(1.5f * scale - headLength, x, y);
                else if (direction == Vector3.UnitY)
                    offset = new Vector3(x, 1.5f * scale - headLength, y);
                else
                    offset = new Vector3(x, y, 1.5f * scale - headLength);

                vertices.Add(new Vertex(offset.X, offset.Y, offset.Z, color.X, color.Y, color.Z, color.W));
            }

            // Tip
            Vector3 tip = direction * (1.5f * scale + headLength * 0.3f);
            vertices.Add(new Vertex(tip.X, tip.Y, tip.Z, color.X, color.Y, color.Z, color.W));

            // Shaft indices
            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);

            // Cone indices
            uint tipIndex = (uint)vertices.Count - 1;
            for (int i = 0; i < segments; i++)
            {
                uint current = shaftEnd + (uint)i;
                uint next = shaftEnd + (uint)((i + 1) % segments);
                indices.Add(shaftEnd - 1);
                indices.Add(current);
                indices.Add(next);
                indices.Add(current);
                indices.Add(tipIndex);
                indices.Add(next);
            }
        }

        private unsafe void BuildRingGeometry()
        {
            var vertices = new List<Vertex>();
            var indices = new List<uint>();

            // XY ring (Blue - rotation around Z)
            AddRing(vertices, indices, new Vector3(0, 0, 1), new Vector4(0.2f, 0.2f, 1f, 1f));
            // XZ ring (Green - rotation around Y)
            AddRing(vertices, indices, new Vector3(0, 1, 0), new Vector4(0.2f, 1f, 0.2f, 1f));
            // YZ ring (Red - rotation around X)
            AddRing(vertices, indices, new Vector3(1, 0, 0), new Vector4(1f, 0.2f, 0.2f, 1f));

            _ringBuffer.UpdateCustom(vertices, indices);
        }

        private void AddRing(List<Vertex> vertices, List<uint> indices, Vector3 axis, Vector4 color)
        {
            uint baseIndex = (uint)vertices.Count;
            int segments = 48;
            float radius = 1.8f * 0.5f; // half the width/size

            for (int i = 0; i < segments; i++)
            {
                float angle = i * MathF.PI * 2f / segments;
                float x = MathF.Cos(angle) * radius;
                float y = MathF.Sin(angle) * radius;

                Vector3 point;
                if (axis == Vector3.UnitX)
                    point = new Vector3(0, x, y);
                else if (axis == Vector3.UnitY)
                    point = new Vector3(x, 0, y);
                else
                    point = new Vector3(x, y, 0);

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

        public void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight)
        {
            // ICustomOverlay.Draw is 2D UI path - gizmo is rendered via RenderWorld from SceneEditorPanel.RenderInnerContent
        }

        public unsafe void RenderWorld(Matrix4x4 view, Matrix4x4 projection)
        {
            if (_selectedEntityId == -1 || _arrowBuffer == null || _ringBuffer == null) return;

            Matrix4x4 model = Matrix4x4.CreateFromQuaternion(_gizmoRotation) * Matrix4x4.CreateTranslation(_gizmoPosition);

            // Render on top of everything (ghost through entities)
            _renderContext.Disable(_renderContext.Enums.DepthTest);

            // Arrows (translation)
            _arrowBuffer.Bind();
            var arrowShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            arrowShader.Use();
            arrowShader.SetMatrix4("uModel", model);
            arrowShader.SetMatrix4("uView", view);
            arrowShader.SetMatrix4("uProjection", projection);
            arrowShader.SetUniform("uPointSize", 8f);
            _renderContext.DrawElements(_renderContext.Enums.Lines, _arrowBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

            // Rings (rotation)
            _ringBuffer.Bind();
            arrowShader.SetMatrix4("uModel", model);
            _renderContext.DrawElements(_renderContext.Enums.Lines, _ringBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

            arrowShader.Dispose();

            // Re-enable depth test for the rest of the scene
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public bool HandleMouseInput(Vector2 contentMouse, float contentW, float contentH)
        {
            if (_selectedEntityId == -1) return false;

            var rayResult = _getMouseRay(contentMouse, contentW, contentH);
            if (!rayResult.success) return false;

            Vector3 rayOrigin = rayResult.origin;
            Vector3 rayDir = rayResult.dir;

            // TODO: full hit detection + drag logic (phase 1 complete with geometry and render)
            // For now return true to indicate input was handled
            return true;
        }
    }
}