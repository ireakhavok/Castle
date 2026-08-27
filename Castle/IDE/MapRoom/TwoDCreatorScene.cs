// Folder: MapRoom
// File: TwoDCreatorScene.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.Renderers;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using SiegeEngine.Core.GPU.ContextManagement;
namespace MapRoom
{
    public unsafe class TwoDCreatorScene : Scene
    {
        private AngledOrthoCamera _orthoCamera;
        private LineRenderer _lineRenderer;
        private SpriteRenderer _spriteRenderer;
        private VertexBuffer _gridBuffer;
        private string _activeSpriteTexturePath = null;
        private Vector2 _activeSpriteSize = new Vector2(2f, 2f);
        private Vector2 _activeSpriteNativeAspect = new Vector2(1f, 1f);
        private bool _spriteGhostVisible = false;
        private Vector3 _spriteGhostPosition = Vector3.Zero;
        private VertexBuffer _ghostBuffer;
        private uint _ghostTextureId = 0;
        private readonly Dictionary<string, uint> _placedTextureCache = new Dictionary<string, uint>();

        public Vector3 CameraPosition => _orthoCamera?.Position ?? Vector3.Zero;
        public Matrix4x4 ViewMatrix => _orthoCamera?.ViewMatrix ?? Matrix4x4.Identity;
        public Matrix4x4 ProjectionMatrix { get; private set; }

        public TwoDCreatorScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _eventBus.Subscribe<SelectSpriteEvent>(OnSpriteSelected);
            _eventBus.Subscribe<EntityPlacedEvent>(OnEntityPlaced);
        }

        public Vector3 ScreenToWorldPlane(Vector2 normalizedMouse, out bool hitPlane)
        {
            hitPlane = normalizedMouse.X >= 0f && normalizedMouse.X <= 1f &&
                       normalizedMouse.Y >= 0f && normalizedMouse.Y <= 1f;

            if (!hitPlane || _orthoCamera == null)
            {
                return _spriteGhostPosition; // hold last valid position
            }

            float ndcX = normalizedMouse.X * 2f - 1f;
            float ndcY = 1f - normalizedMouse.Y * 2f;

            Matrix4x4 proj = ProjectionMatrix;
            Matrix4x4 view = _orthoCamera.ViewMatrix;

            // Step 1: Invert projection (ortho is simple)
            if (!Matrix4x4.Invert(proj, out Matrix4x4 invProj))
            {
                goto fallback;
            }

            // Unproject near and far points to eye space
            Vector4 ndcNear = new Vector4(ndcX, ndcY, -1f, 1f);
            Vector4 ndcFar = new Vector4(ndcX, ndcY, 1f, 1f);

            Vector4 eyeNearH = Vector4.Transform(ndcNear, invProj);
            Vector4 eyeFarH = Vector4.Transform(ndcFar, invProj);

            // Dehomogenize (divide by W) - this was the source of the CS0029 error
            Vector3 eyeNear = new Vector3(eyeNearH.X / eyeNearH.W, eyeNearH.Y / eyeNearH.W, eyeNearH.Z / eyeNearH.W);
            Vector3 eyeFar = new Vector3(eyeFarH.X / eyeFarH.W, eyeFarH.Y / eyeFarH.W, eyeFarH.Z / eyeFarH.W);

            // Step 2: Invert view to world space
            if (!Matrix4x4.Invert(view, out Matrix4x4 invView))
            {
                goto fallback;
            }

            Vector3 worldNear = Vector3.Transform(eyeNear, invView);
            Vector3 worldFar = Vector3.Transform(eyeFar, invView);

            Vector3 rayOrigin = worldNear;
            Vector3 rayDir = Vector3.Normalize(worldFar - worldNear);

            // Step 3: Intersect ray with world Z=0 plane
            float denom = rayDir.Z;
            if (MathF.Abs(denom) < 0.00001f)
            {
                goto fallback; // ray parallel to plane
            }

            float t = -rayOrigin.Z / denom;
            if (t < -0.001f)
            {
                goto fallback; // intersection behind camera
            }

            Vector3 hit = rayOrigin + t * rayDir;
            Vector3 result = new Vector3(hit.X, hit.Y, 0f);
            _spriteGhostPosition = result;
            return result;

        fallback:
            // Fallback: approximate by transforming eye-space mouse point at Z=0
            float halfWidth = (_width * 1.5f) / 2f;
            float halfHeight = (_height * 1.5f) / 2f;
            Vector3 eyePos = new Vector3(ndcX * halfWidth, ndcY * halfHeight, 0f);
            if (Matrix4x4.Invert(view, out invView))
            {
                Vector3 worldApprox = Vector3.Transform(eyePos, invView);
                _spriteGhostPosition = new Vector3(worldApprox.X, worldApprox.Y, 0f);
                return _spriteGhostPosition;
            }

            // Ultimate fallback: just camera position (should never reach)
            _spriteGhostPosition = CameraPosition;
            return _spriteGhostPosition;
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _orthoCamera = new AngledOrthoCamera(_controlContext, _window);
            _lineRenderer = new LineRenderer(_renderContext);
            _lineRenderer.Initialize();
            _spriteRenderer = new SpriteRenderer(_renderContext);
            _spriteRenderer.Initialize();
            ProjectionMatrix = Matrix4x4.CreateOrthographic(width * 1.5f, height * 1.5f, 0.1f, 1000f);
            SetupGrid();
            _ghostBuffer = new VertexBuffer(_renderContext);
            UpdateGhostMesh();
            _orthoCamera.Update(0f, 0f, false);
        }

        public override void Resize(int width, int height)
        {
            base.Resize(width, height);
            ProjectionMatrix = Matrix4x4.CreateOrthographic(width * 1.5f, height * 1.5f, 0.1f, 1000f);
        }

        private void SetupGrid()
        {
            var vertices = new List<Vertex>();
            for (int x = -5000; x <= 5000; x += 100)
            {
                vertices.Add(new Vertex(x, -5000, 0, 0.3f, 0.3f, 0.3f, 1f));
                vertices.Add(new Vertex(x, 5000, 0, 0.3f, 0.3f, 0.3f, 1f));
            }
            for (int y = -5000; y <= 5000; y += 100)
            {
                vertices.Add(new Vertex(-5000, y, 0, 0.3f, 0.3f, 0.3f, 1f));
                vertices.Add(new Vertex(5000, y, 0, 0.3f, 0.3f, 0.3f, 1f));
            }
            _gridBuffer = new VertexBuffer(_renderContext);
            _gridBuffer.UpdateCustom(vertices, new List<uint>());
        }

        private void UpdateGhostMesh()
        {
            if (_ghostBuffer == null) return;
            float aspect = _activeSpriteNativeAspect.X / _activeSpriteNativeAspect.Y;
            float baseSize = 2f;
            float w = (aspect > 1f ? baseSize * aspect : baseSize) * 25f;
            float h = (aspect > 1f ? baseSize : baseSize / aspect) * 25f;
            var vertices = new List<float>
            {
                -w, -h, 0, 1f,1f,1f,0.95f, 0f, 1f,
                 w, -h, 0, 1f,1f,1f,0.95f, 1f, 1f,
                 w, h, 0, 1f,1f,1f,0.95f, 1f, 0f,
                -w, h, 0, 1f,1f,1f,0.95f, 0f, 0f
            };
            var indices = new List<uint> { 0, 1, 2, 0, 2, 3 };
            _ghostBuffer.UpdateCustomWithUV(vertices, indices);
        }

        private void OnSpriteSelected(SelectSpriteEvent e)
        {
            TextureLoader.DeleteTexture(_renderContext, ref _ghostTextureId);
            _activeSpriteTexturePath = e.TexturePath;
            if (string.IsNullOrEmpty(_activeSpriteTexturePath))
            {
                _spriteGhostVisible = false;
                return;
            }
            var (texId, nativeSize) = TextureLoader.LoadTextureWithSize(_renderContext, e.TexturePath);
            _ghostTextureId = texId;
            _activeSpriteNativeAspect = nativeSize.X > 0 && nativeSize.Y > 0 ? nativeSize : new Vector2(1f, 1f);
            _activeSpriteSize = new Vector2(e.Width, e.Height);
            UpdateGhostMesh();
            _spriteGhostVisible = true;
        }

        private void OnEntityPlaced(EntityPlacedEvent e)
        {
            if (e.EntityType != "Sprite" || string.IsNullOrEmpty(e.TexturePath)) return;
            if (!_placedTextureCache.ContainsKey(e.TexturePath))
            {
                var (texId, _) = TextureLoader.LoadTextureWithSize(_renderContext, e.TexturePath);
                if (texId != 0) _placedTextureCache[e.TexturePath] = texId;
            }
            var entity = new Entity { Id = e.EntityId, Type = "Sprite" };
            var transform = entity.GetComponent<TransformComponent>();
            if (transform == null)
            {
                transform = new TransformComponent();
                entity.AddComponent(transform);
            }
            transform.Position = e.Position with { Z = 0f };
            transform.Rotation = e.Rotation;
            transform.Scale = new Vector3(e.Width > 0 ? e.Width : 2f, e.Height > 0 ? e.Height : 2f, 1f);
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null)
            {
                physics = new PhysicsComponent();
                entity.AddComponent(physics);
            }
            physics.Position = e.Position;
            var sprite = new SpriteComponent
            {
                TexturePath = e.TexturePath,
                Size = new Vector2(e.Width, e.Height)
            };
            entity.AddComponent(sprite);
            _server.AddEntity(entity);
        }

        public void Update(float deltaTime, bool cameraActive, Vector3 worldMousePos, bool mouseReleased)
        {
            base.Update(deltaTime);
            _orthoCamera.Update(deltaTime, 0f, cameraActive);

            if (!cameraActive && !string.IsNullOrEmpty(_activeSpriteTexturePath))
            {
                _spriteGhostPosition = worldMousePos;
                _spriteGhostVisible = true;

                if (mouseReleased)
                {
                    var evt = new EntityPlacedEvent(0, "Sprite", worldMousePos)
                    {
                        TexturePath = _activeSpriteTexturePath,
                        Width = _activeSpriteSize.X,
                        Height = _activeSpriteSize.Y
                    };
                    _eventBus.Publish(evt);
                }
            }
            else
            {
                _spriteGhostVisible = false;
            }
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            if (_orthoCamera == null) return;

            ProjectionMatrix = Matrix4x4.CreateOrthographic(_width * 1.5f, _height * 1.5f, 0.1f, 1000f);
            view = _orthoCamera.ViewMatrix;

            _lineRenderer.DrawLines(_gridBuffer, view, ProjectionMatrix, 1f, true);

            if (entities != null)
            {
                _spriteRenderer.Begin(view, ProjectionMatrix);
                foreach (var entity in entities)
                {
                    if (entity.Type != "Sprite") continue;
                    var sprite = entity.GetComponent<SpriteComponent>();
                    var transform = entity.GetComponent<TransformComponent>();
                    if (sprite == null || transform == null || string.IsNullOrEmpty(sprite.TexturePath)) continue;

                    if (!_placedTextureCache.TryGetValue(sprite.TexturePath, out uint texId))
                    {
                        var (newId, _) = TextureLoader.LoadTextureWithSize(_renderContext, sprite.TexturePath);
                        if (newId != 0) _placedTextureCache[sprite.TexturePath] = newId;
                        texId = newId;
                    }
                    if (texId == 0) continue;

                    var model = Matrix4x4.CreateScale(transform.Scale) * Matrix4x4.CreateTranslation(transform.Position);
                    _spriteRenderer.Draw(_ghostBuffer, texId, model);
                }
                _spriteRenderer.End();
            }

            if (_spriteGhostVisible && _ghostBuffer != null && _ghostTextureId != 0)
            {
                _spriteRenderer.Begin(view, ProjectionMatrix);
                _spriteRenderer.Draw(
                    _ghostBuffer,
                    _ghostTextureId,
                    Matrix4x4.CreateScale(new Vector3(_activeSpriteSize.X, _activeSpriteSize.Y, 1f)) * Matrix4x4.CreateTranslation(_spriteGhostPosition));
                _spriteRenderer.End();
            }
        }

        public override void Dispose()
        {
            var cached = new List<uint>(_placedTextureCache.Values);
            for (int i = 0; i < cached.Count; i++)
            {
                uint tex = cached[i];
                TextureLoader.DeleteTexture(_renderContext, ref tex);
            }
            _placedTextureCache.Clear();
            TextureLoader.DeleteTexture(_renderContext, ref _ghostTextureId);
            _gridBuffer?.Dispose();
            _ghostBuffer?.Dispose();
            _lineRenderer?.Dispose();
            _spriteRenderer?.Dispose();
            base.Dispose();
        }
    }
}