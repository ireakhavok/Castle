// Folder: IDE
// File: TransformGizmoOverlay.cs
using Keystone;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
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
        private VertexBuffer _arrowBuffer;
        private VertexBuffer _ringBuffer;
        private bool _isDragging = false;
        private int _activeAxis = -1;
        private bool _isRotating = false;
        private Vector3 _dragAxisWorld;           // local for rotation, world for translation
        private Vector2 _lastDragMouse;
        private Vector3 _dragStartClosestPoint;
        private Vector3 _lastClosestPoint;
        private Vector3 _lastPlanePoint;          // for ring rotation
        private readonly Func<Vector2, float, float, (Vector3 origin, Vector3 dir, bool success)> _getMouseRay;
        private readonly Func<int, Entity> _getEntityById;
        private Matrix4x4 _lastView = Matrix4x4.Identity;
        private Matrix4x4 _lastProjection = Matrix4x4.Identity;

        private const float ArrowPickTolerance = 25f;
        private const float RingPickTolerance = 8f;

        public TransformGizmoOverlay(IRenderContext renderContext, EventBus eventBus,
            Func<Vector2, float, float, (Vector3 origin, Vector3 dir, bool success)> getMouseRay,
            Func<int, Entity> getEntityById)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _getMouseRay = getMouseRay ?? throw new ArgumentNullException(nameof(getMouseRay));
            _getEntityById = getEntityById ?? throw new ArgumentNullException(nameof(getEntityById));
        }

        public void UpdateMatrices(Matrix4x4 view, Matrix4x4 projection)
        {
            _lastView = view;
            _lastProjection = projection;
        }

        public void OnEntitySelected(int entityId, Vector3 position, Quaternion rotation)
        {
            _selectedEntityId = entityId;
            _isDragging = false;
            _isRotating = false;
            _activeAxis = -1;
            RebuildGizmoGeometry();
        }

        public void ClearSelection()
        {
            _selectedEntityId = -1;
            _isDragging = false;
            _isRotating = false;
            _activeAxis = -1;
        }

        public void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight) { }

        private unsafe void RebuildGizmoGeometry()
        {
            if (_arrowBuffer == null) _arrowBuffer = new VertexBuffer(_renderContext);
            if (_ringBuffer == null) _ringBuffer = new VertexBuffer(_renderContext);
            BuildArrowGeometry();
            BuildRingGeometry();
        }

        private unsafe void BuildArrowGeometry()
        {
            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            AddArrow(vertices, indices, Vector3.UnitX, new Vector4(1f, 0.2f, 0.2f, 1f));
            AddArrow(vertices, indices, Vector3.UnitY, new Vector4(0.2f, 1f, 0.2f, 1f));
            AddArrow(vertices, indices, Vector3.UnitZ, new Vector4(0.2f, 0.2f, 1f, 1f));
            _arrowBuffer.UpdateCustom(vertices, indices);
        }

        private void AddArrow(List<Vertex> vertices, List<uint> indices, Vector3 direction, Vector4 color)
        {
            uint baseIndex = (uint)vertices.Count;
            float scale = 0.5f;
            vertices.Add(new Vertex(0, 0, 0, color.X, color.Y, color.Z, color.W));
            vertices.Add(new Vertex(direction.X * 1.5f * scale, direction.Y * 1.5f * scale, direction.Z * 1.5f * scale, color.X, color.Y, color.Z, color.W));
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
                if (direction == Vector3.UnitX) offset = new Vector3(1.5f * scale - headLength, x, y);
                else if (direction == Vector3.UnitY) offset = new Vector3(x, 1.5f * scale - headLength, y);
                else offset = new Vector3(x, y, 1.5f * scale - headLength);
                vertices.Add(new Vertex(offset.X, offset.Y, offset.Z, color.X, color.Y, color.Z, color.W));
            }
            Vector3 tip = direction * (1.5f * scale + headLength * 0.3f);
            vertices.Add(new Vertex(tip.X, tip.Y, tip.Z, color.X, color.Y, color.Z, color.W));
            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);
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
            AddRing(vertices, indices, new Vector3(0, 0, 1), new Vector4(0.2f, 0.2f, 1f, 1f));
            AddRing(vertices, indices, new Vector3(0, 1, 0), new Vector4(0.2f, 1f, 0.2f, 1f));
            AddRing(vertices, indices, new Vector3(1, 0, 0), new Vector4(1f, 0.2f, 0.2f, 1f));
            _ringBuffer.UpdateCustom(vertices, indices);
        }

        private void AddRing(List<Vertex> vertices, List<uint> indices, Vector3 axis, Vector4 color)
        {
            uint baseIndex = (uint)vertices.Count;
            int segments = 48;
            float radius = 1.8f * 0.5f;
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

        public unsafe void RenderWorld(Matrix4x4 view, Matrix4x4 projection)
        {
            _lastView = view;
            _lastProjection = projection;
            if (_selectedEntityId == -1 || _arrowBuffer == null || _ringBuffer == null) return;
            var entity = _getEntityById(_selectedEntityId);
            if (entity == null) return;
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return;
            Matrix4x4 model = Matrix4x4.CreateFromQuaternion(physics.Rotation) * Matrix4x4.CreateTranslation(physics.Position);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _arrowBuffer.Bind();
            var arrowShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            arrowShader.Use();
            arrowShader.SetMatrix4("uModel", model);
            arrowShader.SetMatrix4("uView", view);
            arrowShader.SetMatrix4("uProjection", projection);
            arrowShader.SetUniform("uPointSize", 8f);
            _renderContext.DrawElements(_renderContext.Enums.Lines, _arrowBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            _ringBuffer.Bind();
            arrowShader.SetMatrix4("uModel", model);
            _renderContext.DrawElements(_renderContext.Enums.Lines, _ringBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            arrowShader.Dispose();
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }

        public bool HandleMouseInput(Vector2 contentMouse, float contentW, float contentH, bool mouseDown, bool mousePressed, bool mouseReleased)
        {
            if (_selectedEntityId == -1) return false;

            if (!_isDragging)
            {
                int arrowAxis = PickAxisScreenSpace(contentMouse, contentW, contentH);
                if (arrowAxis != -1)
                {
                    _activeAxis = arrowAxis;
                    _isRotating = false;
                }
                else
                {
                    int ringAxis = PickRingScreenSpace(contentMouse, contentW, contentH);
                    if (ringAxis != -1)
                    {
                        _activeAxis = ringAxis;
                        _isRotating = true;
                    }
                    else
                    {
                        _activeAxis = -1;
                        _isRotating = false;
                    }
                }
            }

            if (mousePressed && _activeAxis != -1)
            {
                StartDrag(contentMouse, contentW, contentH);
                return true;
            }
            if (_isDragging && mouseDown)
            {
                PerformDrag(contentMouse, contentW, contentH);
                return true;
            }
            if (_isDragging && mouseReleased)
            {
                EndDrag();
                return true;
            }
            return _activeAxis != -1 || _isDragging;
        }

        private void StartDrag(Vector2 contentMouse, float contentW, float contentH)
        {
            var entity = _getEntityById(_selectedEntityId);
            if (entity == null) return;
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return;

            if (_isRotating)
            {
                _dragAxisWorld = GetAxisVector(_activeAxis);   // local axis for rotation
            }
            else
            {
                _dragAxisWorld = GetAxisVector(_activeAxis);
                _dragAxisWorld = Vector3.Transform(_dragAxisWorld, Matrix4x4.CreateFromQuaternion(physics.Rotation));
                _dragAxisWorld = Vector3.Normalize(_dragAxisWorld);
            }

            if (!_isRotating)
            {
                var (rayOrigin, rayDir, success) = _getMouseRay(contentMouse, contentW, contentH);
                if (success)
                {
                    _dragStartClosestPoint = ClosestPointOnInfiniteAxis(rayOrigin, rayDir, physics.Position, _dragAxisWorld);
                    _lastClosestPoint = _dragStartClosestPoint;
                }
            }
            else
            {
                // Store initial plane intersection for consistent signed angle
                var (rayOrigin, rayDir, success) = _getMouseRay(contentMouse, contentW, contentH);
                if (success)
                {
                    _lastPlanePoint = ClosestPointOnRingPlane(rayOrigin, rayDir, physics.Position, _dragAxisWorld);
                }
            }

            _lastDragMouse = contentMouse;
            Console.WriteLine($"[TransformGizmoOverlay] Drag START - axis {_activeAxis} {(_isRotating ? "ROTATION" : "TRANSLATION")} axis={_dragAxisWorld}");
            _isDragging = true;
        }

        private void PerformDrag(Vector2 contentMouse, float contentW, float contentH)
        {
            var entity = _getEntityById(_selectedEntityId);
            if (entity == null) return;
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return;

            var (rayOrigin, rayDir, success) = _getMouseRay(contentMouse, contentW, contentH);
            if (!success) return;

            if (_isRotating)
            {
                Vector3 currentPlanePoint = ClosestPointOnRingPlane(rayOrigin, rayDir, physics.Position, _dragAxisWorld);

                // Signed angle between previous and current point on the plane
                Vector3 v1 = _lastPlanePoint - physics.Position;
                Vector3 v2 = currentPlanePoint - physics.Position;
                v1 = Vector3.Normalize(v1);
                v2 = Vector3.Normalize(v2);

                float dot = Vector3.Dot(v1, v2);
                dot = Math.Clamp(dot, -1f, 1f);
                float angleDelta = MathF.Acos(dot);
                Vector3 cross = Vector3.Cross(v1, v2);
                if (Vector3.Dot(cross, _dragAxisWorld) < 0) angleDelta = -angleDelta;

                Quaternion deltaQuat = Quaternion.CreateFromAxisAngle(_dragAxisWorld, angleDelta);
                physics.Rotation = Quaternion.Normalize(physics.Rotation * deltaQuat);

                if (float.IsNaN(physics.Rotation.X) || float.IsNaN(physics.Rotation.Y) ||
                    float.IsNaN(physics.Rotation.Z) || float.IsNaN(physics.Rotation.W))
                {
                    physics.Rotation = Quaternion.Identity;
                    Console.WriteLine("[TransformGizmoOverlay] *** NaN quaternion detected - reset to Identity ***");
                }

                Console.WriteLine($"[TransformGizmoOverlay] Rotation performed: {angleDelta * (180f / MathF.PI):F2} deg around local axis {_dragAxisWorld}");

                _lastPlanePoint = currentPlanePoint;
            }
            else
            {
                Vector3 newClosest = ClosestPointOnInfiniteAxis(rayOrigin, rayDir, physics.Position, _dragAxisWorld);
                Vector3 worldDelta = newClosest - _lastClosestPoint;
                physics.Position += worldDelta;
                _lastClosestPoint = newClosest;
                Console.WriteLine($"[TransformGizmoOverlay] PerformDrag - axis {_activeAxis} worldDelta={worldDelta} newPos={physics.Position}");
            }

            _lastDragMouse = contentMouse;

            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null)
            {
                var blueprintEntity = level.Entities.Find(e => e.Id == _selectedEntityId);
                if (blueprintEntity != null)
                {
                    var bpPhysics = blueprintEntity.GetComponent<PhysicsComponent>();
                    if (bpPhysics != null)
                    {
                        bpPhysics.Position = physics.Position;
                        bpPhysics.Rotation = physics.Rotation;
                    }
                }
            }

            _eventBus.Publish(new EntityMovedEvent(_selectedEntityId, new Vector2(physics.Position.X, physics.Position.Y), physics.Rotation));
        }

        private void EndDrag()
        {
            _isDragging = false;
            _isRotating = false;
            _activeAxis = -1;
            Console.WriteLine("[TransformGizmoOverlay] Drag END");
        }

        private Vector3 ClosestPointOnInfiniteAxis(Vector3 rayOrigin, Vector3 rayDir, Vector3 linePoint, Vector3 lineDir)
        {
            Vector3 w0 = rayOrigin - linePoint;
            float a = Vector3.Dot(rayDir, rayDir);
            float b = Vector3.Dot(rayDir, lineDir);
            float c = Vector3.Dot(lineDir, lineDir);
            float d = Vector3.Dot(rayDir, w0);
            float e = Vector3.Dot(lineDir, w0);
            float denom = a * c - b * b;
            if (Math.Abs(denom) < 1e-8f) return linePoint + Vector3.Dot(w0, lineDir) * lineDir;
            float tc = (a * e - b * d) / denom;
            return linePoint + tc * lineDir;
        }

        private Vector3 ClosestPointOnRingPlane(Vector3 rayOrigin, Vector3 rayDir, Vector3 center, Vector3 normal)
        {
            float denom = Vector3.Dot(rayDir, normal);
            if (Math.Abs(denom) < 1e-6f) return center; // parallel
            float t = Vector3.Dot(center - rayOrigin, normal) / denom;
            return rayOrigin + t * rayDir;
        }

        private int PickAxisScreenSpace(Vector2 contentMouse, float contentW, float contentH)
        {
            var entity = _getEntityById(_selectedEntityId);
            if (entity == null) return -1;
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return -1;

            float bestDist = float.MaxValue;
            int best = -1;
            Vector3 pos = physics.Position;
            Quaternion rot = physics.Rotation;
            for (int i = 0; i < 3; i++)
            {
                Vector3 dir = GetAxisVector(i);
                dir = Vector3.Transform(dir, Matrix4x4.CreateFromQuaternion(rot));
                float d = DistanceToLineSegment2D(contentMouse, pos, pos + dir * 1.5f, contentW, contentH);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            if (bestDist > ArrowPickTolerance) return -1;
            Console.WriteLine($"[TransformGizmoOverlay] *** HOVERED AXIS {best} (screen dist {bestDist:F3}) ***");
            return best;
        }

        private int PickRingScreenSpace(Vector2 contentMouse, float contentW, float contentH)
        {
            var entity = _getEntityById(_selectedEntityId);
            if (entity == null) return -1;
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return -1;

            Vector3 pos = physics.Position;
            Quaternion rot = physics.Rotation;
            float bestDist = float.MaxValue;
            int best = -1;

            for (int ring = 0; ring < 3; ring++)
            {
                Vector3 localAxis = GetAxisVector(ring);
                int segments = 48;
                float radius = 0.9f;
                Vector3 prevPoint = Vector3.Zero;

                for (int i = 0; i < segments; i++)
                {
                    float angle = i * MathF.PI * 2f / segments;
                    float x = MathF.Cos(angle) * radius;
                    float y = MathF.Sin(angle) * radius;
                    Vector3 localPoint;
                    if (ring == 0) localPoint = new Vector3(0, x, y);
                    else if (ring == 1) localPoint = new Vector3(x, 0, y);
                    else localPoint = new Vector3(x, y, 0);

                    Vector3 worldPoint = Vector3.Transform(localPoint, Matrix4x4.CreateFromQuaternion(rot)) + pos;
                    if (i == 0) prevPoint = worldPoint;

                    float d = DistanceToLineSegment2D(contentMouse, prevPoint, worldPoint, contentW, contentH);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = ring;
                    }
                    prevPoint = worldPoint;
                }
            }

            if (bestDist > RingPickTolerance) return -1;
            Console.WriteLine($"[TransformGizmoOverlay] *** HOVERED RING {best} (screen dist {bestDist:F3}) ***");
            return best;
        }

        private Vector3 GetPerpVector(int axis) => axis switch { 0 => Vector3.UnitY, 1 => Vector3.UnitZ, _ => Vector3.UnitX };

        private float DistanceToLineSegment2D(Vector2 p, Vector3 a3, Vector3 b3, float viewportW, float viewportH)
        {
            Vector2 a = WorldToScreen(a3, viewportW, viewportH);
            Vector2 b = WorldToScreen(b3, viewportW, viewportH);
            Vector2 ab = b - a;
            Vector2 ap = p - a;
            float proj = Vector2.Dot(ap, ab);
            float len2 = ab.LengthSquared();
            if (len2 < 1e-8f) return Vector2.Distance(p, a);
            float t = Math.Clamp(proj / len2, 0f, 1f);
            Vector2 closest = a + ab * t;
            return Vector2.Distance(p, closest);
        }

        private Vector2 WorldToScreen(Vector3 worldPos, float viewportW, float viewportH)
        {
            Vector4 clip = Vector4.Transform(new Vector4(worldPos, 1f), _lastView * _lastProjection);
            if (Math.Abs(clip.W) < 1e-6f || clip.W <= 0) return new Vector2(viewportW * 0.5f, viewportH * 0.5f);
            Vector3 ndc = new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
            return new Vector2((ndc.X * 0.5f + 0.5f) * viewportW, (1f - ndc.Y * 0.5f - 0.5f) * viewportH);
        }

        private Vector3 GetAxisVector(int axis) => axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, 2 => Vector3.UnitZ, _ => Vector3.UnitX };
    }
}