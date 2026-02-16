// Folder: SiegeEngine.Scenes.StartingPoints
// File: SandboxScene.cs
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Scenes.StartingPoints
{
    public unsafe class SandboxScene : Scene
    {
        public static void Launch(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new SwitchSceneEvent("Sandbox"));
        }

        private readonly Player _player;
        private readonly PlayerMovement _playerMovement;
        private readonly ModelManager _modelManager;
        private readonly IGameServer _server;
        private float _scrollDelta;
        private ShaderProgram _gridShader;

        // Added back the grid buffer that was used in RenderContent
        protected VertexBuffer _gridBuffer;

        public SandboxScene(IRenderContext renderContext, IControlContext controlContext, nint window, Player player, IGameServer server, PlayerMovement playerMovement, EventBus eventBus, ModelManager modelManager)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _playerMovement = playerMovement ?? throw new ArgumentNullException(nameof(playerMovement));
            _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _scrollDelta = 0f;
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _renderContext.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);

            _gridShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
            _gridBuffer = new VertexBuffer(_renderContext);
            SetupGrid();

            _controlContext.SetScrollCallback(_window, (w, xoffset, yoffset) => _scrollDelta = (float)yoffset);
            _controlContext.SetWindowSizeCallback(_window, (w, newWidth, newHeight) =>
            {
                _width = newWidth;
                _height = newHeight;
                _renderContext.Viewport(0, 0, (uint)newWidth, (uint)newHeight);
            });
        }

        protected virtual void SetupGrid()
        {
            var vertices = new List<Vertex>();
            int width = 128;
            int height = 72;
            float size = 5.0f;
            for (float x = 0; x <= width; x += size)
            {
                vertices.Add(new Vertex(x, 0, 0, 0.6f, 0.6f, 0.6f, 1.0f));
                vertices.Add(new Vertex(x, height, 0, 0.6f, 0.6f, 0.6f, 1.0f));
            }
            for (float y = 0; y <= height; y += size)
            {
                vertices.Add(new Vertex(0, y, 0, 0.6f, 0.6f, 0.6f, 1.0f));
                vertices.Add(new Vertex(width, y, 0, 0.6f, 0.6f, 0.6f, 1.0f));
            }
            var indices = new List<uint>();
            for (uint i = 0; i < vertices.Count; i++)
                indices.Add(i);
            _gridBuffer.UpdateCustom(vertices, indices);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _player.Update(deltaTime, _window, _scrollDelta, _playerMovement, true);
            _scrollDelta = 0f;
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            // Grid
            _gridShader.Use();
            _gridShader.SetMatrix4("uModel", Matrix4x4.Identity);
            _gridShader.SetMatrix4("uView", view);
            _gridShader.SetMatrix4("uProjection", projection);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _gridBuffer.Bind();
            _renderContext.DrawArrays(_renderContext.Enums.Lines, 0, _gridBuffer.GetVertexCount());
            _renderContext.Enable(_renderContext.Enums.DepthTest);

            // Player model using shared renderer
            var playerEntity = _server.GetEntityById(_player.EntityId);
            var modelComp = playerEntity?.GetComponent<ModelComponent>();
            var physics = _player.Physics;

            if (modelComp != null && physics != null)
            {
                _modelRenderer.RenderModel(modelComp, physics, view, projection, _player.Camera?.Position ?? Vector3.Zero, _modelManager);
            }
        }

        public override void Dispose()
        {
            _gridShader?.Dispose();
            _gridBuffer?.Dispose();
            base.Dispose();
        }
    }
}