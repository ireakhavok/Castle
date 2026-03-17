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

namespace MapRoom
{
    public unsafe class TwoDCreatorScene : Scene
    {
        private AngledOrthoCamera _orthoCamera;
        private ShaderProgram _gridShader;
        private VertexBuffer _gridBuffer;
        private string _activeSpriteTexturePath = null;
        private Vector2 _activeSpriteSize = new Vector2(2f, 2f);
        private bool _spriteGhostVisible = false;
        private Vector3 _spriteGhostPosition;
        private VertexBuffer _ghostBuffer;

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
            SetupGrid();
            _ghostBuffer = new VertexBuffer(_renderContext);
            UpdateGhostMesh();
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

            var vertices = new List<float>();
            var indices = new List<uint>();
            int segments = 32;
            float r = 30f;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * MathF.PI * 2f / segments;
                float x = MathF.Cos(angle) * r;
                float y = MathF.Sin(angle) * r;
                vertices.Add(x); vertices.Add(y); vertices.Add(0f);
                vertices.Add(1f); vertices.Add(1f); vertices.Add(1f); vertices.Add(0.6f);
                vertices.Add(0f); vertices.Add(0f);
            }
            for (int i = 0; i < segments; i++)
            {
                indices.Add((uint)i);
                indices.Add((uint)((i + 1) % segments));
            }
            _ghostBuffer.UpdateCustomWithUV(vertices, indices);
        }

        private void OnSpriteSelected(SelectSpriteEvent e)
        {
            _activeSpriteTexturePath = e.TexturePath;
            _activeSpriteSize = new Vector2(e.Width, e.Height);
            UpdateGhostMesh();
            Console.WriteLine($"[TwoDCreatorScene] Sprite selected for ghost preview: {e.TexturePath}");
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _orthoCamera.Update(deltaTime, 0f, true);

            double mouseX = 0, mouseY = 0;
            _controlContext.GetCursorPos(_window, out mouseX, out mouseY);

            float worldX = (float)(mouseX - _width / 2f);
            float worldY = (float)(_height / 2f - mouseY);
            _spriteGhostPosition = new Vector3(worldX, worldY, 0.1f);
            _spriteGhostVisible = !string.IsNullOrEmpty(_activeSpriteTexturePath);
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            projection = Matrix4x4.CreateOrthographic(_width * 1.5f, _height * 1.5f, 0.1f, 1000f);
            view = _orthoCamera.ViewMatrix;

            _gridShader.Use();
            _gridShader.SetMatrix4("uView", view);
            _gridShader.SetMatrix4("uProjection", projection);
            _gridShader.SetMatrix4("uModel", Matrix4x4.Identity);
            _gridBuffer.Bind();
            _renderContext.Enable(_renderContext.Enums.LineSmooth);
            _renderContext.DrawArrays(_renderContext.Enums.Lines, 0, _gridBuffer.GetVertexCount());
            _renderContext.Disable(_renderContext.Enums.LineSmooth);

            if (_spriteGhostVisible && _ghostBuffer != null)
            {
                Matrix4x4 model = Matrix4x4.CreateTranslation(_spriteGhostPosition);
                _gridShader.SetMatrix4("uModel", model);
                _ghostBuffer.Bind();
                _renderContext.Enable(_renderContext.Enums.Blend);
                _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
                _renderContext.DrawElements(_renderContext.Enums.Lines, _ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.Disable(_renderContext.Enums.Blend);
            }
        }

        public override void Dispose()
        {
            _gridBuffer?.Dispose();
            _ghostBuffer?.Dispose();
            _gridShader?.Dispose();
            base.Dispose();
        }
    }
}