// Folder: SiegeEngine/Scenes/StartingPoints
// File: TwoDSandboxScene.cs
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
    public class TwoDSandboxScene : Scene
    {
        private readonly IProjectionProvider _projectionProvider = new OrthoProjection(30f); // 30-degree tilt
        private ShaderProgram _gridShader;
        private VertexBuffer _gridBuffer;
        private ModelManager _modelManager;

        public TwoDSandboxScene(IRenderContext renderContext, IControlContext controlContext, IntPtr window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _modelManager = new ModelManager(_renderContext);
            SetupGrid();
            _gridShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            _gridBuffer = new VertexBuffer(_renderContext);
            _controlContext.SetScrollCallback(_window, (win, x, y) =>
            {
                // Handle zoom for ortho
            });
            _controlContext.SetWindowSizeCallback(_window, (win, w, h) =>
            {
                _renderContext.Viewport(0, 0, (uint)w, (uint)h);
            });
            _renderContext.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
        }

        private void SetupGrid()
        {
            var vertices = new List<Vertex>();
            for (int x = -1000; x <= 1000; x += 10)
            {
                vertices.Add(new Vertex(x, -1000, 0, 0.2f, 0.2f, 0.2f, 1f));
                vertices.Add(new Vertex(x, 1000, 0, 0.2f, 0.2f, 0.2f, 1f));
            }
            for (int y = -1000; y <= 1000; y += 10)
            {
                vertices.Add(new Vertex(-1000, y, 0, 0.2f, 0.2f, 0.2f, 1f));
                vertices.Add(new Vertex(1000, y, 0, 0.2f, 0.2f, 0.2f, 1f));
            }
            var indices = new List<uint>();
            _gridBuffer.UpdateCustom(vertices, indices);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            projection = _projectionProvider.GetProjectionMatrix(_width, _height, 0.1f, 1000f);
            _gridShader.Use();
            _gridShader.SetMatrix4("uView", view);
            _gridShader.SetMatrix4("uProjection", projection);
            _gridShader.SetMatrix4("uModel", Matrix4x4.Identity);
            _gridBuffer.Bind();
            _renderContext.Enable(_renderContext.Enums.LineSmooth);
            _renderContext.DrawArrays(_renderContext.Enums.Lines, 0, _gridBuffer.GetVertexCount());
            _renderContext.Disable(_renderContext.Enums.LineSmooth);
            Vector3 viewPos = _player.Camera.Position;
            foreach (var entity in entities)
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                var physics = entity.GetComponent<PhysicsComponent>();
                if (modelComp != null && physics != null)
                {
                    _modelRenderer.RenderModel(modelComp, physics, view, projection, viewPos, _modelManager);
                }
            }
            var playerEntity = _server.GetEntityById(_player.EntityId);
            if (playerEntity != null)
            {
                var modelComp = playerEntity.GetComponent<ModelComponent>();
                var physics = playerEntity.GetComponent<PhysicsComponent>();
                if (modelComp != null && physics != null)
                {
                    _modelRenderer.RenderModel(modelComp, physics, view, projection, viewPos, _modelManager);
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}