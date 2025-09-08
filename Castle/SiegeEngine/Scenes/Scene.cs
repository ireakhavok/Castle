using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Rendering;
using SiegeEngine.Rendering.Shaders;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Scenes
{
    public abstract class Scene : IDisposable
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly IntPtr _window;
        protected readonly IGameServer _server;
        protected ShaderProgram _shader;
        protected ShaderProgram _modelShader;
        protected VertexBuffer _gridBuffer;
        protected int _width;
        protected int _height;
        protected bool _disposed;
        protected readonly List<GameSystem> _systems = new List<GameSystem>();
        private Player _player; // Added for listener position
        public Scene(IRenderContext renderContext, IControlContext controlContext, IntPtr window, IGameServer server, EventBus eventBus)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _controlContext = controlContext ?? throw new ArgumentNullException(nameof(controlContext));
            _window = window;
            _server = server ?? throw new ArgumentNullException(nameof(server));
            if (eventBus != null)
            {
                var audioSystem = new AudioSystem(server, eventBus, false);
                var lightingSystem = new LightingSystem(server);
                _systems.Add(audioSystem);
                _systems.Add(lightingSystem);
            }
        }
        public void SetPlayer(Player player)
        {
            _player = player;
        }
        public virtual void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            _shader = CreateShader();
            var shaders = ShaderSetup.InitializeShaders(_renderContext);
            _modelShader = shaders.modelShader;
            _gridBuffer = new VertexBuffer(_renderContext);
            SetupGrid();
        }
        protected virtual ShaderProgram CreateShader()
        {
            return new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
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
        public virtual void Resize(int width, int height)
        {
            _width = width;
            _height = height;
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
        }
        public virtual void Update(float deltaTime)
        {
            foreach (var system in _systems)
            {
                if (system is AudioSystem audioSystem && _player?.Camera != null)
                {
                    audioSystem.SetListenerPosition(_player.Camera.Position);
                }
                system.Update(deltaTime);
            }
        }
        protected virtual Matrix4x4 GetViewMatrix() => _player?.Camera?.ViewMatrix ?? Matrix4x4.Identity;
        public virtual void Render(IReadOnlyList<Entity> entities)
        {
            if (_disposed) return;
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            Matrix4x4 view = GetViewMatrix();
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)_width / _height, 0.1f, 1000f);
            // Render grid
            _shader.Use();
            _shader.SetMatrix4("uView", view);
            _shader.SetMatrix4("uModel", Matrix4x4.Identity);
            _shader.SetMatrix4("uProjection", projection);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _gridBuffer.Bind();
            _renderContext.DrawArrays(_renderContext.Enums.Lines, 0, _gridBuffer.GetVertexCount());
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            // Render player model
            _modelShader.Use();
            _modelShader.SetMatrix4("uView", view);
            _modelShader.SetMatrix4("uProjection", projection);
            var lightingSystem = _systems.Find(s => s is LightingSystem) as LightingSystem;
            if (lightingSystem != null)
            {
                var lightData = lightingSystem.GetShaderUniforms();
                if (lightData.HasValue)
                {
                    _modelShader.SetUniform("uLightDir", lightData.Value.direction.X, lightData.Value.direction.Y, lightData.Value.direction.Z, 0.0f);
                    _modelShader.SetUniform("uLightColor", lightData.Value.color.X, lightData.Value.color.Y, lightData.Value.color.Z, 0.0f);
                    _modelShader.SetUniform("uLightIntensity", lightData.Value.intensity);
                }
            }
            _modelShader.SetUniform("uViewPos", _player?.Camera?.Position.X ?? 0f, _player?.Camera?.Position.Y ?? 0f, _player?.Camera?.Position.Z ?? 0f, 0.0f);
            _modelShader.SetUniform("uAmbientStrength", 0.1f);
            _modelShader.SetUniform("uSpecularStrength", 0.5f);
            _modelShader.SetUniform("uShininess", 32.0f);
        }
        public virtual void AddSystem(GameSystem system)
        {
            _systems.Add(system);
        }
        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _shader?.Dispose();
                _modelShader?.Dispose();
                _gridBuffer?.Dispose();
                foreach (var system in _systems)
                {
                    if (system is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                _disposed = true;
            }
        }
    }
}