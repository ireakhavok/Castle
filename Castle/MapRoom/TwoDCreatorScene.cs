// Folder: MapRoom
// File: TwoDCreatorScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;

namespace MapRoom
{
    public unsafe class TwoDCreatorScene : Scene
    {
        private AngledOrthoCamera _orthoCamera;
        private ShaderProgram _gridShader;
        private ShaderProgram _spriteShader; // NEW: dedicated textured sprite/ghost shader
        private VertexBuffer _gridBuffer;
        private string _activeSpriteTexturePath = null;
        private Vector2 _activeSpriteSize = new Vector2(2f, 2f);
        private bool _spriteGhostVisible = false;
        private Vector3 _spriteGhostPosition;
        private VertexBuffer _ghostBuffer;
        private uint _ghostTextureId = 0;

        public TwoDCreatorScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _eventBus.Subscribe<SelectSpriteEvent>(OnSpriteSelected);
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _orthoCamera = new AngledOrthoCamera(_controlContext, _window);

            _gridShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            _spriteShader = new ShaderProgram(_renderContext, SpriteShader.VertexShaderSource, SpriteShader.FragmentShaderSource); // NEW

            SetupGrid();

            _ghostBuffer = new VertexBuffer(_renderContext);
            UpdateGhostMesh();

            _orthoCamera.Update(0f, 0f, false);
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

            float w = _activeSpriteSize.X * 25f;
            float h = _activeSpriteSize.Y * 25f;

            var vertices = new List<float>
            {
                // x, y, z,    r, g, b, a,    u, v
                -w, -h, 0,    1f,1f,1f,0.95f,  0f, 0f,
                 w, -h, 0,    1f,1f,1f,0.95f,  1f, 0f,
                 w,  h, 0,    1f,1f,1f,0.95f,  1f, 1f,
                -w,  h, 0,    1f,1f,1f,0.95f,  0f, 1f
            };

            var indices = new List<uint> { 0, 1, 2, 0, 2, 3 };

            _ghostBuffer.UpdateCustomWithUV(vertices, indices);
        }

        private void OnSpriteSelected(SelectSpriteEvent e)
        {
            if (_ghostTextureId != 0)
            {
                _renderContext.DeleteTexture(_ghostTextureId);
                _ghostTextureId = 0;
            }

            _activeSpriteTexturePath = e.TexturePath;
            _activeSpriteSize = new Vector2(e.Width, e.Height);

            UpdateGhostMesh();

            var (texId, _) = TextureLoader.LoadTexture(_renderContext, e.TexturePath);
            _ghostTextureId = texId;

            if (_ghostTextureId == 0)
            {
                Console.WriteLine($"[ERROR TwoDCreatorScene] Failed to load ghost texture: {Path.GetFileName(e.TexturePath)}");
            }
            else
            {
                Console.WriteLine($"[TwoDCreatorScene] Real PNG ghost loaded: {Path.GetFileName(e.TexturePath)} (ID {_ghostTextureId})");
            }
        }

        public void Update(float deltaTime, bool cameraActive, Vector3 worldMousePos, bool mousePressed)
        {
            base.Update(deltaTime);
            _orthoCamera.Update(deltaTime, 0f, cameraActive);

            if (!cameraActive && !string.IsNullOrEmpty(_activeSpriteTexturePath))
            {
                _spriteGhostPosition = worldMousePos;
                _spriteGhostVisible = true;

                if (mousePressed)
                {
                    Console.WriteLine($"[TwoDCreatorScene] Placing '{Path.GetFileName(_activeSpriteTexturePath)}' at {worldMousePos}");
                    _eventBus.Publish(new EntityPlacedEvent(0, "Sprite", worldMousePos, false));
                }
            }
            else
            {
                _spriteGhostVisible = false;
            }
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            projection = Matrix4x4.CreateOrthographic(_width * 1.5f, _height * 1.5f, 0.1f, 1000f);
            view = _orthoCamera.ViewMatrix;

            // ─── Grid (still uses point shader) ────────────────────────────────────────
            _gridShader.Use();
            _gridShader.SetMatrix4("uView", view);
            _gridShader.SetMatrix4("uProjection", projection);
            _gridShader.SetMatrix4("uModel", Matrix4x4.Identity);

            _gridBuffer.Bind();
            _renderContext.Enable(_renderContext.Enums.LineSmooth);
            _renderContext.DrawArrays(_renderContext.Enums.Lines, 0, _gridBuffer.GetVertexCount());
            _renderContext.Disable(_renderContext.Enums.LineSmooth);

            // ─── Ghost sprite (uses new textured sprite shader) ────────────────────────
            if (_spriteGhostVisible && _ghostBuffer != null && _ghostTextureId != 0)
            {
                _spriteShader.Use();
                _spriteShader.SetMatrix4("uModel", Matrix4x4.CreateTranslation(_spriteGhostPosition));
                _spriteShader.SetMatrix4("uView", view);
                _spriteShader.SetMatrix4("uProjection", projection);

                _renderContext.ActiveTexture(0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, _ghostTextureId);

                _ghostBuffer.Bind();
                _renderContext.Enable(_renderContext.Enums.Blend);
                _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, 6, _renderContext.Enums.UnsignedInt, null);
                _renderContext.Disable(_renderContext.Enums.Blend);

                _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0); // clean up
            }
        }

        public override void Dispose()
        {
            if (_ghostTextureId != 0) _renderContext.DeleteTexture(_ghostTextureId);
            _gridBuffer?.Dispose();
            _ghostBuffer?.Dispose();
            _gridShader?.Dispose();
            _spriteShader?.Dispose(); // NEW
            base.Dispose();
        }
    }
}